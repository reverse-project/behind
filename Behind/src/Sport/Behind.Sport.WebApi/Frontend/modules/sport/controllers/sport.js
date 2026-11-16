define(["controllers/controller", "text!modules/sport/views/sport.html", "modules/sport/viewmodels/sport"], function (createController, view, SportViewModel) {
    return createController(view, SportViewModel);
});
