define(["controllers/controller", "text!views/signup.html", "viewmodels/signup"], function (createController, view, SignUpViewModel) {
    return createController(view, SignUpViewModel);
});
