define(["ko", "models/galleries", "models/newsmeta", "models/pager"], function (ko, galleries, meta, createPager) {
    var pageSize = 12;

    var present = function (item) {
        return {
            id: item.id,
            title: item.title,
            description: item.description,
            author: meta.author(item.author),
            stamp: meta.stamp(item),
            created: meta.created(item),
            thumbnailUrl: item.thumbnailUrl,
            hasThumbnail: !!item.thumbnailUrl,
            count: galleries.imageCountLabel(item.itemCount),
            href: "#/galleries/" + item.id
        };
    };

    return function GalleriesViewModel(context) {
        var self = this;

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.items = ko.observableArray([]);
        self.search = ko.observable(context.params.search || "");
        self.pager = createPager(function () {
            return self.refresh();
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.items().length === 0 && !self.busy();
        });

        self.refresh = function () {
            self.busy(true);

            return galleries.list({ page: self.pager.page(), pageSize: pageSize, search: self.search().trim() })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.items([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data.galleries)) {
                        self.pager.clampIfOutOfRange(result.data.galleries);
                        return self.refresh();
                    }

                    self.pager.update(result.data.galleries);
                    self.items(result.data.galleries.items.map(present));

                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.find = function () {
            self.pager.page(1);

            return self.refresh();
        };

        self.activate = function () {
            return self.refresh();
        };
    };
});
