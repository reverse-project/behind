define(["controllers/controller", "text!views/galleryviewer.html", "viewmodels/galleryviewer"], function (createController, view, GalleryViewerViewModel) {
    return createController(view, GalleryViewerViewModel);
});
