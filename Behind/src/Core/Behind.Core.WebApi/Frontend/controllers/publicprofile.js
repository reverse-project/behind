define(["controllers/controller", "text!views/publicprofile.html", "viewmodels/publicprofile"], function (createController, view, PublicProfileViewModel) {
    return createController(view, PublicProfileViewModel);
});
