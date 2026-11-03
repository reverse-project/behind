define(["controllers/controller", "text!views/galleryeditor.html", "viewmodels/galleryeditor"], function (createController, view, GalleryEditorViewModel) {
    return createController(view, GalleryEditorViewModel);
});
