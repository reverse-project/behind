define(["controllers/controller", "text!views/articlemanager.html", "viewmodels/articlemanager"], function (createController, view, ArticleManagerViewModel) {
    return createController(view, ArticleManagerViewModel);
});
