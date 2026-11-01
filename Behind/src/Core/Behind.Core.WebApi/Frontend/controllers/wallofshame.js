define(["controllers/controller", "text!views/wallofshame.html", "viewmodels/wallofshame"], function (createController, view, WallOfShameViewModel) {
    return createController(view, WallOfShameViewModel);
});
