# Elevating Your Own Role

Some accounts may become an **Administrator** or **Developer** themselves by proving they hold the authenticator secret configured for that role. This is only available when the installation has elevation enabled.

1. Open **Session** from your name menu.
2. In the elevation form, choose the role in the drop-down. Roles you already hold are not offered.
3. Enter the 6 to 8 digit code from your authenticator app.
4. Choose **Elevate**.

You are told "Your role was elevated." Refresh the page to see the new role and grants.

Messages you may see:

| Message | Meaning |
|---------|---------|
| Enter the 6 to 8 digit authenticator code | the code was empty or malformed |
| The code is not valid | the code did not match |
| Administrator elevation is not available | elevation is switched off for this installation |
| Too many attempts | wait a while before trying again |

Elevation attempts are rate limited. The secrets are set by whoever runs the installation; see the developer documentation for configuration.
