define(["ko", "models/session", "models/carousels", "models/i18n", "models/contentlocale"], function (ko, session, carousels, i18n, contentLocale) {
    var states = { none: "none", live: "live", deleted: "deleted" };

    return function CarouselEditorViewModel(params) {
        var self = this;
        var siteId = ko.unwrap(params.siteId);
        self.siteId = siteId;

        self.settings = ko.observable(null);
        self.ready = ko.pureComputed(function () {
            return self.settings() !== null;
        });
        self.state = ko.observable(states.none);
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.version = ko.observable(null);
        self.intervalSeconds = ko.observable("");
        self.slides = ko.observableArray([]);

        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Site.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Site.Delete");
        });
        self.canManageDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });
        self.hasSlides = ko.pureComputed(function () {
            return self.slides().length > 0;
        });
        self.isLive = ko.pureComputed(function () {
            return self.state() === states.live;
        });
        self.isDeleted = ko.pureComputed(function () {
            return self.state() === states.deleted;
        });
        self.canAddMore = ko.pureComputed(function () {
            return self.ready() && self.slides().length < self.settings().maxSlides;
        });
        self.canUpload = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.canAddMore();
        });
        self.intervalValue = ko.pureComputed(function () {
            return Number(self.intervalSeconds());
        });
        self.intervalValid = ko.pureComputed(function () {
            var value = self.intervalValue();
            var settings = self.settings();

            return settings !== null && Number.isInteger(value) && value >= settings.minIntervalSeconds && value <= settings.maxIntervalSeconds;
        });
        self.minInterval = ko.pureComputed(function () {
            return self.ready() ? self.settings().minIntervalSeconds : 1;
        });
        self.maxInterval = ko.pureComputed(function () {
            return self.ready() ? self.settings().maxIntervalSeconds : 3600;
        });
        self.captionLimit = ko.pureComputed(function () {
            return self.ready() ? self.settings().maxCaptionLength : 300;
        });
        self.linkLimit = ko.pureComputed(function () {
            return self.ready() ? self.settings().maxLinkUrlLength : 2000;
        });
        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.hasSlides() && self.intervalValid();
        });
        self.intervalHelp = ko.pureComputed(function () {
            return i18n.t("carousels.editor.intervalHelp", [self.minInterval(), self.maxInterval()]);
        });
        self.slideLimitHelp = ko.pureComputed(function () {
            return self.ready() ? i18n.t("carousels.editor.slideLimit", [self.settings().maxSlides]) : "";
        });
        self.previewData = ko.pureComputed(function () {
            return self.hasSlides() ? {
                intervalSeconds: self.intervalValid() ? self.intervalValue() : self.settings().defaultIntervalSeconds,
                slides: self.slides().map(function (slide) {
                    return { imageUrl: slide.imageUrl, caption: slide.caption(), linkUrl: "" };
                })
            } : null;
        });

        var run = function (action) {
            self.busy(true);

            return action().finally(function () {
                self.busy(false);
            });
        };

        var present = function (item) {
            var slide = {
                id: item.id || null,
                imageId: item.imageId,
                imageUrl: item.imageUrl,
                caption: ko.observable(item.caption || ""),
                linkUrl: ko.observable(item.linkUrl || "")
            };

            slide.position = function () {
                return self.slides.indexOf(slide);
            };
            slide.canMoveUp = ko.pureComputed(function () {
                return slide.position() > 0;
            });
            slide.canMoveDown = ko.pureComputed(function () {
                return slide.position() < self.slides().length - 1;
            });
            slide.move = function (offset) {
                var index = slide.position();
                var items = self.slides().slice();

                items.splice(index, 1);
                items.splice(index + offset, 0, slide);
                self.slides(items);
            };
            slide.moveUp = function () {
                slide.move(-1);
            };
            slide.moveDown = function () {
                slide.move(1);
            };
            slide.remove = function () {
                self.slides.remove(slide);
            };

            return slide;
        };

        var show = function (data) {
            self.state(states.live);
            self.version(data.version);
            self.intervalSeconds(data.intervalSeconds);
            self.slides(data.slides.map(present));
        };

        var reset = function () {
            self.state(states.none);
            self.version(null);
            self.intervalSeconds(self.settings().defaultIntervalSeconds);
            self.slides([]);
        };

        var loadDeleted = function () {
            if (!self.canManageDeleted()) {
                return Promise.resolve();
            }

            return carousels.loadManaged(siteId, true).then(function (result) {
                if (result.ok && !result.missing) {
                    self.state(states.deleted);
                }
            });
        };

        self.load = function () {
            return carousels.loadManaged(siteId, false).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);

                    return;
                }

                if (!result.missing) {
                    show(result.data);

                    return;
                }

                reset();

                return loadDeleted();
            });
        };

        self.activate = function () {
            return carousels.settings().then(function (settings) {
                self.settings(settings);

                return run(self.load);
            });
        };

        self.addFiles = function (viewModel, event) {
            var files = Array.prototype.slice.call(event.target.files);

            event.target.value = "";

            return self.upload(files);
        };

        self.upload = function (files) {
            var allowed = files.slice(0, self.settings().maxSlides - self.slides().length);

            if (allowed.length === 0 || !self.canUpload()) {
                return Promise.resolve();
            }

            return run(function () {
                return allowed.reduce(function (previous, file) {
                    return previous.then(function (failure) {
                        if (failure) {
                            return failure;
                        }

                        return carousels.uploadImage(file).then(function (result) {
                            if (!result.ok) {
                                self.message(result.message);

                                return result;
                            }

                            self.message("");
                            self.slides.push(present({ imageId: result.data.id, imageUrl: result.data.url, caption: "", linkUrl: "" }));

                            return null;
                        });
                    });
                }, Promise.resolve(null));
            });
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = {
                intervalSeconds: self.intervalValue(),
                slides: self.slides().map(function (slide) {
                    return { id: slide.id, imageId: slide.imageId, caption: slide.caption().trim(), linkUrl: slide.linkUrl().trim() };
                })
            };

            return run(function () {
                return carousels.save(siteId, fields).then(function (result) {
                    self.message(result.message);

                    if (result.ok) {
                        contentLocale.clearMedia();
                    }
                    return result.ok ? self.load() : result;
                });
            });
        };

        var perform = function (action) {
            return run(function () {
                return action().then(function (result) {
                    self.message(result.message);

                    return result.ok ? self.load() : result;
                });
            });
        };

        self.remove = function () {
            if (!window.confirm(i18n.t("carousels.confirm.delete"))) {
                return Promise.resolve();
            }

            return perform(function () {
                return carousels.remove(siteId);
            });
        };

        self.restore = function () {
            return perform(function () {
                return carousels.restore(siteId);
            });
        };

        self.hardDelete = function () {
            if (!window.confirm(i18n.t("carousels.confirm.hardDelete"))) {
                return Promise.resolve();
            }

            return perform(function () {
                return carousels.hardDelete(siteId);
            });
        };

        self.activate();
    };
});
