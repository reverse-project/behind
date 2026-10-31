define(["controllers/controller", "text!views/session.html", "viewmodels/session"], function (createController, view, SessionViewModel) {
    return createController(view, SessionViewModel);
});
