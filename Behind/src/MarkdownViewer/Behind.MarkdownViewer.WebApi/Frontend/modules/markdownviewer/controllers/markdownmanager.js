define(["controllers/controller", "text!modules/markdownviewer/views/markdownmanager.html", "modules/markdownviewer/viewmodels/markdownmanager"], function (createController, view, MarkdownManagerViewModel) {
    return createController(view, MarkdownManagerViewModel);
});
