define(["ko", "models/session", "models/sites", "models/slug", "bindings/richeditor"], function (ko, session, sites, slug) {
    var formatDate = function (value) {
        return value ? new Date(value).toLocaleString() : "";
    };

    var statusOf = function (item) {
        if (item.isActive) {
            return { label: "Live", css: "text-bg-success" };
        }

        return item.isDraft ? { label: "Draft", css: "text-bg-secondary" } : { label: "Published", css: "text-bg-info" };
    };

    return function SiteEditorViewModel(context) {
        var self = this;
        var siteId = context.params.id || null;

        self.isNew = siteId === null;
        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Site.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Site.Write");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.title = ko.observable("");
        self.slug = ko.observable("");
        self.slugEdited = ko.observable(!self.isNew);
        self.content = ko.observable("");
        self.basedOn = ko.observable(null);
        self.versions = ko.observableArray([]);
        self.indexSlug = ko.observable("");
        self.maxImageBytes = ko.observable(0);

        self.slugInput = ko.pureComputed({
            read: self.slug,
            write: function (value) {
                self.slugEdited(true);
                self.slug(slug.clean(value));
            }
        });

        self.title.subscribe(function (value) {
            if (!self.slugEdited()) {
                self.slug(slug.create(value));
            }
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.heading = ko.pureComputed(function () {
            return self.isNew ? "New site" : "Edit site";
        });

        self.basedOnLabel = ko.pureComputed(function () {
            return self.basedOn() === null ? "" : "Based on version " + self.basedOn() + ". Saving creates a new version.";
        });

        self.hasVersions = ko.pureComputed(function () {
            return self.versions().length > 0;
        });

        self.live = ko.pureComputed(function () {
            return self.versions().filter(function (version) {
                return version.isActive;
            })[0] || null;
        });

        self.isIndexSlug = ko.pureComputed(function () {
            return self.indexSlug() !== "" && slug.create(self.slug()) === self.indexSlug();
        });

        self.isLiveIndex = ko.pureComputed(function () {
            return self.hasLive() && self.live().slug === self.indexSlug();
        });

        self.imageLimitLabel = ko.pureComputed(function () {
            var bytes = self.maxImageBytes();
            var units = [[1024 * 1024, "MB"], [1024, "KB"], [1, "bytes"]];
            var unit = units.filter(function (candidate) {
                return bytes >= candidate[0];
            })[0] || units[2];

            return bytes > 0 ? "Images can be up to " + parseFloat((bytes / unit[0]).toFixed(2)) + " " + unit[1] + "." : "";
        });

        self.hasLive = ko.pureComputed(function () {
            return self.live() !== null;
        });

        self.primaryPath = ko.pureComputed(function () {
            return self.hasLive() ? "#/" + self.live().slug : "";
        });

        self.fallbackPath = ko.pureComputed(function () {
            return self.hasLive() ? "#/site/" + self.live().slug : "";
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.title().trim() !== "" && slug.create(self.slug()) !== "";
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
                slug: item.slug,
                isActive: item.isActive,
                isIndex: ko.pureComputed(function () {
                    return item.isActive && self.indexSlug() !== "" && item.slug === self.indexSlug();
                }),
                status: status.label,
                statusCss: status.css,
                publishedAt: item.publishedAt ? "Published " + formatDate(item.publishedAt) : "Not published",
                createdAt: formatDate(item.createdAt)
            };

            row.canActivate = ko.pureComputed(function () {
                return self.canWrite() && !self.busy() && !row.isActive;
            });
            row.load = function () {
                return self.loadVersion(row.version);
            };
            row.activate = function () {
                var question = "Make version " + row.version + " the live version of this site?";

                return window.confirm(question) ? self.activateVersion(row.version) : Promise.resolve();
            };

            return row;
        };

        self.loadHistory = function () {
            return sites.history(siteId).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return result;
                }

                self.versions(result.data.versions.map(present));

                return result;
            });
        };

        var readVersion = function (version) {
            return sites.version(siteId, version).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.slugEdited(true);
                self.title(result.data.title);
                self.slug(result.data.slug);
                self.content(result.data.content);
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
                return sites.activate(siteId, version).then(function (result) {
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
                slug: slug.create(self.slug()),
                content: self.content(),
                publish: publish
            };

            return run(function () {
                var saving = self.isNew ? sites.create(fields) : sites.saveVersion(siteId, fields);

                return saving.then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return result;
                    }

                    if (self.isNew) {
                        context.navigate("#/sites/" + result.data.id);
                        return result;
                    }

                    self.slug(fields.slug);
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

        self.resetSlug = function () {
            self.slugEdited(false);
            self.slug(slug.create(self.title()));
        };

        self.uploadImage = function (file) {
            return sites.uploadImage(file).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return null;
                }

                return result.data.url;
            });
        };

        var loadSettings = function () {
            return sites.settings().then(function (result) {
                if (result.ok) {
                    self.indexSlug(result.data.indexSlug || "");
                    self.maxImageBytes(result.data.maxImageBytes);
                }
            });
        };

        self.refresh = function () {
            if (!self.canRead()) {
                return Promise.resolve();
            }

            return run(function () {
                var settings = loadSettings();

                return self.isNew
                    ? settings
                    : Promise.all([
                        settings,
                        self.loadHistory().then(function (result) {
                            return result.ok ? readVersion(result.data.versions[0].version) : null;
                        })
                    ]);
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
