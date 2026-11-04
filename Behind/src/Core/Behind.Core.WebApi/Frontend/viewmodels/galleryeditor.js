define(["ko", "models/session", "models/galleries", "models/i18n"], function (ko, session, galleries, i18n) {
    var automatic = "auto";
    var uploaded = "upload";

    var statusOf = function (item) {
        return item.isActive ? { label: "galleryeditor.statusLive", css: "text-bg-success" } : { label: "galleryeditor.statusVersion", css: "text-bg-secondary" };
    };

    var batchesOf = function (files) {
        var size = galleries.uploadBatchSize;
        var batches = [];

        for (var start = 0; start < files.length; start += size) {
            batches.push(files.slice(start, start + size));
        }

        return batches;
    };

    return function GalleryEditorViewModel(context) {
        var self = this;
        var galleryId = context.params.id || null;

        self.isNew = galleryId === null;
        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Gallery.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Gallery.Write");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });
        self.busy = ko.observable(false);
        self.uploading = ko.observable(false);
        self.progress = ko.observable("");
        self.message = ko.observable("");
        self.uploadErrors = ko.observableArray([]);
        self.title = ko.observable("");
        self.description = ko.observable("");
        self.thumbnailMode = ko.observable(automatic);
        self.uploadedThumbnailId = ko.observable(null);
        self.uploadedThumbnailUrl = ko.observable(null);
        self.basedOn = ko.observable(null);
        self.items = ko.observableArray([]);
        self.versions = ko.observableArray([]);

        self.thumbnailSources = ko.pureComputed(function () {
            return [
                { value: automatic, label: i18n.t("galleryeditor.thumbnailAutomatic") },
                { value: uploaded, label: i18n.t("galleryeditor.thumbnailUploaded") }
            ];
        });

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });

        self.hasProgress = ko.pureComputed(function () {
            return self.progress() !== "";
        });

        self.hasUploadErrors = ko.pureComputed(function () {
            return self.uploadErrors().length > 0;
        });

        self.heading = ko.pureComputed(function () {
            return self.isNew ? i18n.t("galleryeditor.newTitle") : i18n.t("galleryeditor.editTitle");
        });

        self.basedOnLabel = ko.pureComputed(function () {
            return self.basedOn() === null ? "" : i18n.t("galleryeditor.basedOn", [self.basedOn()]);
        });

        self.hasVersions = ko.pureComputed(function () {
            return self.versions().length > 0;
        });

        self.hasItems = ko.pureComputed(function () {
            return self.items().length > 0;
        });

        self.count = ko.pureComputed(function () {
            return galleries.imageCount(self.items().length);
        });

        self.automaticThumbnailUrl = ko.pureComputed(function () {
            return self.hasItems() ? self.items()[0].thumbnailUrl : null;
        });

        self.previewUrl = ko.pureComputed(function () {
            return self.thumbnailMode() === uploaded ? self.uploadedThumbnailUrl() : self.automaticThumbnailUrl();
        });

        self.hasPreview = ko.pureComputed(function () {
            return !!self.previewUrl();
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && !self.uploading() && self.title().trim() !== "";
        });

        self.canUpload = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && !self.uploading();
        });

        var run = function (action) {
            self.busy(true);

            return action().finally(function () {
                self.busy(false);
            });
        };

        var move = function (item, offset) {
            var items = self.items().slice();
            var from = items.indexOf(item);
            var to = from + offset;

            if (from < 0 || to < 0 || to >= items.length) {
                return;
            }

            items.splice(from, 1);
            items.splice(to, 0, item);
            self.items(items);
        };

        var createItem = function (data) {
            var item = {
                imageId: data.imageId,
                thumbnailId: data.thumbnailId,
                imageUrl: data.imageUrl,
                thumbnailUrl: data.thumbnailUrl,
                description: ko.observable(data.description || "")
            };

            item.canMoveUp = ko.pureComputed(function () {
                return self.items().indexOf(item) > 0;
            });
            item.canMoveDown = ko.pureComputed(function () {
                return self.items().indexOf(item) < self.items().length - 1;
            });
            item.moveUp = function () {
                move(item, -1);
            };
            item.moveDown = function () {
                move(item, 1);
            };
            item.remove = function () {
                self.items.remove(item);
            };

            return item;
        };

        var present = function (item) {
            var status = statusOf(item);
            var row = {
                version: item.version,
                title: item.title,
                count: galleries.imageCountLabel(item.itemCount),
                isActive: item.isActive,
                status: ko.pureComputed(function () {
                    return i18n.t(status.label);
                }),
                statusCss: status.css,
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
                var question = i18n.t("galleryeditor.confirmMakeLive", [row.version]);

                return window.confirm(question) ? self.activateVersion(row.version) : Promise.resolve();
            };

            return row;
        };

        self.loadHistory = function () {
            return galleries.history(galleryId).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return result;
                }

                self.versions(result.data.versions.map(present));

                return result;
            });
        };

        var readVersion = function (version) {
            return galleries.version(galleryId, version).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.title(result.data.title);
                self.description(result.data.description);
                self.thumbnailMode(result.data.thumbnailGenerated ? automatic : uploaded);
                self.uploadedThumbnailId(result.data.thumbnailGenerated ? null : result.data.thumbnailImageId);
                self.uploadedThumbnailUrl(result.data.thumbnailGenerated ? null : result.data.thumbnailUrl);
                self.items(result.data.items.map(createItem));
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
                return galleries.activate(galleryId, version).then(function (result) {
                    self.message(result.message);

                    return result.ok ? self.loadHistory() : result;
                });
            });
        };

        var uploadBatch = function (batch, summary) {
            return galleries.uploadImages(batch).then(function (result) {
                if (!result.ok) {
                    summary.errors.push(result.message);
                    return;
                }

                result.images.forEach(function (image) {
                    if (image.ok) {
                        summary.added.push(createItem(image.image));
                    } else {
                        summary.errors.push(image.fileName + ": " + image.message);
                    }
                });
            });
        };

        self.uploadFiles = function (files) {
            var batches = batchesOf(files);
            var summary = { added: [], errors: [] };
            var done = 0;

            if (!self.canUpload() || batches.length === 0) {
                return Promise.resolve();
            }

            self.uploading(true);
            self.uploadErrors([]);
            self.message("");

            return batches
                .reduce(function (chain, batch) {
                    return chain.then(function () {
                        self.progress(i18n.t("galleryeditor.uploadProgress", [done, files.length]));

                        return uploadBatch(batch, summary).then(function () {
                            done += batch.length;
                        });
                    });
                }, Promise.resolve())
                .then(function () {
                    summary.added.forEach(function (item) {
                        self.items.push(item);
                    });
                    self.uploadErrors(summary.errors);
                    self.message(summary.added.length === 0
                        ? ""
                        : i18n.t("galleryeditor.uploaded", [galleries.imageCount(summary.added.length)]));
                })
                .finally(function () {
                    self.progress("");
                    self.uploading(false);
                });
        };

        self.addFiles = function (data, event) {
            var files = Array.prototype.slice.call(event.target.files);

            event.target.value = "";

            return self.uploadFiles(files);
        };

        self.addThumbnail = function (data, event) {
            var file = event.target.files[0];

            event.target.value = "";

            if (!file || !self.canUpload()) {
                return Promise.resolve();
            }

            self.uploading(true);

            return galleries.uploadThumbnail(file)
                .then(function (result) {
                    if (!result.ok) {
                        self.message(result.message);
                        return;
                    }

                    self.uploadedThumbnailId(result.data.id);
                    self.uploadedThumbnailUrl(result.data.url);
                    self.thumbnailMode(uploaded);
                })
                .finally(function () {
                    self.uploading(false);
                });
        };

        self.save = function (publish) {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = {
                title: self.title().trim(),
                description: self.description(),
                thumbnailImageId: self.thumbnailMode() === uploaded ? self.uploadedThumbnailId() : null,
                items: self.items().map(function (item) {
                    return { imageId: item.imageId, thumbnailId: item.thumbnailId, description: item.description() };
                }),
                publish: publish
            };

            return run(function () {
                var saving = self.isNew ? galleries.create(fields) : galleries.saveVersion(galleryId, fields);

                return saving.then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        return result;
                    }

                    if (self.isNew) {
                        context.navigate("#/manage/galleries/" + result.data.id);
                        return result;
                    }

                    return readVersion(result.data.version).then(self.loadHistory);
                });
            });
        };

        self.saveDraft = function () {
            return self.save(false);
        };

        self.publish = function () {
            return self.save(true);
        };

        self.refresh = function () {
            if (!self.canRead() || self.isNew) {
                return Promise.resolve();
            }

            return run(function () {
                return self.loadHistory().then(function (result) {
                    return result.ok ? readVersion(result.data.versions[0].version) : null;
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
