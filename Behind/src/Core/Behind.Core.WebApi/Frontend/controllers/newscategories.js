define(["controllers/controller", "text!views/newscategories.html", "viewmodels/newscategories"], function (createController, view, NewsCategoriesViewModel) {
    return createController(view, NewsCategoriesViewModel);
});
