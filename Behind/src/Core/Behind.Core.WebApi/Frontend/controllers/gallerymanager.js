define(["controllers/controller", "text!views/gallerymanager.html", "viewmodels/gallerymanager"], function (createController, view, GalleryManagerViewModel) {
    return createController(view, GalleryManagerViewModel);
});
