# Your Account

Open these pages from your name in the navigation bar.

## Profile

Use **Profile** to change your display name and your password. Password changes are not available while you impersonate another user.

1. Type a new name (up to 500 characters). A stable short account tag is appended, for example `Alex#12ab34cd`. Leave it empty to use `user#12ab34cd`, never your email address.
2. Choose **Save**.

The line "Shown as ..." tells you how others will see you. Author names link to the public profile at `#/user/<user-id>`.

### Birthday and email privacy

Your birthday is optional and private. It is used to filter content by minimum age when the site's global age restriction setting is enabled; without a birthday, age-restricted content is then unavailable. Global age restrictions are disabled by default. Save your birthday in **Profile**.

**Hide my email on my public profile** is enabled by default. Clear it only if you want your primary email address to be public. This does not change authorized account-management access.

### Email addresses

Under **Profile > Email addresses**, add an address or edit an existing one and choose its **Save** button. Changes save independently of your other profile details. You can remove addresses, but at least one must remain. Email changes are unavailable while impersonating another user.

When `Security.EmailConfirmation` is enabled (the default), each new or changed address becomes unconfirmed. Follow the confirmation link sent to that exact address before using it to sign in. Other confirmed addresses remain usable. Use **Resend confirmation** if needed, then **Refresh** to update the status. When confirmation is disabled, new and changed addresses are confirmed immediately.

Any confirmed address works with your existing password or email sign-in links. Confirmation links cannot confirm a different or replaced address. Older links issued before per-address confirmation was introduced are invalid; request a fresh link from the sign-in page or Profile.

### Public profiles and anonymous comments

Choose **View public profile**, or follow a user's name from content, to see their public name, picture, roles, registration date, last login and any email address they chose to share. Administrators and moderators with the required permissions can ban or unban the user; authorized administrators can also impersonate them from this page. These actions follow the existing permission checks and audit rules.

Visitors with `User.Manage` additionally see the private birthday and email privacy setting, all current addresses with their confirmation times, effective and direct access, two-factor status counts, and retained bans including their reason, issuer, dates and status. Removed or expired bans are shown when their records remain available; permanently deleted records cannot be recovered here. Passwords, authentication secrets and recovery codes are never displayed.

When signed in, choose anonymous posting when adding a comment or forum message. Other readers do not see your profile or picture on that message. Authorized administrators and moderators can still identify the real author; anonymous posting is not anonymous to site staff.

### Profile picture

Choose a PNG, JPEG, GIF or WebP image up to 2 MiB under **Profile picture**; it is saved immediately. **Remove** clears it. Pictures are public and appear beside forum messages. See [Forum](./managing-forum.md).

### Language

Choose your **Language** from the list of available languages by their names (for example **English (International)** or **Magyar**) and choose **Save**. **Site default** follows the language the site is set to.

When you are signed in the choice is saved to your account and follows you to every device. Without signing in it is remembered in this browser only.

## Two-factor authentication

**Profile > Two-factor authentication** adds a second step to your sign in so that a stolen password or sign in link is not enough. It is optional unless the administrator requires it for everybody. It is not available while you impersonate another user.

- **Authenticator app**: choose **Set up**, scan the QR code with an authenticator app (or type the shown key), then type the six digit code the app shows and choose **Confirm**. Until you confirm, the app is not used. **Remove** turns it off.
- **Passkeys**: choose **Add passkey**, optionally name it (for example "Work laptop") and confirm with your device (security key, phone, Windows Hello, Touch ID). You can add several and **Remove** any of them.
- **Recovery codes**: when you turn on the first method you get 20 recovery codes, 8 letters and numbers each. They are shown **only once**: copy them or download them and keep them somewhere safe. If you lose your authenticator app or passkey, type a recovery code at the second step of the sign in instead. Every code works only once, and when the last one is used you are shown a new set. **Generate new recovery codes** replaces all of them at any time; the old ones stop working.

When the administrator requires two-factor authentication you cannot remove your last method. Removing the last method turns two-factor authentication off and deletes the recovery codes.

## Session

**Session** shows who the app thinks you are right now and how long you stay signed in.

- **User**: your name, user id, email addresses, and your roles with the grants each role gives you. The grants listed per role already have that role's restrictions applied.
- **Details**: when the session started, its age, when it expires, when the short-lived access token was issued and expires, the remote address, and the browser (user agent) the session was started from.

The access token renews itself automatically while you use the app. **Refresh** reloads the information. If your roles or access were changed by an administrator, refresh to pick up the change.

If you do not already hold every elevated role, this page also shows the [elevation form](./elevation.md).

## All your sessions

**Sessions** lists every place you are signed in.

- **Current sessions**: IP address, browser, when it started, when it was last active, and when it expires. Your present session is marked **This session**.
- **Show ended sessions**: previous sessions with when they ended and why ("Signed out" or "Expired"). Long lists are paged.
- **Refresh** reloads the lists.
- **Sign out all sessions** ends all of them, including this one.

If you see a session you do not recognise, sign out all sessions and change your password.
