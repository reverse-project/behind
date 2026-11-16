define(["ko", "modules/sport/models/sport", "modules/sport/models/seasonbrowser", "modules/sport/models/links"], function (ko, sport, createSeasonBrowser, links) {
    var topLimit = 10;
    var fullLimit = 100;

    return function LeaderboardsViewModel(context) {
        var self = this;

        self.links = links;
        self.browser = createSeasonBrowser(context, links.leaderboards);
        self.message = ko.observable("");
        self.boards = ko.observableArray([]);
        self.board = ko.observable(null);
        self.isSingle = self.browser.statTypeId !== null;
        self.hasBoards = ko.pureComputed(function () {
            return self.boards().length > 0;
        });

        var loadBoards = function (seasonId) {
            return sport.leaderboards(seasonId, topLimit).then(function (result) {
                self.message(result.message);
                self.boards(result.ok ? result.data.boards : []);
            });
        };

        var loadBoard = function (seasonId) {
            return sport.leaderboard(seasonId, self.browser.statTypeId, fullLimit).then(function (result) {
                self.message(result.message);
                self.board(result.ok ? result.data : null);
            });
        };

        self.activate = function () {
            return self.browser.activate().then(function (ready) {
                if (!ready) {
                    return undefined;
                }

                return self.isSingle ? loadBoard(self.browser.seasonId) : loadBoards(self.browser.seasonId);
            });
        };
    };
});
