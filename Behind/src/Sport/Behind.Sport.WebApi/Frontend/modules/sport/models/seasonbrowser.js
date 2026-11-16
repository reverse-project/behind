define(["ko", "models/i18n", "modules/sport/models/sport", "modules/sport/models/format"], function (ko, i18n, sport, format) {
    var pageSize = 100;

    var itemsOf = function (result) {
        return result.ok ? result.data.page.items : [];
    };

    return function createSeasonBrowser(context, route) {
        var browser = {
            message: ko.observable(""),
            disciplines: ko.observableArray([]),
            disciplineId: ko.observable(null),
            seasons: ko.observableArray([]),
            statTypes: ko.observableArray([]),
            season: ko.observable(null),
            seasonId: context.params.season || null,
            statTypeId: context.params.statType || null
        };

        var seasonsOf = function (disciplineId) {
            return sport.list("seasons", { discipline: disciplineId, pageSize: pageSize }).then(function (result) {
                return format.newestFirst(itemsOf(result));
            });
        };

        var show = function (disciplineId) {
            browser.disciplineId(disciplineId);

            return Promise.all([
                seasonsOf(disciplineId),
                sport.list("stat-types", { discipline: disciplineId, pageSize: pageSize })
            ]).then(function (loaded) {
                browser.seasons(loaded[0]);
                browser.statTypes(itemsOf(loaded[1]));
            });
        };

        var openNewest = function (discipline, go) {
            browser.message("");

            return seasonsOf(discipline.id).then(function (seasons) {
                if (seasons.length === 0) {
                    browser.disciplineId(discipline.id);
                    browser.seasons([]);
                    browser.statTypes([]);
                    browser.message(i18n.t("sport.browser.noSeasons"));
                    return false;
                }

                go(route(seasons[0].id, null));
                return false;
            });
        };

        var open = function () {
            return sport.season(browser.seasonId).then(function (result) {
                browser.message(result.message);

                if (!result.ok) {
                    return false;
                }

                browser.season(result.data.season);

                return show(result.data.season.disciplineId).then(function () {
                    return true;
                });
            });
        };

        browser.seasonLink = function (season) {
            return route(season.id, browser.statTypeId);
        };

        browser.statTypeLink = function (statType) {
            return route(browser.seasonId, statType ? statType.id : null);
        };

        browser.choose = function (discipline) {
            return openNewest(discipline, context.navigate);
        };

        browser.activate = function () {
            return sport.list("disciplines", { pageSize: pageSize }).then(function (result) {
                browser.disciplines(itemsOf(result));

                if (browser.seasonId) {
                    return open();
                }

                return browser.disciplines().length > 0
                    ? openNewest(browser.disciplines()[0], context.replace)
                    : false;
            });
        };

        return browser;
    };
});
