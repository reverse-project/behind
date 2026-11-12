define(["ko", "models/session", "models/galleries", "models/newsmeta", "models/managedlist", "models/i18n"], function (ko, session, galleries, meta, createManagedList, i18n) {
    return function GalleryManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Gallery.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Gallery.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Gallery.Delete");
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
                thumbnailUrl: item.thumbnailUrl,
                hasThumbnail: !!item.thumbnailUrl,
                count: galleries.imageCountLabel(item.itemCount),
                isDeleted: item.isDeleted,
                status: item.status,
                isDraft: item.status === "draft",
                isPublished: item.status === "published",
                hasNewerDraft: item.hasNewerDraft,
                version: ko.pureComputed(function () {
                    return i18n.t("gallerymanager.version", [item.latestVersion, item.activeVersion === null ? i18n.t("gallerymanager.noLiveVersion") : item.activeVersion]);
                }),
                href: "#/manage/galleries/" + item.id
            };

            row.remove = function () {
                if (!window.confirm(i18n.t("gallerymanager.confirmDelete", [row.title]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return galleries.remove(item.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return galleries.restore(item.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("gallerymanager.confirmHardDelete", [row.title]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return galleries.hardDelete(item.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: galleries.manage,
            pageOf: function (data) {
                return data.galleries;
            },
            present: present
        });

        self.activate = self.list.activate;
    };
});
