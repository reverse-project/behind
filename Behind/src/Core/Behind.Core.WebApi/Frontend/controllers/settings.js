define(["controllers/controller", "text!views/settings.html", "viewmodels/settings"], function (createController, view, SettingsViewModel) {
    return createController(view, SettingsViewModel);
});
