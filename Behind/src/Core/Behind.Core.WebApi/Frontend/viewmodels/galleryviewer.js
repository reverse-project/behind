define(["ko", "models/galleries", "models/newsmeta"], function (ko, galleries, meta) {
    var escapeKey = "Escape";
    var previousKey = "ArrowLeft";
    var nextKey = "ArrowRight";

    return function GalleryViewerViewModel(context) {
        var self = this;
        var galleryId = context.params.id;

        self.loading = ko.observable(true);
        self.message = ko.observable("");
        self.title = ko.observable("");
        self.description = ko.observable("");
        self.author = ko.observable("");
        self.authorHref = ko.observable(null);
        self.stamp = ko.observable("");
        self.created = ko.observable("");
        self.images = ko.observableArray([]);
        self.selectedIndex = ko.observable(-1);
        self.lightboxFocus = ko.observable(false);

        self.found = ko.pureComputed(function () {
            return !self.loading() && self.message() === "";
        });

        self.missing = ko.pureComputed(function () {
            return !self.loading() && self.message() !== "";
        });

        self.hasDescription = ko.pureComputed(function () {
            return self.description() !== "";
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.found() && self.images().length === 0;
        });

        self.count = ko.pureComputed(function () {
            return galleries.imageCount(self.images().length);
        });

        self.selected = ko.pureComputed(function () {
            return self.images()[self.selectedIndex()] || null;
        });

        self.caption = ko.pureComputed(function () {
            return self.selected() === null ? "" : self.selected().description;
        });

        self.hasCaption = ko.pureComputed(function () {
            return self.caption() !== "";
        });

        self.isOpen = ko.pureComputed(function () {
            return self.selected() !== null;
        });

        self.position = ko.pureComputed(function () {
            return self.isOpen() ? (self.selectedIndex() + 1) + " / " + self.images().length : "";
        });

        self.hasPrevious = ko.pureComputed(function () {
            return self.selectedIndex() > 0;
        });

        self.hasNext = ko.pureComputed(function () {
            return self.selectedIndex() < self.images().length - 1;
        });

        self.open = function (index) {
            self.selectedIndex(index);
            self.lightboxFocus(true);
        };

        self.close = function () {
            self.selectedIndex(-1);
            self.lightboxFocus(false);
        };

        self.previous = function () {
            if (self.hasPrevious()) {
                self.selectedIndex(self.selectedIndex() - 1);
            }
        };

        self.next = function () {
            if (self.hasNext()) {
                self.selectedIndex(self.selectedIndex() + 1);
            }
        };

        self.press = function (data, event) {
            var handlers = {};

            handlers[escapeKey] = self.close;
            handlers[previousKey] = self.previous;
            handlers[nextKey] = self.next;

            if (!handlers[event.key]) {
                return true;
            }

            handlers[event.key]();

            return false;
        };

        var present = function (item, index) {
            return {
                description: item.description,
                hasDescription: item.description !== "",
                imageUrl: item.imageUrl,
                thumbnailUrl: item.thumbnailUrl,
                open: function () {
                    self.open(index);
                }
            };
        };

        self.activate = function () {
            return galleries.get(galleryId)
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return;
                    }

                    self.title(result.data.title);
                    self.description(result.data.description);
                    self.author(meta.author(result.data.author));
                    self.authorHref(meta.authorHref(result.data.authorId));
                    self.stamp(meta.stamp(result.data));
                    self.created(meta.created(result.data));
                    self.images(result.data.items.map(present));
                })
                .finally(function () {
                    self.loading(false);
                });
        };
    };
});
