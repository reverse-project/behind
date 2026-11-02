define(["ko", "models/session", "models/articles", "models/newsmeta", "bindings/richeditor"], function (ko, session, articles, meta) {
    var statusOf = function (item) {
        return item.isActive ? { label: "Live", css: "text-bg-success" } : { label: "Version", css: "text-bg-secondary" };
    };

    return function ArticleEditorViewModel(context) {
        var self = this;
        var articleId = context.params.id || null;

        self.isNew = articleId === null;
        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Article.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Article.Write");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.title = ko.observable("");
        self.summary = ko.observable("");
        self.content = ko.observable("");
        self.categoryId = ko.observable("");
        self.basedOn = ko.observable(null);
        self.categories = ko.observableArray([]);
        self.versions = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.heading = ko.pureComputed(function () {
            return self.isNew ? "New article" : "Edit article";
        });

        self.basedOnLabel = ko.pureComputed(function () {
            return self.basedOn() === null ? "" : "Based on version " + self.basedOn() + ". Saving creates a new version.";
        });

        self.hasVersions = ko.pureComputed(function () {
            return self.versions().length > 0;
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.title().trim() !== "";
        });

        var run = function (action) {
            self.busy(true);

            return action().finally(function () {
                self.busy(false);
            });
        };

        var present = function (item) {
            var status = statusOf(item);
            var row = {
                version: item.version,
                title: item.title,
                isActive: item.isActive,
                status: status.label,
                statusCss: status.css,
                createdAt: meta.formatDate(item.createdAt)
            };

            row.canActivate = ko.pureComputed(function () {
                return self.canWrite() && !self.busy() && !row.isActive;
            });
            row.load = function () {
                return self.loadVersion(row.version);
            };
            row.activate = function () {
                var question = "Make version " + row.version + " the live version of this article?";

                return window.confirm(question) ? self.activateVersion(row.version) : Promise.resolve();
            };

            return row;
        };

        var loadCategories = function () {
            return articles.categories().then(function (result) {
                if (result.ok) {
                    self.categories(result.data.map(function (category) {
                        return { id: category.id, name: category.name };
                    }));
                }
            });
        };

        self.loadHistory = function () {
            return articles.history(articleId).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return result;
                }

                self.versions(result.data.versions.map(present));

                return result;
            });
        };

        var readVersion = function (version) {
            return articles.version(articleId, version).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.title(result.data.title);
                self.summary(result.data.summary);
                self.content(result.data.content);
                self.categoryId(result.data.category ? result.data.category.id : "");
                self.basedOn(result.data.version);
            });
        };

        self.loadVersion = function (version) {
            return run(function () {
                return readVersion(version);
            });
        };

        self.activateVersion = function (version) {
            return run(function () {
                return articles.activate(articleId, version).then(function (result) {
                    self.message(result.message);

                    return result.ok ? self.loadHistory() : result;
                });
            });
        };

        self.save = function (publish) {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = {
                title: self.title().trim(),
                summary: self.summary(),
                content: self.content(),
                categoryId: self.categoryId(),
                publish: publish
            };

            return run(function () {
                var saving = self.isNew ? articles.create(fields) : articles.saveVersion(articleId, fields);

                return saving.then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return result;
                    }

                    if (self.isNew) {
                        context.navigate("#/manage/articles/" + result.data.id);
                        return result;
                    }

                    self.basedOn(result.data.version);

                    return self.loadHistory();
                });
            });
        };

        self.saveDraft = function () {
            return self.save(false);
        };

        self.publish = function () {
            return self.save(true);
        };

        self.uploadImage = function (file) {
            return articles.uploadImage(file).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return null;
                }

                return result.data.url;
            });
        };

        self.refresh = function () {
            if (!self.canRead()) {
                return Promise.resolve();
            }

            return run(function () {
                return loadCategories().then(function () {
                    return self.isNew
                        ? undefined
                        : self.loadHistory().then(function (result) {
                            return result.ok ? readVersion(result.data.versions[0].version) : null;
                        });
                });
            });
        };

        self.activate = function () {
            self.canRead.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };
    };
});
