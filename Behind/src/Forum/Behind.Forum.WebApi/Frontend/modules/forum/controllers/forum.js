define(["controllers/controller", "text!modules/forum/views/forum.html", "modules/forum/viewmodels/forum", "modules/forum/bindings/bbcode"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
