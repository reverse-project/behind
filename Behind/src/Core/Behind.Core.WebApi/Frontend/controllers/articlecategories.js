define(["controllers/controller", "text!views/articlecategories.html", "viewmodels/articlecategories"], function (createController, view, ArticleCategoriesViewModel) {
    return createController(view, ArticleCategoriesViewModel);
});
