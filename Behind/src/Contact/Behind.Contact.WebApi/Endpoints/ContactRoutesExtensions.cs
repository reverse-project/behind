using Behind.Common.WebApi;
using Behind.Contact.Common;
using Behind.Contact.Database.Commands;
using Behind.Contact.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Behind.Contact.WebApi.Endpoints;

public static class ContactRoutesExtensions
{
    extension(IEndpointRouteBuilder application)
    {
        public void MapContactEndpoints()
        {
            application.MapGet("/contact/forms", ContactEndpoints.Forms).WithSummary("List public contact forms").Produces<ContactFormResponse[]>();
            application.MapGet("/contact/forms/{id:guid}", ContactEndpoints.Form).WithSummary("Get a public contact form").Produces<ContactFormResponse>().Produces(404);
            application.MapPost("/contact/forms/{id:guid}/requests", ContactEndpoints.Submit).RequireRateLimiting(ContactWebApiExtensions.SubmissionPolicy).WithSummary("Submit a contact request").Produces<ContactReceipt>(201).Produces(400).Produces(404).Produces(429);
            application.MapGet("/contact/manage/forms", ContactEndpoints.ManageForms).RequireAuthorization(ContactModuleNames.ReadGrant).WithSummary("List managed contact forms").Produces<ManagedContactForm[]>().Produces(403).Produces(401).Produces(403);
            application.MapGet("/contact/manage/forms/{id:guid}", ContactEndpoints.ManageForm).RequireAuthorization(ContactModuleNames.ReadGrant).WithSummary("Get a managed contact form").Produces<ManagedContactForm>().Produces(403).Produces(404).Produces(401).Produces(403);
            application.MapPost("/contact/manage/forms", ContactEndpoints.CreateForm).RequireAuthorization(ContactModuleNames.WriteGrant).WithSummary("Create a contact form").Produces<VersionedSavedResponse>().Produces(400).Produces(409).Produces(401).Produces(403);
            application.MapPost("/contact/manage/forms/{id:guid}", ContactEndpoints.UpdateForm).RequireAuthorization(ContactModuleNames.WriteGrant).WithSummary("Update a contact form").Produces<VersionedSavedResponse>().Produces(400).Produces(404).Produces(409).Produces(401).Produces(403);
            application.MapDelete("/contact/manage/forms/{id:guid}", async (Guid id, ContactFormCommands commands, HttpContext http) => (await commands.Delete(id, http.RequestAborted)).ToNoContentResult()).RequireAuthorization(ContactModuleNames.DeleteGrant).WithSummary("Delete a contact form").Produces(204).Produces(404).Produces(401).Produces(403);
            application.MapPost("/contact/manage/forms/{id:guid}/restore", async (Guid id, ContactFormCommands commands, HttpContext http) => (await commands.Restore(id, http.RequestAborted)).ToNoContentResult()).RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant).WithSummary("Restore a contact form").Produces(204).Produces(404).Produces(401).Produces(403);
            application.MapDelete("/contact/manage/forms/{id:guid}/hard", async (Guid id, ContactFormCommands commands, HttpContext http) => (await commands.HardDelete(id, http.RequestAborted)).ToResult()).RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant).WithSummary("Permanently delete a contact form").Produces(204).Produces(404).Produces(409).Produces(401).Produces(403);
            application.MapGet("/contact/manage/requests", ContactEndpoints.ManageRequests).RequireAuthorization(ContactModuleNames.ReadGrant).WithSummary("Search and page contact requests").Produces<PageResponse<ContactRequestResponse>>().Produces(400).Produces(401).Produces(403);
            application.MapGet("/contact/manage/requests/{id:guid}", ContactEndpoints.ManageRequest).RequireAuthorization(ContactModuleNames.ReadGrant).WithSummary("Get a contact request").Produces<ContactRequestResponse>().Produces(403).Produces(404).Produces(401).Produces(403);
            application.MapPost("/contact/manage/requests/{id:guid}/read", ContactEndpoints.Read).RequireAuthorization(ContactModuleNames.WriteGrant).WithSummary("Set contact request read state").Produces(204).Produces(404).Produces(409).Produces(401).Produces(403);
            application.MapPost("/contact/manage/requests/{id:guid}/retry", ContactEndpoints.Retry).RequireAuthorization(ContactModuleNames.WriteGrant).WithSummary("Retry failed contact delivery").Produces(204).Produces(404).Produces(409).Produces(503).Produces(401).Produces(403);
            application.MapDelete("/contact/manage/requests/{id:guid}", (Guid id, IContactContext context, ContactRequestCommands commands) => ContactEndpoints.DeleteRequest(id, context, commands, 0)).RequireAuthorization(ContactModuleNames.DeleteGrant).WithSummary("Delete a contact request").Produces(204).Produces(404).Produces(401).Produces(403);
            application.MapPost("/contact/manage/requests/{id:guid}/restore", (Guid id, IContactContext context, ContactRequestCommands commands) => ContactEndpoints.DeleteRequest(id, context, commands, 1)).RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant).WithSummary("Restore a contact request").Produces(204).Produces(404).Produces(401).Produces(403);
            application.MapDelete("/contact/manage/requests/{id:guid}/hard", (Guid id, IContactContext context, ContactRequestCommands commands) => ContactEndpoints.DeleteRequest(id, context, commands, 2)).RequireAuthorization(ManagedListExtensions.DeletedRecordsGrant).WithSummary("Permanently delete a contact request").Produces(204).Produces(404).Produces(409).Produces(401).Produces(403);
        }
    }
}
