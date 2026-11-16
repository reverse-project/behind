define(["controllers/controller", "text!views/routealiases.html", "viewmodels/routealiases"], function (createController, view, RouteAliasesViewModel) {
    return createController(view, RouteAliasesViewModel);
});
