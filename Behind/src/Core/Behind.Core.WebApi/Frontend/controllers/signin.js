define(["controllers/controller", "text!views/signin.html", "viewmodels/signin"], function (createController, view, SignInViewModel) {
    return createController(view, SignInViewModel);
});
