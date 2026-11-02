define(["controllers/controller", "text!views/articlereader.html", "viewmodels/articlereader"], function (createController, view, ArticleReaderViewModel) {
    return createController(view, ArticleReaderViewModel);
});
