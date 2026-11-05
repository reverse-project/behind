define(["ko", "models/session", "models/sites", "models/sitesections", "models/slug", "models/i18n", "bindings/richeditor"], function (ko, session, sites, sectionsModel, slug, i18n) {
    var statusOf = function (item) {
        if (item.isActive) {
            return { key: "sites.status.live", css: "text-bg-success" };
        }

        return item.isDraft ? { key: "sites.status.draft", css: "text-bg-secondary" } : { key: "sites.status.published", css: "text-bg-info" };
    };

    var sizeUnits = [
        { size: 1024 * 1024, key: "sites.unit.mb" },
        { size: 1024, key: "sites.unit.kb" },
        { size: 1, key: "sites.unit.bytes" }
    ];

    return function SiteEditorViewModel(context) {
        var self = this;
        var siteId = context.params.id || null;

        self.siteId = siteId;
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
        self.hideTitle = ko.observable(false);
        self.sections = ko.observableArray([]);
        self.maxSections = ko.observable(0);
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
            return self.isNew ? i18n.t("sites.editor.new") : i18n.t("sites.editor.edit");
        });

        self.basedOnLabel = ko.pureComputed(function () {
            return self.basedOn() === null ? "" : i18n.t("sites.editor.basedOn", [self.basedOn()]);
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
            var unit = sizeUnits.filter(function (candidate) {
                return bytes >= candidate.size;
            })[0] || sizeUnits[2];
            var amount = new Intl.NumberFormat(i18n.locale(), { maximumFractionDigits: 2 }).format(bytes / unit.size);

            return bytes > 0 ? i18n.t("sites.editor.imageLimit", [amount, i18n.t(unit.key)]) : "";
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

        self.hasEmptySection = ko.pureComputed(function () {
            return self.sections().some(function (section) {
                return !section.isFilled();
            });
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.title().trim() !== "" && slug.create(self.slug()) !== "" && !self.hasEmptySection();
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
                status: ko.pureComputed(function () {
                    return i18n.t(status.key);
                }),
                statusCss: status.css,
                publishedAt: ko.pureComputed(function () {
                    return item.publishedAt ? i18n.t("sites.editor.published", [i18n.dateTime(item.publishedAt)]) : i18n.t("sites.editor.notPublished");
                }),
                createdAt: ko.pureComputed(function () {
                    return i18n.dateTime(item.createdAt);
                })
            };

            row.canActivate = ko.pureComputed(function () {
                return self.canWrite() && !self.busy() && !row.isActive;
            });
            row.load = function () {
                return self.loadVersion(row.version);
            };
            row.activate = function () {
                var question = i18n.t("sites.editor.confirmActivate", [row.version]);

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
                self.hideTitle(result.data.hideTitle);
                self.sections(result.data.sections.map(sectionsModel.editable));
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
                hideTitle: self.hideTitle(),
                sections: self.sections().map(sectionsModel.toRequest),
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
                    self.maxSections(result.data.maxSections);
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
