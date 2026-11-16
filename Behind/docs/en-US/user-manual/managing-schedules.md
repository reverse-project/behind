# Managing schedules

Schedules run predefined actions on a five-field cron expression in UTC. Open **Administration > Schedules**; you need `Schedule.Read` to view schedules, `Schedule.Write` to create or edit them, `Schedule.Delete` to delete them, and `Schedule.Manage` to run one immediately. Developers with `RawDatabase.Manage` can also show deleted schedules and runs, restore them, or permanently delete them.

## Creating a schedule

Enter a name, choose whether the schedule is enabled, and select an action. Actions are supplied by Core and installed modules: Core provides **Session cleanup** (`core.session-cleanup`). Its available parameters and choices are provided by the action and shown dynamically. A template parameter can expose clickable `{{placeholder}}` chips; clicking a chip inserts it at the cursor.

A cron expression has five whitespace-separated fields:

| Field | Values |
|---|---|
| Minute | 0–59 |
| Hour | 0–23 |
| Day of month | 1–31 |
| Month | 1–12 |
| Day of week | 0–6 (Sunday is 0) |

Use `*` for every value, `*/N` for every N values, `a,b,c` for selected values, and `a-b` for a range. For example, `*/15 * * * *` runs every 15 minutes; `0 2 * * *` runs daily at 02:00 UTC; `30 8 * * 1-5` runs at 08:30 UTC on weekdays. The field calculator edits the same raw expression. Presets cover every minute, hourly, daily, weekly, and monthly schedules. Expressions that do not fit the calculator's simple modes remain editable as raw cron. Validation and the next five occurrences are shown below the expression.

## Session cleanup

The **Session cleanup** action permanently deletes sessions that were invalidated (signed out, ended by an administrator, or replaced) more than **Invalidated session age (days)** ago, measured from the scheduled run time. The age is a whole number of days; leaving it empty uses the default of 30 days. The upper limit is 3650 days unless the operator changes `Backend:Scheduler:MaxSessionCleanupAgeDays`. Active sessions, and invalidated sessions without an invalidation time, are never removed. The run summary reports how many sessions were deleted; the action does not produce a result link. For example, the cron expression `0 3 * * *` runs the cleanup every day at 03:00 UTC.

## Participants and email

The Participants tab lets you add roles, search for users, or add custom email addresses. Duplicate entries are ignored. Choose when to email the action result: **Never**, **Always**, **On success**, or **On failure**. The result email can include a link to the generated report. An empty participant list means there are no selected recipients.

## Running and reviewing

Use **Run now** to queue an immediate run; an already-running schedule cannot be started a second time. Select **Runs** beside a schedule to open its filtered history, or use **Run history** for all schedules. The runs page shows status, summary, start and finish times, email delivery status, and recipients. Follow **Open result** to view the action result when a link is available. Schedules and runs can be soft-deleted; developers can restore or permanently delete their deleted records.
