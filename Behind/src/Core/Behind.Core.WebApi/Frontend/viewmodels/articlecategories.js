define(["ko", "models/session", "models/articles", "models/managedlist", "models/i18n"], function (ko, session, articles, createManagedList, i18n) {
    return function ArticleCategoriesViewModel() {
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

        self.editingId = ko.observable(null);
        self.name = ko.observable("");
        self.headerImageId = ko.observable(null);
        self.headerImageUrl = ko.observable(null);
        self.uploadMessage = ko.observable("");

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("content.editCategory") : i18n.t("content.newCategory");
        });

        self.hasHeaderImage = ko.pureComputed(function () {
            return self.headerImageUrl() !== null;
        });

        self.hasUploadMessage = ko.pureComputed(function () {
            return self.uploadMessage() !== "";
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.name().trim() !== "";
        });

        var present = function (item) {
            var row = {
                id: item.id,
                name: item.name,
                headerImageId: item.headerImageId,
                headerImageUrl: item.headerImageUrl,
                hasImage: item.headerImageUrl !== null,
                isDeleted: item.isDeleted
            };

            row.edit = function () {
                self.editingId(row.id);
                self.name(row.name);
                self.headerImageId(row.headerImageId);
                self.headerImageUrl(row.headerImageUrl);
                self.uploadMessage("");
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("articles.confirmDeleteCategory", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return articles.removeCategory(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return articles.restoreCategory(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("content.confirmHardDeleteCategory", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return articles.hardDeleteCategory(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: articles.manageCategories,
            pageOf: function (data) {
                return data.categories;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.name("");
            self.headerImageId(null);
            self.headerImageUrl(null);
            self.uploadMessage("");
        };

        self.removeImage = function () {
            self.headerImageId(null);
            self.headerImageUrl(null);
        };

        self.chooseImage = function (viewModel, event) {
            var file = event.target.files[0];

            if (!file) {
                return Promise.resolve();
            }

            self.list.busy(true);

            return articles.uploadImage(file)
                .then(function (result) {
                    self.uploadMessage(result.message);

                    if (result.ok) {
                        self.headerImageId(result.data.id);
                        self.headerImageUrl(result.data.url);
                    }
                })
                .finally(function () {
                    event.target.value = "";
                    self.list.busy(false);
                });
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = { name: self.name().trim(), headerImageId: self.headerImageId() };

            return self.list.perform(function () {
                var saving = self.isEditing() ? articles.saveCategory(self.editingId(), fields) : articles.createCategory(fields);

                return saving.then(function (result) {
                    if (result.ok) {
                        self.reset();
                    }

                    return result;
                });
            });
        };

        self.activate = self.list.activate;
    };
});
