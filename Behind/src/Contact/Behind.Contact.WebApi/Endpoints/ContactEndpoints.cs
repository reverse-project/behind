using System.Data;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Middleware;
using Behind.Contact.Database;
using Behind.Contact.Database.Commands;
using Behind.Contact.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Behind.Contact.WebApi.Endpoints;

public static class ContactEndpoints
{
    private static ContactFormResponse Public(Keyed<ContactForm> x)
    {
        var schema = ContactValidation.Schema(x.Entity);
        return new(x.Key, x.Version, schema.Name, schema.Description, schema.SubjectMode, schema.Subjects, schema.Fields);
    }

    private static ManagedContactForm Managed(Keyed<ContactForm> x, bool deleted)
    {
        var schema = ContactValidation.Schema(x.Entity);
        return new(x.Key, x.Version, schema.Name, schema.Description, x.Entity.Recipient, schema.SubjectMode, schema.Subjects, schema.Fields, deleted);
    }

    private static ContactRequestResponse Managed(Keyed<ContactRequest> x, bool deleted, DateTimeOffset? createdAt, IReadOnlyDictionary<Guid, string> readers) =>
        new(x.Key, x.Version, x.Entity.FormId, x.Entity.FormName, x.Entity.Recipient, x.Entity.Name, x.Entity.Email, x.Entity.Subject, x.Entity.Message, ContactValidation.Values(x.Entity.FieldsJson), x.Entity.DeliveryStatus, x.Entity.ReadBy, deleted, createdAt,
            x.Entity.ReadBy is { } reader ? readers.GetValueOrDefault(reader, reader.ToUserLabel(null)) : null);

    private static async Task<IReadOnlyDictionary<Guid, string>> Readers(IReadOnlyCollection<Keyed<ContactRequest>> rows, IModuleUsers users, CancellationToken cancellationToken)
    {
        var ids = rows.Where(row => row.Entity.ReadBy.HasValue).Select(row => row.Entity.ReadBy!.Value).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }
        return (await users.Find(ids, cancellationToken)).ToDictionary(profile => profile.Id, profile => profile.DisplayName);
    }

    [IgnoreETag]
    public static async Task<IResult> Forms(HttpContext http, IContactContext context)
    {
        http.Response.Headers.CacheControl = "no-store";
        var forms = await context.InDeletionState(context.ContactForms, DeletionState.Active).LatestOf(context.ContactForms).ToKeyedAsync(http.RequestAborted);
        return Results.Ok(forms.Select(Public).OrderBy(x => x.Name));
    }

    [IgnoreETag]
    public static async Task<IResult> Form(Guid id, HttpContext http, IContactContext context)
    {
        http.Response.Headers.CacheControl = "no-store";
        var form = (await context.InDeletionState(context.ContactForms, DeletionState.Active).LatestOf(context.ContactForms).OfKey(id).ToKeyedAsync(http.RequestAborted)).FirstOrDefault();
        return form is null ? Results.NotFound() : Results.Ok(Public(form));
    }

    [IgnoreETag]
    public static async Task<IResult> Submit(Guid id, SubmitContactRequest request, HttpContext http, IContactContext context, ContactRequestCommands commands, IModuleEmailSender sender, ILoggerFactory logs)
    {
        var form = (await context.InDeletionState(context.ContactForms, DeletionState.Active).LatestOf(context.ContactForms).OfKey(id).ToKeyedAsync(http.RequestAborted)).FirstOrDefault();
        if (form is null)
        {
            return Results.NotFound();
        }
        if (!ContactValidation.Valid(request, form.Entity, out var fields))
        {
            return Results.BadRequest();
        }
        var deliveryRequested = form.Entity.Recipient.Length > 0;
        var draft = new ContactRequest(id, form.Entity.Name, form.Entity.Recipient, request.Name, request.Email, request.Subject, request.Message, ContactValidation.Json(fields), deliveryRequested ? "failed" : "notRequested", null);
        var saved = await commands.Create(draft, CancellationToken.None);
        if (saved is not VersionedSaveResult.Saved created)
        {
            return saved.ToResult();
        }
        if (!deliveryRequested)
        {
            return Results.Created($"/contact/manage/requests/{created.Id.Key}", new ContactReceipt("notRequested"));
        }
        var outcome = await Deliver(created.Id.Key, context, commands, sender, logs.CreateLogger("Contact.Delivery"));
        var status = outcome switch
        {
            DeliveryResult.Sent => "sent",
            DeliveryResult.Failed => "failed",
            _ => "sending"
        };
        return Results.Created($"/contact/manage/requests/{created.Id.Key}", new ContactReceipt(status));
    }

    [IgnoreETag]
    public static Task<IResult> ManageForms(HttpContext http, IContactContext context, IAuthorizationService auth, bool deleted = false) =>
        http.WithDeletionState(auth, deleted, async state =>
        {
            using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
            var rows = await context.InDeletionState(context.ContactForms, state).LatestOf(context.ContactForms).ToKeyedAsync(http.RequestAborted);
            return Results.Ok(rows.Select(x => Managed(x, deleted)).OrderBy(x => x.Name));
        });

    [IgnoreETag]
    public static Task<IResult> ManageForm(Guid id, HttpContext http, IContactContext context, IAuthorizationService auth, bool deleted = false) =>
        http.WithDeletionState(auth, deleted, async state =>
        {
            using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
            var row = (await context.InDeletionState(context.ContactForms, state).LatestOf(context.ContactForms).OfKey(id).ToKeyedAsync(http.RequestAborted)).FirstOrDefault();
            return row is null ? Results.NotFound() : Results.Ok(Managed(row, deleted));
        });

    [IgnoreETag]
    public static async Task<IResult> CreateForm(SaveContactForm request, HttpContext http, ContactFormCommands commands)
    {
        if (!ContactValidation.Valid(request))
        {
            return Results.BadRequest();
        }
        return (await commands.Create(Draft(request), http.RequestAborted)).ToResult();
    }

    [IgnoreETag]
    public static async Task<IResult> UpdateForm(Guid id, SaveContactForm request, HttpContext http, IContactContext context, ContactFormCommands commands)
    {
        if (!ContactValidation.Valid(request))
        {
            return Results.BadRequest();
        }
        if (request.Version is not > 0)
        {
            return Results.BadRequest();
        }
        return (await commands.UpdateExpected(id, request.Version.Value, Draft(request), http.RequestAborted)).ToResult();
    }

    private static ContactForm Draft(SaveContactForm x) => new(x.Name, x.Description, x.Recipient ?? "", x.SubjectMode, ContactValidation.Json(x.Subjects), ContactValidation.Json(x.Fields));

    [IgnoreETag]
    public static Task<IResult> ManageRequests(HttpContext http, IContactContext context, IAuthorizationService auth, IModuleCreationOrder order, IModuleUsers users,
        bool deleted = false, int? page = null, int? pageSize = null, string? search = null, bool unreadOnly = false) =>
        http.WithManagedList(auth, search, null, deleted, async filter =>
        {
            using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
            var flipper = Flipper.Create(page, pageSize, ManagedListExtensions.DefaultPageSize, ManagedListExtensions.MaxPageSize);
            if ((long)(flipper.Index - 1) * flipper.Size > int.MaxValue)
            {
                return Results.BadRequest();
            }
            var query = context.InDeletionState(context.ContactRequests, filter.State).LatestOf(context.ContactRequests);
            if (unreadOnly)
            {
                query = query.Where(request => request.ReadBy == null);
            }
            query = filter.Search.Map(term => term.ToLowerInvariant()).Match(
                () => query,
                term => query.Where(request => request.Name.ToLower().Contains(term)
                    || request.Email.ToLower().Contains(term)
                    || request.Subject.ToLower().Contains(term)
                    || request.Message.ToLower().Contains(term)
                    || request.FormName.ToLower().Contains(term)));
            var total = await query.LongCountAsync(http.RequestAborted);
            var rows = await flipper.Apply(order.NewestFirst(query)).ToKeyedAsync(http.RequestAborted);
            var times = await order.CreatedAtAsync<ContactRequest>(rows.Select(row => row.Key).ToArray(), http.RequestAborted);
            var readers = await Readers(rows, users, http.RequestAborted);
            return Results.Ok(flipper.ToPage(total, rows).ToPageResponse(row => Managed(row, deleted, times[row.Key], readers)));
        });

    [IgnoreETag]
    public static Task<IResult> ManageRequest(Guid id, HttpContext http, IContactContext context, IAuthorizationService auth, IModuleCreationOrder order, IModuleUsers users, bool deleted = false) =>
        http.WithDeletionState(auth, deleted, async state =>
        {
            using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
            var row = (await context.InDeletionState(context.ContactRequests, state).LatestOf(context.ContactRequests).OfKey(id).ToKeyedAsync(http.RequestAborted)).FirstOrDefault();
            if (row is null)
            {
                return Results.NotFound();
            }
            var times = await order.CreatedAtAsync<ContactRequest>([row.Key], http.RequestAborted);
            var readers = await Readers([row], users, http.RequestAborted);
            return Results.Ok(Managed(row, deleted, times[row.Key], readers));
        });

    [IgnoreETag]
    public static async Task<IResult> Read(Guid id, SetReadRequest request, HttpContext http, IContactContext context, ContactRequestCommands commands, IModuleIdentity identity)
    {
        var user = await identity.UserId(http);
        if (user is null)
        {
            return Results.Unauthorized();
        }
        var result = await Change(id, context, commands, row => row with { ReadBy = request.Read ? user : null });
        return result switch
        {
            MutationResult.Changed => Results.NoContent(),
            MutationResult.Missing => Results.NotFound(),
            _ => Results.Conflict()
        };
    }

    [IgnoreETag]
    public static async Task<IResult> Retry(Guid id, IContactContext context, ContactRequestCommands commands, IModuleEmailSender sender, ILoggerFactory logs)
    {
        var row = await Current(id, context, CancellationToken.None);
        if (row is null)
        {
            return Results.NotFound();
        }
        if (row.Entity.DeliveryStatus != "failed")
        {
            return Results.Conflict();
        }
        var outcome = await Deliver(id, context, commands, sender, logs.CreateLogger("Contact.Delivery"));
        return outcome switch
        {
            DeliveryResult.Sent => Results.NoContent(),
            DeliveryResult.Missing => Results.NotFound(),
            DeliveryResult.Conflict => Results.Conflict(),
            _ => Results.StatusCode(503)
        };
    }

    private static async Task<Keyed<ContactRequest>?> Current(Guid id, IContactContext context, CancellationToken token) =>
        (await context.InDeletionState(context.ContactRequests, DeletionState.Active).LatestOf(context.ContactRequests).OfKey(id).ToKeyedAsync(token)).FirstOrDefault();

    private enum MutationResult { Changed, Missing, Conflict }
    private enum DeliveryResult { Sent, Failed, Uncertain, Missing, Conflict }

    private static async Task<MutationResult> Change(Guid id, IContactContext context, ContactRequestCommands commands, Func<ContactRequest, ContactRequest?> update)
    {
        await using var transaction = await context.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        var current = await Current(id, context, CancellationToken.None);
        if (current is null)
        {
            return MutationResult.Missing;
        }
        var next = update(current.Entity);
        if (next is null)
        {
            return MutationResult.Conflict;
        }
        await commands.SaveExpected(id, current.Version, next, CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        return MutationResult.Changed;
    }

    public static async Task<IResult> DeleteRequest(Guid id, IContactContext context, ContactRequestCommands commands, int operation)
    {
        await using var transaction = await context.Context.Database.BeginTransactionAsync(IsolationLevel.Serializable, CancellationToken.None);
        using var scope = operation == 0 ? null : context.SoftDelete.IncludeDeleted();
        var state = operation == 0 ? DeletionState.Active : DeletionState.Deleted;
        var rows = await context.InDeletionState(context.ContactRequests, state).LatestOf(context.ContactRequests).OfKey(id).ToKeyedAsync(CancellationToken.None);
        if (rows.Count == 0)
        {
            return Results.NotFound();
        }
        IResult result;
        if (operation == 0)
        {
            result = (await commands.Delete(id, CancellationToken.None)).ToNoContentResult();
        }
        else if (operation == 1)
        {
            result = (await commands.Restore(id, CancellationToken.None)).ToNoContentResult();
        }
        else
        {
            result = (await commands.HardDeleteChecked(id, CancellationToken.None)).ToResult();
        }
        await transaction.CommitAsync(CancellationToken.None);
        return result;
    }

    private static async Task<DeliveryResult> Deliver(Guid id, IContactContext context, ContactRequestCommands commands, IModuleEmailSender sender, ILogger logger)
    {
        var row = await Current(id, context, CancellationToken.None);
        if (row is null)
        {
            return DeliveryResult.Missing;
        }
        var fields = ContactValidation.Values(row.Entity.FieldsJson);
        var claimed = await Change(id, context, commands, current => current.DeliveryStatus == "failed" ? current with { DeliveryStatus = "sending" } : null);
        if (claimed != MutationResult.Changed)
        {
            return claimed == MutationResult.Missing ? DeliveryResult.Missing : DeliveryResult.Conflict;
        }
        var body = new StringBuilder();
        foreach (var pair in new[] { ("Name", row.Entity.Name), ("Email", row.Entity.Email), ("Subject", row.Entity.Subject), ("Message", row.Entity.Message) }.Concat(fields.Select(x => (x.Label, x.Value))))
        {
            body.Append("<p><strong>").Append(WebUtility.HtmlEncode(pair.Item1)).Append(":</strong> ").Append(WebUtility.HtmlEncode(pair.Item2)).Append("</p>");
        }
        var success = false;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await sender.SendEmailAsync(row.Entity.Recipient, row.Entity.Subject, body.ToString(), timeout.Token).WaitAsync(timeout.Token);
            success = true;
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Contact delivery timed out for request {RequestId}; delivery is uncertain", id);
            return DeliveryResult.Uncertain;
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Contact delivery timed out for request {RequestId}; delivery is uncertain", id);
            return DeliveryResult.Uncertain;
        }
        catch (Exception exception) when (exception is SmtpException or IOException or SocketException or HttpRequestException or InvalidOperationException or NotSupportedException or OperationCanceledException or FormatException or ArgumentException)
        {
            logger.LogWarning(exception, "Contact delivery failed for request {RequestId}", id);
        }
        try
        {
            var recorded = await Change(id, context, commands, current => current.DeliveryStatus == "sending" ? current with { DeliveryStatus = success ? "sent" : "failed" } : null);
            if (recorded != MutationResult.Changed)
            {
                logger.LogError("Could not record delivery outcome for contact request {RequestId}: {Result}", id, recorded);
                return DeliveryResult.Uncertain;
            }
        }
        catch (Exception exception) when (exception is System.Data.Common.DbException or DbUpdateException)
        {
            logger.LogError(exception, "Could not record delivery outcome for contact request {RequestId}", id);
            context.Context.ChangeTracker.Clear();
            return DeliveryResult.Uncertain;
        }
        return success ? DeliveryResult.Sent : DeliveryResult.Failed;
    }
}
