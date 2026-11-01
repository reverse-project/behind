define(["controllers/controller", "text!views/confirmemail.html", "viewmodels/confirmemail"], function (createController, view, ConfirmEmailViewModel) {
    return createController(view, ConfirmEmailViewModel);
});
