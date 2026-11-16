define(["controllers/controller", "text!views/articleeditor.html", "viewmodels/articleeditor"], function (createController, view, ArticleEditorViewModel) {
    return createController(view, ArticleEditorViewModel);
});
