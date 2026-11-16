# Contact forms and inbox

Contact is available when the Contact module is installed. The main Behind installation includes it; existing independent site installations retain their own module selection.

## Sending a request

Open `#/contact` or use a Contact form widget placed in a page layout. Select an active form and fill in your name, email address, subject, message and any additional fields. A form may offer fixed subject choices or allow your own subject. Required fields must be completed.

The application saves your request in the administrator inbox. If the form has an email recipient, it then attempts email delivery. If delivery fails, the saved request remains available to administrators for retry. Do not resubmit an accepted request just because email delivery is pending or failed.

## Configuring forms

Open **Administration > Contact** (`#/administration/contact`) and use the forms management section. Create as many forms as needed and configure each one's:

- Display name and description.
- Optional destination email address, visible only to authorized administrators. Leave it empty to use the administrator inbox without sending email.
- Fixed subject choices or a free-text subject.
- Extra field keys, labels, text/textarea/email types, required flags and maximum lengths.

Deleted forms do not accept public requests. Editing a form does not change already-submitted requests: their original form name, field labels, values and destination are preserved.

Email delivery is optional. For forms with a destination address, configure an email delivery provider under **Administration > Settings > Email**. The Logger provider marks submissions as sent after logging, but does not send email to a mailbox; it logs the message body only in development. Provider acceptance does not confirm that the recipient read the message.

## Layout widget

Add the **Contact form** widget in the existing layout editor. The widget uses the same active forms and validation as the contact page and inherits the installation's Bootstrap skin. Only installed module widgets appear in the layout editor.

## Administrator inbox

The inbox lists saved requests, their creation time and their delivery/read state. Search by form name, sender name or email, subject or message; use the page controls to navigate results. Turn on **Hide read** to show only unread requests; by default all requests are shown. These controls also apply when listing deleted requests. Open a request to inspect all submitted data. Explicitly mark it read when handled; this shared state records which administrator marked it read, and the details link their display name to their profile. Viewing a request is not itself a read-marking action.

Failed deliveries can be retried by authorized administrators. A retry uses the original saved destination and submitted data, even if the form was subsequently edited. Inbox-only requests have `notRequested` delivery status and cannot be retried, even if a recipient is later added to the form. Successfully delivered messages cannot be retried as though they had failed.

The `sending` state protects against duplicate concurrent deliveries. If the application stops during delivery, this state may remain because the delivery outcome is unknown. Check the destination mailbox before taking further action; it is not automatically retried. The record can still be deleted.

Forms and requests support soft deletion. Developers with `RawDatabase.Manage` may list deleted records, restore them or delete them permanently. Deleting an inbox item never deletes an email that has already reached a mailbox. Keep only the personal data your installation needs and include the shared database in backups.
