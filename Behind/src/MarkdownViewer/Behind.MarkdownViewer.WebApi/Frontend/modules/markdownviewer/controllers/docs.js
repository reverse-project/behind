define(["controllers/controller", "text!modules/markdownviewer/views/docs.html", "modules/markdownviewer/viewmodels/docs"], function (createController, view, DocsViewModel) {
    return createController(view, DocsViewModel);
});
