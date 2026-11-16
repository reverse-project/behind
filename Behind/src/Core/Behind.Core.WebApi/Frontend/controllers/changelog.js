define(["controllers/controller", "text!views/changelog.html", "viewmodels/changelog"], function (createController, view, ChangelogViewModel) {
    return createController(view, ChangelogViewModel);
});
