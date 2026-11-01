define(["controllers/controller", "text!views/site.html", "viewmodels/site"], function (createController, view, SiteViewModel) {
    return createController(view, SiteViewModel);
});
