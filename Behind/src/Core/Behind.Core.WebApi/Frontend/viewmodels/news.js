define(["ko", "models/news", "models/newsmeta", "models/pager", "models/letterpager"], function (ko, news, meta, createPager, createLetterPager) {
    var pageSize = 10;

    var plural = function (count) {
        return count === 1 ? "1 comment" : count + " comments";
    };

    var present = function (item) {
        var category = item.category;

        return {
            id: item.id,
            title: item.title,
            summary: item.summary,
            author: meta.author(item.author),
            stamp: meta.stamp(item),
            created: meta.created(item),
            hasCategory: category !== null,
            categoryName: category ? category.name : "",
            imageUrl: category && category.headerImageUrl ? category.headerImageUrl : null,
            commentLabel: plural(item.commentCount),
            isHighlighted: item.isHighlighted,
            href: "#/news/" + item.id
        };
    };

    return function NewsViewModel(context) {
        var self = this;

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.items = ko.observableArray([]);
        self.categories = ko.observableArray([]);
        self.selected = ko.observable(context.params.category || "");
        self.pager = createPager(function () {
            return self.refresh();
        });
        self.letterPager = createLetterPager(function () {
            self.pager.page(1);
            return self.refresh();
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.isEmpty = ko.pureComputed(function () {
            return self.items().length === 0 && !self.busy();
        });

        self.filters = ko.pureComputed(function () {
            return [{ id: "", name: "All" }].concat(self.categories()).map(function (category) {
                return {
                    name: category.name,
                    isSelected: self.selected() === category.id,
                    select: function () {
                        return self.choose(category.id);
                    }
                };
            });
        });

        self.banner = ko.pureComputed(function () {
            return self.categories().filter(function (category) {
                return category.id === self.selected();
            })[0] || null;
        });

        self.hasBanner = ko.pureComputed(function () {
            return self.banner() !== null && self.banner().headerImageUrl !== null;
        });

        self.bannerUrl = ko.pureComputed(function () {
            return self.hasBanner() ? self.banner().headerImageUrl : null;
        });

        self.bannerName = ko.pureComputed(function () {
            return self.banner() === null ? "" : self.banner().name;
        });

        self.refresh = function () {
            self.busy(true);

            return news.list({ page: self.pager.page(), pageSize: pageSize, category: self.selected(), letter: self.letterPager.selected() })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.items([]);
                        return undefined;
                    }

                    self.categories(result.data.categories.map(function (category) {
                        return { id: category.id, name: category.name, headerImageUrl: category.headerImageUrl };
                    }));

                    if (self.pager.isOutOfRange(result.data.news)) {
                        self.pager.clampIfOutOfRange(result.data.news);
                        return self.refresh();
                    }

                    self.letterPager.update(result.data.letters);
                    self.pager.update(result.data.news);
                    self.items(result.data.news.items.map(present));

                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.choose = function (categoryId) {
            if (categoryId === self.selected()) {
                return Promise.resolve();
            }

            self.selected(categoryId);
            self.letterPager.selected("");
            self.pager.page(1);

            return self.refresh();
        };

        self.activate = function () {
            return self.refresh();
        };
    };
});
