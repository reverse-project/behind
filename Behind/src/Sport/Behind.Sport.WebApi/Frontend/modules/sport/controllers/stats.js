define(["controllers/controller", "text!modules/sport/views/stats.html", "modules/sport/viewmodels/stats"], function (createController, view, StatsViewModel) {
    return createController(view, StatsViewModel);
});
