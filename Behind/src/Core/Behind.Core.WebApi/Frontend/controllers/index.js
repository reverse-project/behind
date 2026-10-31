define(["controllers/controller", "text!views/index.html", "viewmodels/index"], function (createController, view, IndexViewModel) {
    return createController(view, IndexViewModel);
});
