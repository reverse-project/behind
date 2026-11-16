define(["controllers/controller", "text!views/articles.html", "viewmodels/articles"], function (createController, view, ArticlesViewModel) {
    return createController(view, ArticlesViewModel);
});
