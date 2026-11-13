# Behind.Web security review

Date: 2026-11-13.
Reviewed commit: `a397bba445dc6b95020e1458c01fb0a834e6b453`.

## Scope

Non-destructive black-box testing against a disposable, loopback-only Production
instance preceded a light source review of Behind.Web and relevant Core, Common,
Contact, and MarkdownViewer web surfaces. Existing application data was not used.
The instance ran existing .NET 11 Debug binaries; exact correspondence to the
reviewed source was not established.

## Findings

| # | Severity | File | Lines | Vulnerability | Confidence |
|---|----------|------|-------|---------------|------------|
| 1 | HIGH | [SignInEndpoints.cs](Behind/src/Core/Behind.Core.WebApi/Endpoints/SignInEndpoints.cs#L141) | 141-144 | Host-header poisoning of authentication emails when Frontend:Url is unset | 9/10 |

### 1. Request Host controls credential-bearing email links

When `Frontend:Url` is absent, authentication emails fall back to a base URI
derived from the incoming request's scheme and Host. If the deployment accepts an
attacker-supplied Host, an attacker can request an authentication email for a known
victim address. Following the genuine email can deliver the token to an
attacker-controlled origin, whose JavaScript can read the URL fragment.

Potential impact is account takeover without an independent second factor.
Exploitation requires working email delivery, an unset canonical frontend URL,
acceptance of the supplied Host, completion of the normal CAPTCHA, and victim
interaction. Independent MFA can prevent completion.

Evidence:

- [SignInEndpoints.cs](Behind/src/Core/Behind.Core.WebApi/Endpoints/SignInEndpoints.cs#L141),
  lines 141-144, signs the credential, falls back to `httpContext.BaseUri()`, and
  queues the link.
- [HttpContextExtensions.cs](Behind/src/Common/Behind.Common.WebApi/HttpContextExtensions.cs#L17)
  derives the URI from `Request.Scheme` and `Request.Host`.
- [MagicLinkExtensions.cs](Behind/src/Core/Behind.Core.WebApi/MagicLinks/MagicLinkExtensions.cs#L23),
  lines 23-24, and
  [FrontendLinkExtensions.cs](Behind/src/Core/Behind.Core.WebApi/FrontendLinkExtensions.cs#L22),
  lines 22-23, construct the token link using that origin.
- [appsettings.json](Behind/src/Web/Behind.Web/appsettings.json#L10) defaults to
  `"AllowedHosts": "*"`. Fresh generated configuration had no `Frontend.Url`.
- Source-informed runtime requests to `/accounts/settings` and `/about` with
  `Host: audit-host.invalid` both returned 200.
- [SignInMagicLinkEndpointsTests.cs](Behind/tests/Behind.Tests/CoreWebApi/Endpoints/SignInMagicLinkEndpointsTests.cs#L347),
  lines 347-353, explicitly expects request-base-URI fallback. Inspected, not run.
- [AddressConfirmationExtensions.cs](Behind/src/Core/Behind.Core.WebApi/Endpoints/AddressConfirmationExtensions.cs#L30),
  lines 30-33, shares the fallback for email-confirmation links.

Verified non-email probe against the disposable target:

```bash
curl --max-time 5 -i -H 'Host: audit-host.invalid' \
  http://127.0.0.1:5187/accounts/settings
# HTTP 200
```

Host acceptance and missing canonical URL were verified dynamically.
Credential-bearing URL construction was established from source. No email was
sent, token captured, or account taken over.

Recommended remediation: require a configured, validated canonical HTTPS frontend
URL for credential-bearing emails and fail closed when it is unavailable.
Restrict accepted hosts at the application and ingress. Add captured-mail
regression tests covering sign-in, lost-password, and email-confirmation links:
an arbitrary request Host must never change their origin.

## Other observations

- Anonymous session, user/role/site/settings management, contact submission
  management, and tested write endpoints returned 401.
- Invalid Bearer tokens were rejected.
- Tested configuration files, repository metadata, source maps, and encoded
  traversal paths returned 404 without file contents.
- CSP, nosniff, and no-referrer headers were present on the home response.
- Tested cross-origin requests did not receive permissive CORS headers.
- Malformed authentication JSON returned 400 without stack traces.
- `/openapi/v1.json` returned a generic 500 response. This is an operational
  observation, not a reported vulnerability.
- Light source inspection covered JWT/session checks, password reset/revocation,
  email ownership, grant escalation, path containment, image/attachment handling,
  sanitization, documentation file lookup, raw SQL construction, contact response
  separation, and generic exception responses. No additional high-confidence
  finding was established in the inspected portions.

## Limitations

CAPTCHA prevented creation of authenticated identities. Cross-user authorization,
uploads, stored-XSS browser execution, populated-content/media visibility,
refresh/revocation, passkeys/2FA, actual email delivery, HTTPS/ingress behavior, and
configured documentation serving remain dynamically unverified.

Automated builds and .NET tests were not run; related test source was inspected.
No dependency-advisory audit, load testing, external-target testing, or exhaustive
repository review was performed. The disposable server was stopped and its data
and configuration removed. This review is not an application-wide security
clearance. The finding remains unremediated by this documentation-only change.
