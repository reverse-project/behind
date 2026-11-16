define(["ko", "models/articles", "models/newsmeta", "models/pager", "models/i18n"], function (ko, articles, meta, createPager, i18n) {
    var pageSize = 10;

    var present = function (item) {
        var category = item.category;

        return {
            id: item.id,
            title: item.title,
            summary: item.summary,
            author: meta.author(item.author),
            authorHref: meta.authorHref(item.authorId),
            stamp: meta.stamp(item),
            created: meta.created(item),
            hasCategory: category !== null,
            categoryName: category ? category.name : "",
            imageUrl: category && category.headerImageUrl ? category.headerImageUrl : null,
            href: "#/articles/" + item.id
        };
    };

    return function ArticlesViewModel(context) {
        var self = this;

        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.items = ko.observableArray([]);
        self.categories = ko.observableArray([]);
        self.selected = ko.observable(context.params.category || "");
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

        self.filters = ko.pureComputed(function () {
            return [{ id: "", name: i18n.t("content.all") }].concat(self.categories()).map(function (category) {
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

            return articles.list({ page: self.pager.page(), pageSize: pageSize, category: self.selected(), search: self.search().trim() })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.items([]);
                        return undefined;
                    }

                    self.categories(result.data.categories.map(function (category) {
                        return { id: category.id, name: category.name, headerImageUrl: category.headerImageUrl };
                    }));

                    if (self.pager.isOutOfRange(result.data.articles)) {
                        self.pager.clampIfOutOfRange(result.data.articles);
                        return self.refresh();
                    }

                    self.pager.update(result.data.articles);
                    self.items(result.data.articles.items.map(present));

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
            self.pager.page(1);

            return self.refresh();
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
