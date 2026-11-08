define(["controllers/controller", "text!modules/sport/views/leaderboards.html", "modules/sport/viewmodels/leaderboards"], function (createController, view, LeaderboardsViewModel) {
    return createController(view, LeaderboardsViewModel);
});
