define(["ko", "models/articles", "models/newsmeta"], function (ko, articles, meta) {
    return function ArticleReaderViewModel(context) {
        var self = this;
        var articleId = context.params.id;

        self.loading = ko.observable(true);
        self.message = ko.observable("");
        self.title = ko.observable("");
        self.content = ko.observable("");
        self.author = ko.observable("");
        self.authorHref = ko.observable(null);
        self.stamp = ko.observable("");
        self.created = ko.observable("");
        self.categoryName = ko.observable("");
        self.imageUrl = ko.observable(null);

        self.found = ko.pureComputed(function () {
            return !self.loading() && self.message() === "";
        });

        self.missing = ko.pureComputed(function () {
            return !self.loading() && self.message() !== "";
        });

        self.hasCategory = ko.pureComputed(function () {
            return self.categoryName() !== "";
        });

        self.hasImage = ko.pureComputed(function () {
            return self.imageUrl() !== null;
        });

        self.activate = function () {
            return articles.get(articleId)
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return;
                    }

                    var category = result.data.category;

                    self.title(result.data.title);
                    self.content(result.data.displayContent);
                    self.author(meta.author(result.data.author));
                    self.authorHref(meta.authorHref(result.data.authorId));
                    self.stamp(meta.stamp(result.data));
                    self.created(meta.created(result.data));
                    self.categoryName(category ? category.name : "");
                    self.imageUrl(category && category.headerImageUrl ? category.headerImageUrl : null);
                })
                .finally(function () {
                    self.loading(false);
                });
        };
    };
});
