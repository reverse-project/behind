define(["controllers/controller", "text!views/profile.html", "viewmodels/profile"], function (createController, view, ProfileViewModel) {
    return createController(view, ProfileViewModel);
});
