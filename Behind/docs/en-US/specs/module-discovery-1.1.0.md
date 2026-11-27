# Module discovery 1.1.0

Status: current. This revision extends the unversioned `GET /modules` response
described by [configuration 1.7.0](./configuration-json-1.7.0.md). The response
conforms to the [module discovery schema](../../schemas/module-discovery-1.1.0.schema.json).

## Discovery

`GET /modules` is public, returns JSON with `Cache-Control: no-store`, and lists
only enabled module descriptors. Each descriptor contains its key, name, frontend
path, routes (path, controller and open flag), administration pages (name, icon,
href and grant), styles and widgets (key, name and model).

The new `availableGrants` array contains grant names from the installed access
catalog except areas contributed by disabled modules. Core grants remain listed.
It is for display filtering only, not an authorization decision. Existing role,
user and session APIs retain their full stored or calculated access data.

The frontend filters role grants and restrictions, user access editors and
calculated grants, session role grants and profile moderation access. Role and
user editor saves include hidden direct selections unchanged. Explicit role
grant reset still restores all default grants. Disabling a module does not
delete its assignments; after reactivation and browser reload they reappear.

## Compatibility and migration

The response adds `availableGrants`; existing descriptor fields are unchanged.
Clients must tolerate additional properties. Deploy matching backend and
frontend versions together: a frontend without the array hides access entries
and logs an error. No database or configuration migration is needed. Other open
browsers must reload after a switch change.
