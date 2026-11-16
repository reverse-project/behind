define(["controllers/controller", "text!modules/sport/views/teams.html", "modules/sport/viewmodels/teams"], function (createController, view, TeamsViewModel) {
    return createController(view, TeamsViewModel);
});
