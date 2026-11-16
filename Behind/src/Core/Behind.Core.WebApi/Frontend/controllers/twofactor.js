define(["controllers/controller", "text!views/twofactor.html", "viewmodels/twofactor"], function (createController, view, TwoFactorViewModel) {
    return createController(view, TwoFactorViewModel);
});
