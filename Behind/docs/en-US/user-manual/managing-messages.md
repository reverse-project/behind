# Messages

A **message** is a short notice, for example a maintenance warning or an announcement, that you place into a [layout](./managing-sites.md) as a widget. Every message has a level, a title, rich content and an optional schedule, so it appears and disappears on its own.

You find the tool under **Administration > Messages** (`#/manage/messages`).

| Action | You need |
|--------|----------|
| See the messages | the `Message.Read` grant |
| Create and edit messages | the `Message.Write` grant |
| Delete messages | the `Message.Delete` grant |
| See, restore and permanently delete deleted messages | the `RawDatabase.Manage` grant (developers) |

Administrators have the first three grants by default. Users do not.

## Message fields

- the **name**, shown in the layout editor so you can pick the message. Visitors never see it,
- the **level**: **Info**, **Success**, **Warning** or **Danger**. The level sets the color of the notice,
- the optional **title**, shown as the heading of the notice,
- the **content**, written in the rich text editor. Scripts and unsafe HTML are removed when saving,
- **Visible from** and **Visible until**, both optional. Times are entered in your local time. Leave **Visible from** empty to show the message right away and **Visible until** empty to show it until it is deleted. The end must be later than the start.

The list has a **Search** box (it looks at the name and the title), **letter buttons** for the first letter of the name and page buttons.

## Showing a message on the site

1. Open **Administration > Layouts**.
2. Add a widget to a section and choose the **Message** widget type.
3. Choose the message in the **Message** list and save the layouts.

The notice is shown only while the message is scheduled to be live and not deleted; otherwise the widget shows nothing. Pages that are already open follow the schedule within about a minute, without a reload.

Visitors can dismiss a notice with its close button. The browser remembers it, so the notice stays hidden for that visitor until you edit the message: saving a change shows it again.

## Deleting and restoring

- **Delete** soft deletes a message. The notice disappears from the site. Nothing is lost.
- Developers see a **Show deleted** switch. A deleted entry shows **Restore** and **Delete permanently**.

Everything you change here is recorded in the [audit log](./audit-log.md) with the message name instead of identifiers.
