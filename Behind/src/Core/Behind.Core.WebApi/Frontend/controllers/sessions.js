define(["controllers/controller", "text!views/sessions.html", "viewmodels/sessions"], function (createController, view, SessionsViewModel) {
    return createController(view, SessionsViewModel);
});
