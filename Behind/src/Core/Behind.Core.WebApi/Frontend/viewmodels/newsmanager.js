define(["ko", "models/session", "models/news", "models/newsmeta", "models/managedlist"], function (ko, session, news, meta, createManagedList) {
    return function NewsManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("News.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("News.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("News.Delete");
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
                commentCount: item.commentCount,
                isDeleted: item.isDeleted,
                isHighlighted: item.isHighlighted,
                status: item.status,
                isDraft: item.status === "draft",
                isScheduled: item.status === "scheduled",
                isExpired: item.status === "expired",
                hasNewerDraft: item.hasNewerDraft,
                version: "Version " + item.latestVersion + ", live: " + (item.activeVersion === null ? "none" : item.activeVersion),
                schedule: item.startAt ? "Starts " + meta.formatDate(item.startAt) + (item.endAt ? ", ends " + meta.formatDate(item.endAt) : "") : "",
                href: "#/manage/news/" + item.id
            };

            row.remove = function () {
                if (!window.confirm("Delete " + row.title + "? It stops being shown and can be restored by a developer.")) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return news.remove(item.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return news.restore(item.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm("Permanently delete " + row.title + " with all its comments? This cannot be undone.")) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return news.hardDelete(item.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: news.manage,
            pageOf: function (data) {
                return data.news;
            },
            present: present
        });

        self.activate = self.list.activate;
    };
});
