define(["controllers/controller", "text!views/siteeditor.html", "viewmodels/siteeditor"], function (createController, view, SiteEditorViewModel) {
    return createController(view, SiteEditorViewModel);
});
