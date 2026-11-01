define(["controllers/controller", "text!views/sites.html", "viewmodels/sites"], function (createController, view, SitesViewModel) {
    return createController(view, SitesViewModel);
});
