define(["controllers/controller", "text!views/layouts.html", "viewmodels/layouts"], function (createController, view, LayoutsViewModel) {
    return createController(view, LayoutsViewModel);
});
