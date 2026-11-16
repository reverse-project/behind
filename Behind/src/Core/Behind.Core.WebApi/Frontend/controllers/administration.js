define(["controllers/controller", "text!views/administration.html", "viewmodels/administration"], function (createController, view, AdministrationViewModel) {
    return createController(view, AdministrationViewModel);
});
