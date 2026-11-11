define(["controllers/controller", "text!modules/forum/views/forummanager.html", "modules/forum/viewmodels/forummanager", "modules/forum/bindings/bbcode"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
