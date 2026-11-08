define(["controllers/controller", "text!modules/sport/views/season.html", "modules/sport/viewmodels/season"], function (createController, view, SeasonViewModel) {
    return createController(view, SeasonViewModel);
});
