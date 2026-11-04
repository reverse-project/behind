define(["ko", "models/session", "models/news", "models/newsmeta", "models/managedlist", "models/comments", "models/i18n"], function (ko, session, news, meta, createManagedList, comments, i18n) {
    return function NewsManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.commentsEnabled = ko.observable(true);
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
                version: meta.version(item),
                schedule: meta.schedule(item),
                href: "#/manage/news/" + item.id
            };

            row.remove = function () {
                if (!window.confirm(i18n.t("content.confirmDelete", [row.title]))) {
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
                if (!window.confirm(i18n.t("news.confirmHardDelete", [row.title]))) {
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

        self.activate = function () {
            return comments.settings().then(function (settings) {
                self.commentsEnabled(settings.news);

                return self.list.activate();
            });
        };
    };
});
