define(["controllers/controller", "text!views/galleries.html", "viewmodels/galleries"], function (createController, view, GalleriesViewModel) {
    return createController(view, GalleriesViewModel);
});
