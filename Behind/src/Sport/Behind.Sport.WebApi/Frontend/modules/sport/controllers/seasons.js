define(["controllers/controller", "text!modules/sport/views/seasons.html", "modules/sport/viewmodels/seasons"], function (createController, view, SeasonsViewModel) {
    return createController(view, SeasonsViewModel);
});
