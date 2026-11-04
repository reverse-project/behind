# Getting Started

## The navigation bar

| Item | Shown | Goes to |
|------|-------|---------|
| Behind / Index | always | the welcome page |
| Wall of shame | always | [the wall of shame](./wall-of-shame.md) |
| Sign in, Sign up | when signed out | the sign in and sign up pages |
| Your name (menu) | when signed in | Profile, Session, Sessions, Administration (only if you may use it), and Sign out |

If you have no display name, your email address is shown in the menu.

## Creating an account

1. Choose **Sign up**.
2. Enter your **email** and a **password**.
3. Optionally enter a **display name**. If you leave it empty your email address is shown instead.
4. Choose **Create account**.

If the email is already in use you are told so. The administrator of the server can hide the **Sign up** and **Sign in** buttons of the top menu in `backend.json`; this only hides the buttons, the pages themselves still work when you open their address. After the account is created, sign in.

Unless the installation has turned email confirmation off, you also receive an email with a link to confirm your address. Follow it to mark your email as confirmed. If confirmation is turned off, your email counts as confirmed the moment you sign up.

Every new account gets the **Users** role, which lets you read the basic areas of the application.

## Signing in

### With a password

1. Choose **Sign in**.
2. Enter your email and password and choose **Sign in**.

### With an email link (magic link)

1. On the sign in page enter your email.
2. Choose **Email me a sign in link**. For privacy the app always says a link is on its way, whether or not the account exists.
3. Open the email and follow the link. You are signed in and taken to the app.

A link works for a short time only. If it is invalid or expired, the page offers to request a new one. If requests come too quickly you are asked to wait.

### Lost password

1. On the sign in page enter your email and choose **Lost your password?**. You receive a sign in link the same way as with a magic link.
2. Follow the link to sign in.
3. Open **Profile**, enter a new password under **New password** and choose **Save**.

### Banned accounts

If your account is banned, signing in fails and the reason given by the administrator is shown. See [Wall of shame](./wall-of-shame.md).

## Signing out

- **Sign out** (navigation menu or welcome page) ends the current session only.
- **Sign out all sessions** on the [Sessions page](./your-account.md#all-your-sessions) ends every session of your account, including other browsers and devices.

If your session expires or an administrator signs you out, you are asked to sign in again.
