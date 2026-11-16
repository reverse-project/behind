define(["controllers/controller", "text!views/profile.html", "viewmodels/profile"], function (createController, view, ProfileViewModel) {
    return createController(view, function ProfilePasswordViewModel(context) {
        var viewModel = new ProfileViewModel(context);

        viewModel.settingNewPassword(true);

        return viewModel;
    });
});
