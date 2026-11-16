define(["controllers/controller", "text!views/newsarticle.html", "viewmodels/newsarticle"], function (createController, view, NewsArticleViewModel) {
    return createController(view, NewsArticleViewModel);
});
