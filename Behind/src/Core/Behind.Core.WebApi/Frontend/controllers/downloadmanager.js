define(["controllers/controller", "text!views/downloadmanager.html", "viewmodels/downloadmanager"], function (createController, view, DownloadManagerViewModel) {
    return createController(view, DownloadManagerViewModel);
});
