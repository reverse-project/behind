define(["controllers/controller", "text!modules/sport/views/team.html", "modules/sport/viewmodels/team"], function (createController, view, TeamViewModel) {
    return createController(view, TeamViewModel);
});
