define(["controllers/controller", "text!views/restriction.html", "viewmodels/restriction"], function (createController, view, RestrictionViewModel) {
    return createController(view, RestrictionViewModel);
});
