define(["controllers/controller", "text!views/magiclink.html", "viewmodels/magiclink"], function (createController, view, MagicLinkViewModel) {
    return createController(view, MagicLinkViewModel);
});
