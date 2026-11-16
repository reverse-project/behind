using System.Data;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public sealed class PrivateMessageStore(AbstractCoreContext context) : IReportConversations
{
    public const int MaxRecipients = 100;
    public sealed record Participant(Guid Id, string DisplayName, bool IsRole);
    public sealed record Message(Guid Id, Guid AuthorId, string Author, string Content, Guid? ReplyToId, DateTimeOffset? CreatedAt);
    public sealed record Conversation(
        Guid Id, string Subject, bool IsGroup, bool IsSystem, bool IsReport, bool IsRead, bool CanReply,
        bool CanManage, DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt,
        IReadOnlyList<Participant> Participants, IReadOnlyList<Message> Messages);
    public sealed record Listing(
        Guid Id, string Subject, bool IsGroup, bool IsSystem, bool IsReport, bool IsRead,
        DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);
    public sealed record Page(IReadOnlyList<Listing> Items, int Total, int PageNumber, int PageSize, int PageCount);
    public sealed record RecipientLookup(IReadOnlyList<Participant> Users, IReadOnlyList<Participant> Roles);
    public sealed record Evidence(Guid AuthorId, string Content);
    public sealed record Result(bool Succeeded, Guid? Id = null, string? Error = null)
    {
        public static Result Failure(string error) => new(false, null, error);
        public static Result Success(Guid? id = null) => new(true, id);
    }

    private sealed record ConversationRow(Guid Id, long Version, Guid SenderId, string Subject, bool IsGroup, bool IsSystem, bool IsReport);
    private sealed record RecipientRow(Guid ConversationId, Guid RecipientId, bool IsRole, bool IsHidden);
    private sealed record MessageRow(Guid Id, long Version, Guid ConversationId, Guid AuthorId, string Content, Guid? ReplyToId, Guid? PrivateRecipientId, bool IsInitial);
    private sealed record MessageTime(DateTimeOffset? CreatedAt, DateTimeOffset? UpdatedAt);
    private sealed record MailboxRow(Guid Id, long Version, Guid ConversationId, Guid UserId, bool Deleted, bool Left);
    private sealed record BlockRow(Guid Id, long Version, Guid UserId, Guid BlockedUserId, bool Active);
    private sealed record Snapshot(
        ConversationRow Conversation, IReadOnlyList<RecipientRow> Recipients, IReadOnlyList<MessageRow> Messages,
        MailboxRow? Mailbox, IReadOnlySet<Guid> ReadIds, PrivateMessagingViewer Viewer);

    public async Task<Page> List(
        Guid viewer, string folder, int page = 1, int pageSize = 20, bool systemModerator = false,
        bool reportModerator = false, CancellationToken cancellationToken = default)
    {
        if (folder is not ("inbox" or "read" or "sent" or "deleted" or "left") || page < 1 || pageSize is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(folder));
        }

        var snapshots = await Load(viewer, systemModerator, reportModerator, null, cancellationToken, folder == "left");
        if (folder == "left")
        {
            var left = snapshots.Where(x => x.Mailbox?.Left == true && x.Conversation.IsGroup
                    && !x.Conversation.IsSystem && !x.Conversation.IsReport)
                .OrderBy(x => x.Conversation.Id)
                .Select(x => new Listing(x.Conversation.Id, x.Conversation.Subject, true, false, false, false, null, null))
                .ToArray();
            return new Page(left.Skip((int)Math.Min((long)(page - 1) * pageSize, left.Length)).Take(pageSize).ToArray(),
                left.Length, page, pageSize, (int)(((long)left.Length + pageSize - 1) / pageSize));
        }

        var visible = snapshots.Select(snapshot => (Snapshot: snapshot, Messages: VisibleMessages(snapshot))).ToList();
        var times = await MessageTimes(visible.SelectMany(x => x.Messages), cancellationToken);
        var result = visible.Where(x => InFolder(x.Snapshot, x.Messages, folder))
            .Select(x => ToListing(x.Snapshot, x.Messages, times))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();
        var total = result.Count;
        var items = result.Skip((int)Math.Min((long)(page - 1) * pageSize, total)).Take(pageSize).ToArray();
        return new Page(items, total, page, pageSize, (int)(((long)total + pageSize - 1) / pageSize));
    }

    public async Task<Conversation?> Find(
        Guid viewer, Guid id, bool systemModerator = false, bool reportModerator = false,
        CancellationToken cancellationToken = default)
    {
        var snapshot = (await Load(viewer, systemModerator, reportModerator, id, cancellationToken)).SingleOrDefault();
        if (snapshot is null)
        {
            return null;
        }

        var messages = VisibleMessages(snapshot);
        var times = await MessageTimes(messages, cancellationToken);
        var listing = ToListing(snapshot, messages, times);
        var userIds = messages.Select(x => x.AuthorId)
            .Concat(snapshot.Recipients.Where(x => !x.IsRole && !x.IsHidden).Select(x => x.RecipientId))
            .Append(snapshot.Conversation.SenderId).Distinct().ToArray();
        var roleIds = snapshot.Recipients.Where(x => x.IsRole && !x.IsHidden).Select(x => x.RecipientId).ToArray();
        var users = await Users(userIds, cancellationToken);
        var roles = await Roles(roleIds, cancellationToken);
        var participants = snapshot.Recipients.Where(x => !x.IsHidden)
            .Select(x => new Participant(x.RecipientId,
                x.IsRole ? roles.GetValueOrDefault(x.RecipientId, "Unknown") : users.GetValueOrDefault(x.RecipientId, x.RecipientId.ToUserLabel(null)),
                x.IsRole))
            .Prepend(new Participant(snapshot.Conversation.SenderId,
                users.GetValueOrDefault(snapshot.Conversation.SenderId, snapshot.Conversation.SenderId.ToUserLabel(null)), false))
            .DistinctBy(x => (x.Id, x.IsRole)).ToArray();
        var output = messages.OrderBy(x => x.IsInitial ? 0 : 1)
            .ThenBy(x => times.GetValueOrDefault(x.Id)?.CreatedAt).ThenBy(x => x.Id)
            .Select(x => new Message(x.Id, x.AuthorId, users.GetValueOrDefault(x.AuthorId, x.AuthorId.ToUserLabel(null)),
                x.Content, x.ReplyToId, times.GetValueOrDefault(x.Id)?.CreatedAt)).ToArray();
        var canReply = CanParticipate(snapshot) && (!snapshot.Conversation.IsSystem || viewer != snapshot.Conversation.SenderId
            || messages.Any(x => x.PrivateRecipientId is not null));
        return new Conversation(id, listing.Subject, listing.IsGroup, listing.IsSystem, listing.IsReport, listing.IsRead,
            canReply, snapshot.Conversation.SenderId == viewer || snapshot.Conversation.IsSystem && systemModerator
            || snapshot.Conversation.IsReport && reportModerator, listing.CreatedAt, listing.UpdatedAt, participants, output);
    }

    public async Task<Result> Create(
        Guid viewer, string subject, string content, IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<Guid> roleIds,
        IReadOnlyCollection<Guid> bccUserIds, IReadOnlyCollection<Guid> bccRoleIds, bool isGroup, bool isSystem,
        bool systemModerator = false, bool isReport = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 200 || !ValidContent(content)
            || isSystem && !systemModerator || isSystem && isReport)
        {
            return Result.Failure("invalid");
        }

        var publicUsers = userIds.Distinct().Where(x => x != viewer).ToArray();
        var publicRoles = roleIds.Distinct().ToArray();
        var hiddenUsers = bccUserIds.Distinct().Where(x => x != viewer && !publicUsers.Contains(x)).ToArray();
        var hiddenRoles = bccRoleIds.Distinct().Where(x => !publicRoles.Contains(x)).ToArray();
        var allUsers = publicUsers.Concat(hiddenUsers).ToArray();
        var allRoles = publicRoles.Concat(hiddenRoles).ToArray();
        if (allRoles.Length > 0 && !systemModerator)
        {
            return Result.Failure("forbidden");
        }

        if (userIds.Intersect(bccUserIds).Any() || roleIds.Intersect(bccRoleIds).Any()
            || allUsers.Length + allRoles.Length == 0 || allUsers.Length + allRoles.Length > MaxRecipients)
        {
            return Result.Failure("invalid-recipients");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var existingUsers = await Users(allUsers.Append(viewer).Distinct().ToArray(), cancellationToken);
        var existingRoles = await Roles(allRoles, cancellationToken);
        if (existingUsers.Count != allUsers.Length + 1 || existingRoles.Count != allRoles.Length)
        {
            return Result.Failure("invalid-recipients");
        }

        if (allUsers.Length == 0 && !await (
                from membership in context.Set<Dictionary<string, object>>("UserRoles")
                join user in context.Users on EF.Property<Guid>(membership, "UserId") equals EF.Property<Guid>(user, "Id")
                where allRoles.Contains(EF.Property<Guid>(membership, "RoleId"))
                      && EF.Property<Guid>(membership, "UserId") != viewer
                select user).AnyAsync(cancellationToken))
        {
            return Result.Failure("invalid-recipients");
        }

        if (!isSystem && await AnyBlocked(viewer, allUsers, cancellationToken))
        {
            return Result.Failure("blocked");
        }

        var id = Guid.NewGuid();
        await Add(new PrivateConversation(new(id, 1), viewer, subject.Trim(), true, isSystem, isReport), id, 1, cancellationToken);
        foreach (var recipient in publicUsers.Select(x => (x, false, false))
                     .Concat(publicRoles.Select(x => (x, true, false)))
                     .Concat(hiddenUsers.Select(x => (x, false, true)))
                     .Concat(hiddenRoles.Select(x => (x, true, true))))
        {
            var recipientId = Guid.NewGuid();
            await Add(new PrivateConversationRecipient(new(recipientId, 1), id, recipient.x, recipient.Item2, recipient.Item3),
                recipientId, 1, cancellationToken);
        }

        var messageId = Guid.NewGuid();
        await Add(new PrivateMessage(new(messageId, 1), id, viewer, content, null, null, true), messageId, 1, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await new PrivateMessageEmailStore(context).Enqueue(messageId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(id);
    }

    public async Task<Result> Invite(
        Guid viewer, Guid id, IReadOnlyCollection<Guid> userIds, IReadOnlyCollection<Guid> roleIds,
        IReadOnlyCollection<Guid> bccUserIds, IReadOnlyCollection<Guid> bccRoleIds,
        CancellationToken cancellationToken = default, bool systemModerator = false)
    {
        if ((roleIds.Count > 0 || bccRoleIds.Count > 0) && !systemModerator)
        {
            return Result.Failure("forbidden");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var snapshot = (await Load(viewer, false, false, id, cancellationToken)).SingleOrDefault();
        if (snapshot is null || snapshot.Conversation.SenderId != viewer || !snapshot.Conversation.IsGroup
            || snapshot.Conversation.IsSystem || snapshot.Conversation.IsReport)
        {
            return Result.Failure("not-found");
        }

        var requested = userIds.Distinct().Where(x => x != viewer).Select(x => new RecipientRow(id, x, false, false))
            .Concat(roleIds.Distinct().Select(x => new RecipientRow(id, x, true, false)))
            .Concat(bccUserIds.Distinct().Where(x => x != viewer).Select(x => new RecipientRow(id, x, false, true)))
            .Concat(bccRoleIds.Distinct().Select(x => new RecipientRow(id, x, true, true))).ToArray();
        if (requested.Length == 0 || requested.GroupBy(x => (x.RecipientId, x.IsRole)).Any(x => x.Count() > 1)
            || requested.Any(x => snapshot.Recipients.Any(existing => existing.RecipientId == x.RecipientId
                && existing.IsRole == x.IsRole && existing.IsHidden != x.IsHidden)))
        {
            return Result.Failure("invalid-recipients");
        }

        var additions = requested.Where(x => !snapshot.Recipients.Any(existing => existing.RecipientId == x.RecipientId
            && existing.IsRole == x.IsRole)).ToArray();
        if (snapshot.Recipients.Count + additions.Length > MaxRecipients)
        {
            return Result.Failure("invalid-recipients");
        }

        var users = additions.Where(x => !x.IsRole).Select(x => x.RecipientId).ToArray();
        var roles = additions.Where(x => x.IsRole).Select(x => x.RecipientId).ToArray();
        if ((await Users(users, cancellationToken)).Count != users.Length
            || (await Roles(roles, cancellationToken)).Count != roles.Length)
        {
            return Result.Failure("invalid-recipients");
        }

        if (await AnyBlocked(viewer, users, cancellationToken))
        {
            return Result.Failure("blocked");
        }

        foreach (var recipient in additions)
        {
            var recipientId = Guid.NewGuid();
            await Add(new PrivateConversationRecipient(new(recipientId, 1), id, recipient.RecipientId,
                recipient.IsRole, recipient.IsHidden), recipientId, 1, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Guid> CreateReport(
        Guid viewer, string subject, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 200 || !ValidContent(content)
            || (await Users([viewer], cancellationToken)).Count != 1)
        {
            throw new ArgumentException("Invalid report conversation.");
        }

        var id = Guid.NewGuid();
        await Add(new PrivateConversation(new(id, 1), viewer, subject.Trim(), false, false, true), id, 1, cancellationToken);
        var messageId = Guid.NewGuid();
        await Add(new PrivateMessage(new(messageId, 1), id, viewer, content, null, null, true), messageId, 1, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return id;
    }

    public async Task AppendReportMessage(
        Guid conversationId, Guid authorId, string content, CancellationToken cancellationToken = default)
    {
        if (!ValidContent(content)
            || !await context.PrivateConversations.LatestOf(context.PrivateConversations).OfKey(conversationId)
                .AnyAsync(x => x.IsReport, cancellationToken)
            || (await Users([authorId], cancellationToken)).Count != 1)
        {
            throw new ArgumentException("Invalid report message.");
        }

        var id = Guid.NewGuid();
        await Add(new PrivateMessage(new(id, 1), conversationId, authorId, content, null, null, false),
            id, 1, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await new PrivateMessageEmailStore(context).Enqueue(id, cancellationToken);
    }

    public async Task DeleteReportConversation(Guid conversationId, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();
        if (!await context.PrivateConversations.LatestOf(context.PrivateConversations).OfKey(conversationId)
                .AnyAsync(x => x.IsReport, cancellationToken))
        {
            return;
        }

        var conversations = await context.PrivateConversations.OfKey(conversationId).ToListAsync(cancellationToken);
        var messages = await context.PrivateMessages.Where(x => x.ConversationId == conversationId).ToListAsync(cancellationToken);
        var messageIds = messages.Select(x => context.Entry(x).VersionedId.Key).Distinct().ToArray();
        var reads = await context.PrivateMessageReads.Where(x => messageIds.Contains(x.MessageId)).ToListAsync(cancellationToken);
        var recipients = await context.PrivateConversationRecipients.Where(x => x.ConversationId == conversationId).ToListAsync(cancellationToken);
        var mailboxes = await context.PrivateMailboxes.Where(x => x.ConversationId == conversationId).ToListAsync(cancellationToken);
        var notifications = await context.Set<PrivateMessageEmailDelivery>()
            .Where(delivery => delivery.ConversationId == conversationId).ToListAsync(cancellationToken);
        foreach (var notification in notifications)
        {
            context.HardDelete(notification);
        }
        foreach (var read in reads)
        {
            context.HardDelete(read);
        }
        foreach (var message in messages)
        {
            context.HardDelete(message);
        }
        foreach (var recipient in recipients)
        {
            context.HardDelete(recipient);
        }
        foreach (var mailbox in mailboxes)
        {
            context.HardDelete(mailbox);
        }
        foreach (var conversation in conversations)
        {
            context.HardDelete(conversation);
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Evidence?> FindEvidence(
        Guid viewer, Guid messageId, bool systemModerator = false, bool reportModerator = false,
        CancellationToken cancellationToken = default)
    {
        var conversationId = await context.PrivateMessages.LatestOf(context.PrivateMessages).AsNoTracking().OfKey(messageId)
            .Select(x => (Guid?)x.ConversationId).SingleOrDefaultAsync(cancellationToken);
        if (conversationId is null)
        {
            return null;
        }

        var snapshot = (await Load(viewer, systemModerator, reportModerator, conversationId, cancellationToken)).SingleOrDefault();
        var message = snapshot is null ? null : VisibleMessages(snapshot).SingleOrDefault(x => x.Id == messageId);
        return message is null ? null : new Evidence(message.AuthorId, message.Content);
    }

    public async Task<Result> Reply(
        Guid viewer, Guid id, string content, Guid? replyToId = null, bool systemModerator = false,
        bool reportModerator = false, CancellationToken cancellationToken = default)
    {
        if (!ValidContent(content))
        {
            return Result.Failure("invalid");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var snapshot = (await Load(viewer, systemModerator, reportModerator, id, cancellationToken)).SingleOrDefault();
        if (snapshot is null || !CanParticipate(snapshot))
        {
            return Result.Failure("not-found");
        }

        var source = replyToId is null ? null : snapshot.Messages.SingleOrDefault(x => x.Id == replyToId);
        if (replyToId is not null && source is null)
        {
            return Result.Failure("invalid-reply");
        }

        var conversation = ToPolicy(snapshot.Conversation);
        var recipients = ToPolicy(snapshot.Recipients);
        var target = source is null ? null : ToPolicy(source);
        if (!PrivateMessagingVisibility.TryGetReplyRecipient(conversation, recipients, snapshot.Viewer, target, out var audience))
        {
            return Result.Failure("invalid-reply");
        }

        if (!snapshot.Conversation.IsSystem && snapshot.Recipients.Count == 1 && !snapshot.Recipients[0].IsRole)
        {
            var other = snapshot.Conversation.SenderId == viewer
                ? snapshot.Recipients.Single(x => !x.IsRole).RecipientId : snapshot.Conversation.SenderId;
            if (await IsBlocked(viewer, other, cancellationToken))
            {
                return Result.Failure("blocked");
            }
        }

        var messageId = Guid.NewGuid();
        await Add(new PrivateMessage(new(messageId, 1), id, viewer, content, replyToId, audience, false),
            messageId, 1, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await new PrivateMessageEmailStore(context).Enqueue(messageId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success(messageId);
    }

    public async Task<Result> SetMailbox(
        Guid viewer, Guid id, string action, CancellationToken cancellationToken = default,
        bool systemModerator = false, bool reportModerator = false)
    {
        if (action is not ("read" or "delete" or "restore"))
        {
            return Result.Failure("invalid");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var snapshot = (await Load(viewer, systemModerator, reportModerator, id, cancellationToken)).SingleOrDefault();
        if (snapshot is null)
        {
            return Result.Failure("not-found");
        }

        if (action == "read")
        {
            foreach (var message in VisibleMessages(snapshot).Where(x => x.AuthorId != viewer && !snapshot.ReadIds.Contains(x.Id)))
            {
                var readId = Guid.NewGuid();
                await Add(new PrivateMessageRead(new(readId, 1), message.Id, viewer), readId, 1, cancellationToken);
            }
        }
        else
        {
            await SaveMailbox(snapshot, action == "delete", snapshot.Mailbox?.Left ?? false, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public Task<Result> Join(Guid viewer, Guid id, CancellationToken cancellationToken = default) =>
        Membership(viewer, id, false, cancellationToken);

    public Task<Result> Leave(Guid viewer, Guid id, CancellationToken cancellationToken = default) =>
        Membership(viewer, id, true, cancellationToken);

    private async Task<Result> Membership(Guid viewer, Guid id, bool left, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var snapshot = (await Load(viewer, false, false, id, cancellationToken, includeLeft: true)).SingleOrDefault();
        if (snapshot is null || !snapshot.Conversation.IsGroup || snapshot.Conversation.IsSystem || snapshot.Conversation.IsReport)
        {
            return Result.Failure("not-found");
        }

        await SaveMailbox(snapshot, snapshot.Mailbox?.Deleted ?? false, left, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<RecipientLookup> Recipients(
        string? search, CancellationToken cancellationToken = default, bool reportTargets = false, Guid? viewer = null)
    {
        var term = (search ?? "").Trim().ToLowerInvariant();
        var query = context.Users.AsNoTracking().Where(user =>
            EF.Property<Guid>(user, "Id") != DomainConstants.SystemUserId
            && EF.Property<Guid>(user, "Id") != DomainConstants.GuestUserId
            && (viewer == null || EF.Property<Guid>(user, "Id") != viewer));

        var users = await query.Where(x => x.DisplayName.ToLower().Contains(term))
            .OrderBy(x => x.DisplayName).ThenBy(x => EF.Property<Guid>(x, "Id")).Take(10)
            .Select(x => new Participant(EF.Property<Guid>(x, "Id"), x.DisplayName, false)).ToListAsync(cancellationToken);
        var roles = reportTargets ? [] : await context.Roles.AsNoTracking().Where(x => x.Name.ToLower().Contains(term))
            .OrderBy(x => x.Name).ThenBy(x => EF.Property<Guid>(x, "Id")).Take(10)
            .Select(x => new Participant(EF.Property<Guid>(x, "Id"), x.Name, true)).ToListAsync(cancellationToken);
        return new RecipientLookup(users.Select(x => x with { DisplayName = x.Id.ToUserLabel(x.DisplayName) }).ToArray(), roles);
    }

    public async Task<IReadOnlyList<Participant>> Blocks(Guid viewer, CancellationToken cancellationToken = default)
    {
        var blocks = await BlockRows(cancellationToken);
        var ids = blocks.Where(x => x.UserId == viewer && x.Active).Select(x => x.BlockedUserId).ToArray();
        return (await Users(ids, cancellationToken)).Select(x => new Participant(x.Key, x.Value, false)).ToArray();
    }

    public async Task<Result> SetBlock(Guid viewer, Guid userId, bool active, CancellationToken cancellationToken = default)
    {
        if (viewer == userId)
        {
            return Result.Failure("invalid");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if ((await Users([viewer, userId], cancellationToken)).Count != 2)
        {
            return Result.Failure("not-found");
        }

        var current = (await BlockRows(cancellationToken)).SingleOrDefault(x => x.UserId == viewer && x.BlockedUserId == userId);
        if (current?.Active != active)
        {
            var id = current?.Id ?? Guid.NewGuid();
            var version = (current?.Version ?? 0) + 1;
            await Add(new PrivateUserBlock(new(id, version), viewer, userId, active), id, version, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<List<Snapshot>> Load(
        Guid viewer, bool systemModerator, bool reportModerator, Guid? id, CancellationToken cancellationToken,
        bool includeLeft = false)
    {
        var roles = await (from membership in context.Set<Dictionary<string, object>>("UserRoles")
                           join role in context.Roles on EF.Property<Guid>(membership, "RoleId") equals EF.Property<Guid>(role, "Id")
                           where EF.Property<Guid>(membership, "UserId") == viewer
                           select EF.Property<Guid>(role, "Id")).ToListAsync(cancellationToken);
        var policyViewer = new PrivateMessagingViewer(viewer, roles.ToHashSet(), systemModerator, reportModerator);
        var recipientConversationIds = context.PrivateConversationRecipients.LatestOf(context.PrivateConversationRecipients).AsNoTracking()
            .Where(x => !x.IsRole && x.RecipientId == viewer || x.IsRole && roles.Contains(x.RecipientId))
            .Select(x => x.ConversationId);
        var query = context.PrivateConversations.LatestOf(context.PrivateConversations).AsNoTracking().Where(x =>
            x.SenderId == viewer || x.IsSystem && systemModerator || x.IsReport && reportModerator
            || recipientConversationIds.Contains(EF.Property<Guid>(x, "Key")));
        if (id is not null)
        {
            query = query.OfKey(id.Value);
        }

        var conversations = await query.Select(x => new ConversationRow(
            EF.Property<Guid>(x, "Key"), EF.Property<long>(x, "Version"), x.SenderId, x.Subject, true, x.IsSystem, x.IsReport))
            .ToListAsync(cancellationToken);
        var conversationIds = conversations.Select(x => x.Id).ToArray();
        var allRecipients = await context.PrivateConversationRecipients.LatestOf(context.PrivateConversationRecipients).AsNoTracking()
            .Where(x => conversationIds.Contains(x.ConversationId))
            .Select(x => new RecipientRow(x.ConversationId, x.RecipientId, x.IsRole, x.IsHidden)).ToListAsync(cancellationToken);
        var mailboxes = await context.PrivateMailboxes.LatestOf(context.PrivateMailboxes).AsNoTracking()
            .Where(x => x.UserId == viewer && conversationIds.Contains(x.ConversationId))
            .Select(x => new MailboxRow(EF.Property<Guid>(x, "Key"), EF.Property<long>(x, "Version"),
                x.ConversationId, x.UserId, x.Deleted, x.Left)).ToListAsync(cancellationToken);
        var permitted = conversations.Where(c =>
        {
            var mailbox = mailboxes.SingleOrDefault(x => x.ConversationId == c.Id);
            var recipients = allRecipients.Where(x => x.ConversationId == c.Id).ToArray();
            return (includeLeft || mailbox?.Left != true)
                   && PrivateMessagingVisibility.CanReadConversation(ToPolicy(c), ToPolicy(recipients), policyViewer);
        }).ToList();
        var ids = permitted.Select(x => x.Id).ToArray();
        var messages = await context.PrivateMessages.LatestOf(context.PrivateMessages).AsNoTracking().Where(x => ids.Contains(x.ConversationId))
            .Select(x => new MessageRow(EF.Property<Guid>(x, "Key"), EF.Property<long>(x, "Version"), x.ConversationId, x.AuthorId, x.Content,
                x.ReplyToId, x.PrivateRecipientId, x.IsInitial)).ToListAsync(cancellationToken);
        var messageIds = messages.Select(x => x.Id).ToArray();
        var readIds = (await context.PrivateMessageReads.LatestOf(context.PrivateMessageReads).AsNoTracking()
            .Where(x => x.UserId == viewer && messageIds.Contains(x.MessageId))
            .Select(x => x.MessageId).ToListAsync(cancellationToken)).ToHashSet();
        return permitted.Select(c => new Snapshot(c, allRecipients.Where(x => x.ConversationId == c.Id).ToArray(),
            messages.Where(x => x.ConversationId == c.Id).ToArray(), mailboxes.SingleOrDefault(x => x.ConversationId == c.Id),
            readIds, policyViewer)).ToList();
    }

    private static IReadOnlyList<MessageRow> VisibleMessages(Snapshot snapshot) =>
        snapshot.Messages.Where(message => PrivateMessagingVisibility.CanReadMessage(
            ToPolicy(snapshot.Conversation), ToPolicy(snapshot.Recipients), ToPolicy(message), snapshot.Viewer)).ToArray();

    private static bool CanParticipate(Snapshot snapshot) =>
        snapshot.Viewer.UserId == snapshot.Conversation.SenderId
        || snapshot.Conversation.IsReport && snapshot.Viewer.CanModerateReports
        || snapshot.Recipients.Any(x => x.IsRole
            ? snapshot.Viewer.RoleIds.Contains(x.RecipientId)
            : x.RecipientId == snapshot.Viewer.UserId);

    private static bool InFolder(Snapshot snapshot, IReadOnlyList<MessageRow> messages, string folder)
    {
        var deleted = snapshot.Mailbox?.Deleted == true;
        var isRead = messages.All(x => x.AuthorId == snapshot.Viewer.UserId || snapshot.ReadIds.Contains(x.Id));
        return folder switch
        {
            "deleted" => deleted,
            "sent" => !deleted && messages.Any(x => x.AuthorId == snapshot.Viewer.UserId),
            "read" => !deleted && isRead && messages.Any(x => x.AuthorId != snapshot.Viewer.UserId),
            "inbox" => !deleted && !isRead,
            _ => false
        };
    }

    private static Listing ToListing(Snapshot snapshot, IReadOnlyList<MessageRow> messages, IReadOnlyDictionary<Guid, MessageTime> times)
    {
        var created = messages.Where(x => x.IsInitial).Select(x => times.GetValueOrDefault(x.Id)?.CreatedAt).FirstOrDefault();
        var updated = messages.Select(x => times.GetValueOrDefault(x.Id) is { } time
            ? time.UpdatedAt ?? time.CreatedAt : null).Max();
        return new Listing(snapshot.Conversation.Id, snapshot.Conversation.Subject, snapshot.Conversation.IsGroup,
            snapshot.Conversation.IsSystem, snapshot.Conversation.IsReport,
            messages.All(x => x.AuthorId == snapshot.Viewer.UserId || snapshot.ReadIds.Contains(x.Id)), created, updated);
    }

    private async Task<IReadOnlyDictionary<Guid, MessageTime>> MessageTimes(
        IEnumerable<MessageRow> messages, CancellationToken cancellationToken)
    {
        var versions = messages.DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.Version);
        var times = await context.AuditTimesAsync<PrivateMessage>(versions, cancellationToken);
        return times.ToDictionary(x => x.Key, x => new MessageTime(
            x.Value.CreatedAt.Match<DateTimeOffset?>(() => null, value => value),
            x.Value.UpdatedAt.Match<DateTimeOffset?>(() => null, value => value)));
    }

    private async Task SaveMailbox(Snapshot snapshot, bool deleted, bool left, CancellationToken cancellationToken)
    {
        var id = snapshot.Mailbox?.Id ?? Guid.NewGuid();
        var version = (snapshot.Mailbox?.Version ?? 0) + 1;
        await Add(new PrivateMailbox(new(id, version), snapshot.Conversation.Id, snapshot.Viewer.UserId, deleted, left),
            id, version, cancellationToken);
    }

    private async Task<bool> IsBlocked(Guid user, Guid other, CancellationToken cancellationToken) =>
        await AnyBlocked(user, [other], cancellationToken);

    private async Task<bool> AnyBlocked(Guid user, IReadOnlyCollection<Guid> others, CancellationToken cancellationToken)
    {
        if (others.Count == 0)
        {
            return false;
        }
        var blocks = await BlockRows(cancellationToken);
        return blocks.Any(x => x.Active
            && (x.UserId == user && others.Contains(x.BlockedUserId)
                || x.BlockedUserId == user && others.Contains(x.UserId)));
    }

    private Task<List<BlockRow>> BlockRows(CancellationToken cancellationToken) =>
        context.PrivateUserBlocks.LatestOf(context.PrivateUserBlocks).AsNoTracking()
            .Select(x => new BlockRow(EF.Property<Guid>(x, "Key"), EF.Property<long>(x, "Version"),
                x.UserId, x.BlockedUserId, x.Active)).ToListAsync(cancellationToken);

    private async Task<Dictionary<Guid, string>> Users(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await context.Users.AsNoTracking().Where(x => ids.Contains(EF.Property<Guid>(x, "Id")))
            .Select(x => new { Id = EF.Property<Guid>(x, "Id"), x.DisplayName }).ToListAsync(cancellationToken))
        .ToDictionary(x => x.Id, x => x.Id.ToUserLabel(x.DisplayName));

    private async Task<Dictionary<Guid, string>> Roles(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        (await context.Roles.AsNoTracking().Where(x => ids.Contains(EF.Property<Guid>(x, "Id")))
            .Select(x => new { Id = EF.Property<Guid>(x, "Id"), x.Name }).ToListAsync(cancellationToken))
        .ToDictionary(x => x.Id, x => x.Name);

    private async Task Add<TEntity>(TEntity entity, Guid id, long version, CancellationToken cancellationToken) where TEntity : class
    {
        await context.Set<TEntity>().AddAsync(entity, cancellationToken);
        context.Entry(entity).StampVersionedKey(id, version);
    }

    private static bool ValidContent(string? content) =>
        !string.IsNullOrWhiteSpace(content) && content.Length <= 100000;

    private static PrivateMessagingConversation ToPolicy(ConversationRow row) =>
        new(row.Id, row.SenderId, row.IsSystem, row.IsReport);

    private static PrivateMessagingRecipient[] ToPolicy(IEnumerable<RecipientRow> rows) =>
        rows.Select(x => new PrivateMessagingRecipient(x.RecipientId, x.IsRole, x.IsHidden)).ToArray();

    private static PrivateMessagingMessage ToPolicy(MessageRow row) =>
        new(row.Id, row.ConversationId, row.AuthorId, row.PrivateRecipientId, row.IsInitial);
}
