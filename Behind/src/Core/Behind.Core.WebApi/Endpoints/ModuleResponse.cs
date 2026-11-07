namespace Behind.Core.WebApi.Endpoints;

public sealed record ModuleRouteResponse(string Path, string Controller, bool Open);

public sealed record ModuleAdministrationPageResponse(string Name, string Icon, string Href, string Grant);

public sealed record ModuleResponse(
    string Key,
    string Name,
    string Path,
    IReadOnlyList<ModuleRouteResponse> Routes,
    IReadOnlyList<ModuleAdministrationPageResponse> AdministrationPages,
    IReadOnlyList<string> Styles);

public sealed record ModulesResponse(IReadOnlyList<ModuleResponse> Modules);
