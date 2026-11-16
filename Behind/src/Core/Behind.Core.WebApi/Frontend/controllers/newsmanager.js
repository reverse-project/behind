define(["controllers/controller", "text!views/newsmanager.html", "viewmodels/newsmanager"], function (createController, view, NewsManagerViewModel) {
    return createController(view, NewsManagerViewModel);
});
