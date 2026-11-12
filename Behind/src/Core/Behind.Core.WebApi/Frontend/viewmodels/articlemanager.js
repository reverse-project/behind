define(["ko", "models/session", "models/articles", "models/newsmeta", "models/managedlist", "models/i18n"], function (ko, session, articles, meta, createManagedList, i18n) {
    return function ArticleManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Article.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Article.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Article.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        var present = function (item) {
            var row = {
                id: item.id,
                title: item.title,
                author: meta.author(item.author),
                authorHref: meta.authorHref(item.authorId),
                stamp: meta.stamp(item),
                categoryName: item.category ? item.category.name : "",
                hasCategory: item.category !== null,
                isDeleted: item.isDeleted,
                status: item.status,
                isDraft: item.status === "draft",
                isPublished: item.status === "published",
                hasNewerDraft: item.hasNewerDraft,
                version: meta.version(item),
                href: "#/manage/articles/" + item.id
            };

            row.remove = function () {
                if (!window.confirm(i18n.t("content.confirmDelete", [row.title]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return articles.remove(item.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return articles.restore(item.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("articles.confirmHardDelete", [row.title]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return articles.hardDelete(item.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: articles.manage,
            pageOf: function (data) {
                return data.articles;
            },
            present: present
        });

        self.activate = self.list.activate;
    };
});
