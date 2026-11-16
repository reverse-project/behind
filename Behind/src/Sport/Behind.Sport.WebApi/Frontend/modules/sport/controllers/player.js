define(["controllers/controller", "text!modules/sport/views/player.html", "modules/sport/viewmodels/player"], function (createController, view, PlayerViewModel) {
    return createController(view, PlayerViewModel);
});
