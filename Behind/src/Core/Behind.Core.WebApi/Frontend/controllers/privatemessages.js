define(["controllers/controller", "text!views/privatemessages.html", "viewmodels/privatemessages", "bindings/bbcode"], function (createController, view, ViewModel) {
    return createController(view, ViewModel);
});
