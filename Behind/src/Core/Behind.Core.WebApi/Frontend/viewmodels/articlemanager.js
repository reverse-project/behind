define(["ko", "models/session", "models/articles", "models/newsmeta", "models/managedlist"], function (ko, session, articles, meta, createManagedList) {
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
                stamp: meta.stamp(item),
                categoryName: item.category ? item.category.name : "",
                hasCategory: item.category !== null,
                isDeleted: item.isDeleted,
                status: item.status,
                isDraft: item.status === "draft",
                isPublished: item.status === "published",
                hasNewerDraft: item.hasNewerDraft,
                version: "Version " + item.latestVersion + ", live: " + (item.activeVersion === null ? "none" : item.activeVersion),
                href: "#/manage/articles/" + item.id
            };

            row.remove = function () {
                if (!window.confirm("Delete " + row.title + "? It stops being shown and can be restored by a developer.")) {
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
                if (!window.confirm("Permanently delete " + row.title + " with all its versions? This cannot be undone.")) {
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
