define(["controllers/controller", "text!views/newseditor.html", "viewmodels/newseditor"], function (createController, view, NewsEditorViewModel) {
    return createController(view, NewsEditorViewModel);
});
