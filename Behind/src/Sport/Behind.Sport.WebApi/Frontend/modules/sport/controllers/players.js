define(["controllers/controller", "text!modules/sport/views/players.html", "modules/sport/viewmodels/players"], function (createController, view, PlayersViewModel) {
    return createController(view, PlayersViewModel);
});
