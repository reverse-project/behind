define(["ko", "models/session", "models/news", "models/newsmeta", "bindings/richeditor"], function (ko, session, news, meta) {
    var statusOf = function (item) {
        return item.isActive ? { label: "Live", css: "text-bg-success" } : { label: "Version", css: "text-bg-secondary" };
    };

    return function NewsEditorViewModel(context) {
        var self = this;
        var newsId = context.params.id || null;

        self.isNew = newsId === null;
        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("News.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("News.Write");
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
        self.highlighted = ko.observable(false);
        self.scheduled = ko.observable(false);
        self.startAt = ko.observable("");
        self.endAt = ko.observable("");
        self.basedOn = ko.observable(null);
        self.categories = ko.observableArray([]);
        self.versions = ko.observableArray([]);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.heading = ko.pureComputed(function () {
            return self.isNew ? "New news" : "Edit news";
        });

        self.basedOnLabel = ko.pureComputed(function () {
            return self.basedOn() === null ? "" : "Based on version " + self.basedOn() + ". Saving creates a new version.";
        });

        self.hasVersions = ko.pureComputed(function () {
            return self.versions().length > 0;
        });

        self.scheduled.subscribe(function (enabled) {
            if (enabled && self.startAt() === "") {
                self.startAt(meta.toLocalInput(new Date().toISOString()));
            }
        });

        self.hasValidSchedule = ko.pureComputed(function () {
            if (!self.scheduled()) {
                return true;
            }

            if (self.startAt() === "") {
                return false;
            }

            return self.endAt() === "" || new Date(self.endAt()) > new Date(self.startAt());
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.title().trim() !== "" && self.hasValidSchedule();
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
                isHighlighted: item.isHighlighted,
                status: status.label,
                statusCss: status.css,
                schedule: item.startAt
                    ? "Starts " + meta.formatDate(item.startAt) + (item.endAt ? ", ends " + meta.formatDate(item.endAt) : "")
                    : "No schedule",
                createdAt: meta.formatDate(item.createdAt)
            };

            row.canActivate = ko.pureComputed(function () {
                return self.canWrite() && !self.busy() && !row.isActive;
            });
            row.load = function () {
                return self.loadVersion(row.version);
            };
            row.activate = function () {
                var question = "Make version " + row.version + " the live version of this news?";

                return window.confirm(question) ? self.activateVersion(row.version) : Promise.resolve();
            };

            return row;
        };

        var loadCategories = function () {
            return news.categories().then(function (result) {
                if (result.ok) {
                    self.categories(result.data.map(function (category) {
                        return { id: category.id, name: category.name };
                    }));
                }
            });
        };

        self.loadHistory = function () {
            return news.history(newsId).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return result;
                }

                self.versions(result.data.versions.map(present));

                return result;
            });
        };

        var readVersion = function (version) {
            return news.version(newsId, version).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.title(result.data.title);
                self.summary(result.data.summary);
                self.content(result.data.content);
                self.categoryId(result.data.category ? result.data.category.id : "");
                self.highlighted(result.data.isHighlighted);
                self.scheduled(!!result.data.startAt);
                self.startAt(meta.toLocalInput(result.data.startAt));
                self.endAt(meta.toLocalInput(result.data.endAt));
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
                return news.activate(newsId, version).then(function (result) {
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
                highlighted: self.highlighted(),
                startAt: self.scheduled() ? meta.fromLocalInput(self.startAt()) : null,
                endAt: self.scheduled() ? meta.fromLocalInput(self.endAt()) : null,
                publish: publish
            };

            return run(function () {
                var saving = self.isNew ? news.create(fields) : news.saveVersion(newsId, fields);

                return saving.then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return result;
                    }

                    if (self.isNew) {
                        context.navigate("#/manage/news/" + result.data.id);
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
            return news.uploadImage(file).then(function (result) {
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
