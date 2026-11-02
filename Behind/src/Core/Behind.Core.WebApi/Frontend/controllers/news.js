define(["controllers/controller", "text!views/news.html", "viewmodels/news"], function (createController, view, NewsViewModel) {
    return createController(view, NewsViewModel);
});
