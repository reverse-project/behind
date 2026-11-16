define(["modules/sport/models/seasonbrowser", "modules/sport/models/listing", "modules/sport/models/links"], function (createSeasonBrowser, createListing, links) {
    return function StatsViewModel(context) {
        var self = this;

        self.links = links;
        self.browser = createSeasonBrowser(context, links.stats);
        self.list = createListing({
            resource: "stats",
            pageSize: 25,
            filters: function () {
                return { season: self.browser.seasonId, statType: self.browser.statTypeId };
            }
        });
        self.leaderboardLink = function () {
            return links.leaderboards(self.browser.seasonId, self.browser.statTypeId);
        };
        self.hasSeason = self.browser.seasonId !== null;

        self.activate = function () {
            return self.browser.activate().then(function (ready) {
                return ready ? self.list.refresh() : undefined;
            });
        };
    };
});
