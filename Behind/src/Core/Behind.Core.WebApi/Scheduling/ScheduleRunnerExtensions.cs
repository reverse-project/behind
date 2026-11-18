using System.Net;
using Behind.Common;
using Behind.Common.WebApi;
using Behind.Core.Database;
using Microsoft.AspNetCore.WebUtilities;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Scheduling;

public static class ScheduleRunnerExtensions
{
    private const string HashPrefix = "#";

    extension(IEnumerable<IScheduledAction> actions)
    {
        public Option<IScheduledAction> FindAction(string key) =>
            actions.Where(action => string.Equals(action.Descriptor.Key, key, StringComparison.Ordinal)).Head();
    }

    extension(ScheduledActionLink link)
    {
        public string ToHashRoute() => HashPrefix + QueryHelpers.AddQueryString(link.Route, link.Query);
    }

    extension(IScheduleRunQueries.ScheduleRunDetails run)
    {
        public IScheduleRunCommands.ScheduleRunDraft ToDraft() =>
            new(
                run.ScheduleId,
                run.ScheduleName,
                run.Action,
                run.ScheduledFor,
                run.StartedAt,
                run.FinishedAt,
                run.Status,
                run.Summary,
                run.Link,
                run.Email,
                run.Recipients);
    }

    extension(string scheduleName)
    {
        public string ToScheduleEmailSubject(bool succeeded) =>
            $"{scheduleName}: {(succeeded ? "succeeded" : "failed")}";

        public string ToScheduleEmailBody(ScheduledActionResult outcome, Option<string> link) =>
            $"<p>The scheduled task <strong>{WebUtility.HtmlEncode(scheduleName)}</strong> {(outcome.Succeeded ? "succeeded" : "failed")}.</p>"
            + (outcome.Summary.Length > 0 ? $"<p>{WebUtility.HtmlEncode(outcome.Summary)}</p>" : string.Empty)
            + link.Match(
                () => string.Empty,
                url => $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">Open the result</a></p>");
    }

    extension(DateTimeOffset moment)
    {
        public DateTimeOffset ToScheduleMinute() =>
            new(moment.UtcDateTime.Year, moment.UtcDateTime.Month, moment.UtcDateTime.Day,
                moment.UtcDateTime.Hour, moment.UtcDateTime.Minute, 0, TimeSpan.Zero);

        public IReadOnlyList<DateTimeOffset> ScheduleMinutesUntil(DateTimeOffset now, int maxCatchUpMinutes)
        {
            var last = now.ToScheduleMinute();
            var first = moment.ToScheduleMinute().AddMinutes(1);
            var earliest = last.AddMinutes(-maxCatchUpMinutes);
            var start = first < earliest ? earliest : first;

            return start > last
                ? []
                : Enumerable.Range(0, (int)(last - start).TotalMinutes + 1).Select(offset => start.AddMinutes(offset)).ToList();
        }
    }

}
