define(["ko", "models/pager", "modules/sport/models/sport"], function (ko, createPager, sport) {
    return function createListing(config) {
        var listing = {
            loading: ko.observable(true),
            message: ko.observable(""),
            search: ko.observable(""),
            items: ko.observableArray([])
        };

        listing.pager = createPager(function () {
            return listing.refresh();
        });
        listing.hasItems = ko.pureComputed(function () {
            return listing.items().length > 0;
        });
        listing.isEmpty = ko.pureComputed(function () {
            return !listing.loading() && listing.message() === "" && !listing.hasItems();
        });

        listing.refresh = function () {
            var query = Object.assign(
                { page: listing.pager.page(), pageSize: config.pageSize, search: listing.search().trim() },
                config.filters ? config.filters() : {}
            );

            listing.loading(true);

            return sport
                .list(config.resource, query)
                .then(function (result) {
                    listing.message(result.message);

                    if (!result.ok) {
                        listing.items([]);
                        return;
                    }

                    listing.pager.update(result.data.page);
                    listing.items(result.data.page.items);

                    if (config.loaded) {
                        config.loaded(result.data.page.items);
                    }
                })
                .finally(function () {
                    listing.loading(false);
                });
        };

        listing.find = function () {
            listing.pager.page(1);
            return listing.refresh();
        };

        return listing;
    };
});
