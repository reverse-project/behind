define(["controllers/controller", "text!views/roles.html", "viewmodels/roles"], function (createController, view, RolesViewModel) {
    return createController(view, RolesViewModel);
});
