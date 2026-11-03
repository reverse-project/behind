define(["controllers/controller", "text!views/downloads.html", "viewmodels/downloads"], function (createController, view, DownloadsViewModel) {
    return createController(view, DownloadsViewModel);
});
