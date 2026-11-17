using System.Security.Claims;
using Behind.Common.WebApi;

namespace Behind.Tests.Helpers;

public sealed class FakeScheduledAction(
    Func<ScheduledActionInvocation, CancellationToken, Task<ScheduledActionResult>>? run = null) : IScheduledAction
{
    public const string ReportParameter = "report";
    public const string NoteParameter = "note";

    public List<ScheduledActionInvocation> Invocations { get; } = [];

    public Option<string> Rejection { get; set; } = None;

    public ScheduledActionDescriptor Descriptor { get; } = new(
        ScheduleTestBedExtensions.TestAction,
        "test.action.name",
        [
            new ScheduledActionParameter(ReportParameter, "test.action.report", ScheduledActionParameterKind.Choice, true),
            new ScheduledActionParameter(NoteParameter, "test.action.note", ScheduledActionParameterKind.Template, false)
            {
                DefaultValue = "{Graph}",
                Placeholders = ["Graph"]
            }
        ]);

    public Task<IReadOnlyList<ScheduledActionOption>> Options(
        ClaimsPrincipal user,
        string parameter,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ScheduledActionOption>>([new ScheduledActionOption("r1", $"{parameter} one")]);

    public Task<Option<string>> Validate(
        ClaimsPrincipal user,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        Task.FromResult(Rejection);

    public async Task<ScheduledActionResult> Run(ScheduledActionInvocation invocation, CancellationToken cancellationToken)
    {
        lock (Invocations)
        {
            Invocations.Add(invocation);
        }

        return run is null
            ? new ScheduledActionResult(
                true,
                "Report created",
                Some(new ScheduledActionLink("/reports/42", new Dictionary<string, string?> { ["tab"] = "summary" })))
            : await run(invocation, cancellationToken);
    }
}
