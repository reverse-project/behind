# Private messaging and reporting

Open **Private messages** in the account menu. The inbox, read, sent and deleted folders are paged. Opening a conversation and marking it read changes only your mailbox; deletion moves it to your deleted folder without removing other participants' messages. Restore returns it to your mailbox.

Use the pill navigation to switch folders, compose a message or manage blocked users. The selected view is highlighted; BBCode and visual editor modes use the same pill style.

Composition and replies open in the visual editor by default. BBCode source editing is available as the secondary mode, without changing the stored message format.

Use **Send private message** on another user's public profile to open composition with that user selected. The button requires private-message read/write permissions and is not offered for your own profile or built-in accounts.

Compose a conversation with a subject, BBCode body and at least one recipient. All chats are groups, including existing two-person chats; there is no group toggle. Only administrators with **PrivateMessageModeration.Manage** can address user roles, including hidden roles and role invitations. A role grants its current members access to the conversation, including earlier messages; leaving the role removes that access. Hidden recipients are not disclosed to other recipients. Their replies are private to the original sender rather than revealing their participation to the group.

Conversations start with at least two participants (sender and one recipient) and support owner-managed invitations and leaving/rejoining. Uninvited outsiders cannot join. Use the block list to prevent ordinary direct messages in either direction. System notices are exempt from blocking. A reply to a system notice is visible only to its author, the original sender and administrators with the system-message moderation grant.

Use **Report** on a profile, forum post, public chat message, news comment or private message. Provide a reason; a general report can identify several users. The server checks access to the source and preserves its content as evidence. Reporting anonymous content does not reveal the author's identity. A report opens a private conversation with report moderators, not the reported users.

Administrators with **Report.Manage** use the Reports page to review evidence and conversations. They can dismiss a report, resolve it or ban the reported users permanently or until a selected date. Optional notifications send a private reply to the reporter and an email to reported users with confirmed email addresses. Delivery failures remain visible; only pending or failed deliveries can be retried. An interrupted delivery marked sending has an uncertain outcome and is not automatically repeated.

Report deletion is soft deletion. Developers can list deleted reports, restore them or permanently remove them. Reviewing a report does not give administrators access to unrelated private conversations.

The blocked-user list has its own pager. Resolved and dismissed report details show the handling administrator and link to their public profile.

Only human accounts can be report targets. Target browsing and search exclude the built-in System and Guest accounts.

Private-message recipient browsing and search also exclude System, Guest and your own account, including when inviting participants.

Recipient searches show at most 10 users and, for authorized administrators, 10 roles. Refine the search to find other recipients.

New messages and replies email recipients at their confirmed addresses with a direct link to the conversation. Emails do not include subjects, message bodies or participant identities. Hidden recipients and current role members receive notifications only for messages they can see; the message author and departed participants are excluded. Reporter replies from moderators also notify the reporter. Membership is checked again before delivery.

Notifications are saved with the message and processed every minute. Known SMTP rejections retry up to three attempts using the original confirmed address. Interrupted or uncertain sends are not automatically repeated. Administrators can inspect delivery states in Raw database; email errors are logged. Configure **Frontend:Url** and the email provider before enabling delivery. The Logger provider logs notifications rather than sending email.

Report bans publish only the generic Misconduct reason, translated into the viewer's interface language. Custom ban reasons remain as written; private report reasons are not published.
