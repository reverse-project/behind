define(["ko", "models/session", "models/downloads", "models/downloadbrowser", "models/filetypes", "models/slug", "models/i18n"], function (ko, session, downloads, createBrowser, filetypes, slug, i18n) {
    return function DownloadManagerViewModel(context) {
        var self = this;
        var browser = null;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Download.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Download.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Download.Delete");
        });
        self.canViewDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.busy = ko.observable(false);
        self.showDeleted = ko.observable(false);
        self.deletedFolders = ko.observableArray([]);
        self.deletedFiles = ko.observableArray([]);

        self.perform = function (action) {
            if (self.busy()) {
                return Promise.resolve();
            }

            self.busy(true);
            browser.message("");

            return action()
                .then(function (result) {
                    if (result) {
                        browser.message(result.message);
                    }

                    return result;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        var loadDeleted = function () {
            if (!self.showDeleted() || !self.canViewDeleted()) {
                self.deletedFolders([]);
                self.deletedFiles([]);

                return Promise.resolve();
            }

            return downloads.deleted().then(function (result) {
                if (result.ok) {
                    self.deletedFolders(result.data.folders.map(presentDeletedFolder));
                    self.deletedFiles(result.data.files.map(browser.presentFile));
                } else {
                    browser.message(result.message);
                }
            });
        };

        var refreshAfter = function (result) {
            if (!result.ok) {
                return result;
            }

            var keep = browser.selected() ? browser.selected().id : null;

            return browser.refresh(keep, loadDeleted).then(function () {
                return result;
            });
        };

        var confirmed = function (question, action) {
            return function () {
                if (!window.confirm(question())) {
                    return Promise.resolve();
                }

                return self.perform(action);
            };
        };

        var decorate = function (row) {
            row.editing = ko.observable(false);
            row.newName = ko.observable(row.name);
            row.canRename = ko.pureComputed(function () {
                return row.newName().trim() !== "";
            });
            row.edit = function () {
                row.newName(row.name);
                row.editing(true);
            };
            row.cancel = function () {
                row.editing(false);
            };
            row.rename = function () {
                if (!row.canRename()) {
                    return Promise.resolve();
                }

                return self.perform(function () {
                    return downloads.rename(row.id, row.newName().trim()).then(refreshAfter);
                });
            };
            row.remove = confirmed(
                function () {
                    return i18n.t("downloadmanager.confirmDeleteFile", [row.name]);
                },
                function () {
                    return downloads.remove(row.id).then(refreshAfter);
                });
            row.restore = function () {
                return self.perform(function () {
                    return downloads.restore(row.id).then(refreshAfter);
                });
            };
            row.hardDelete = confirmed(
                function () {
                    return i18n.t("downloadmanager.confirmHardDeleteFile", [row.name]);
                },
                function () {
                    return downloads.hardDelete(row.id).then(refreshAfter);
                });

            return row;
        };

        var presentDeletedFolder = function (folder) {
            var row = { id: folder.id, name: folder.displayName, slug: folder.slug };

            row.restore = function () {
                return self.perform(function () {
                    return downloads.restoreFolder(row.id).then(refreshAfter);
                });
            };
            row.hardDelete = confirmed(
                function () {
                    return i18n.t("downloadmanager.confirmHardDeleteFolder", [row.name]);
                },
                function () {
                    return downloads.hardDeleteFolder(row.id).then(refreshAfter);
                });

            return row;
        };

        browser = createBrowser({ rootHref: "#/manage/downloads", params: context.params, decorate: decorate });
        self.browser = browser;

        self.folderMode = ko.observable("");
        self.folderName = ko.observable("");
        self.folderSlug = ko.observable("");
        self.folderSlugEdited = ko.observable(false);

        self.isEditingFolder = ko.pureComputed(function () {
            return self.folderMode() === "edit";
        });
        self.isFolderFormOpen = ko.pureComputed(function () {
            return self.folderMode() !== "";
        });
        self.folderFormHeading = ko.pureComputed(function () {
            if (self.isEditingFolder()) {
                return i18n.t("downloadmanager.editFolder");
            }

            return browser.selected()
                ? i18n.t("downloadmanager.newFolderIn", [browser.selected().displayName])
                : i18n.t("downloadmanager.newFolder");
        });
        self.folderSlugInput = ko.pureComputed({
            read: self.folderSlug,
            write: function (value) {
                self.folderSlugEdited(true);
                self.folderSlug(slug.clean(value));
            }
        });
        self.folderName.subscribe(function (value) {
            if (!self.folderSlugEdited()) {
                self.folderSlug(slug.create(value));
            }
        });
        self.canSaveFolder = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.folderName().trim() !== "" && slug.create(self.folderSlug()) !== "";
        });

        self.closeFolderForm = function () {
            self.folderMode("");
            self.folderName("");
            self.folderSlug("");
            self.folderSlugEdited(false);
        };
        self.startCreateFolder = function () {
            self.closeFolderForm();
            self.folderMode("create");
        };
        self.startEditFolder = function () {
            var current = browser.selected();

            if (!current) {
                return;
            }

            self.closeFolderForm();
            self.folderSlugEdited(true);
            self.folderName(current.displayName);
            self.folderSlug(current.slug);
            self.folderMode("edit");
        };

        self.saveFolder = function () {
            if (!self.canSaveFolder()) {
                return Promise.resolve();
            }

            var fields = { displayName: self.folderName().trim(), slug: slug.create(self.folderSlug()) };
            var current = browser.selected();
            var editing = self.isEditingFolder();

            return self.perform(function () {
                var saving = editing
                    ? downloads.saveFolder(current.id, fields)
                    : downloads.createFolder(current ? current.id : null, fields);

                return saving.then(function (result) {
                    if (!result.ok) {
                        return result;
                    }

                    self.closeFolderForm();

                    if (current && !editing) {
                        browser.expand(current.id);
                    }

                    return refreshAfter(result).then(function () {
                        var now = browser.selected();

                        if (editing && now && browser.hrefOf(now.node) !== window.location.hash) {
                            context.navigate(browser.hrefOf(now.node));
                        }

                        return result;
                    });
                });
            });
        };

        self.deleteFolder = function () {
            var current = browser.selected();

            if (!current || !window.confirm(i18n.t("downloadmanager.confirmDeleteFolder", [current.displayName]))) {
                return Promise.resolve();
            }

            return self.perform(function () {
                return downloads.removeFolder(current.id).then(function (result) {
                    if (result.ok) {
                        context.navigate(browser.hrefOf(current.node.parent));
                    }

                    return result;
                });
            });
        };

        self.pending = ko.observableArray([]);
        self.uploadProgress = ko.observable("");

        self.hasPending = ko.pureComputed(function () {
            return self.pending().length > 0;
        });

        var isUploadable = function (item) {
            return item.error() === "" && item.name().trim() !== "";
        };

        self.canUpload = ko.pureComputed(function () {
            return self.canWrite() && browser.inFolder() && !self.busy() && self.pending().some(isUploadable);
        });

        var presentPending = function (file) {
            var item = {
                file: file,
                fileName: file.name,
                icon: "fa-solid " + filetypes.iconOf(file.name, file.type),
                size: filetypes.sizeOf(file.size),
                name: ko.observable(file.name),
                error: ko.observable(file.size === 0
                    ? i18n.t("downloads.error.fileEmpty")
                    : browser.maxFileBytes() > 0 && file.size > browser.maxFileBytes()
                        ? i18n.t("downloadmanager.fileTooLarge", [browser.maxFileSize()])
                        : "")
            };

            item.remove = function () {
                self.pending.remove(item);
            };

            return item;
        };

        self.addFiles = function (data, event) {
            var chosen = Array.prototype.slice.call(event.target.files);

            event.target.value = "";
            chosen.map(presentPending).forEach(function (item) {
                self.pending.push(item);
            });
        };

        self.clearPending = function () {
            self.pending([]);
        };

        self.upload = function () {
            var current = browser.selected();
            var queue = self.pending().filter(isUploadable);

            if (!self.canUpload() || !current) {
                return Promise.resolve();
            }

            return self.perform(function () {
                var done = 0;

                return queue
                    .reduce(function (chain, item) {
                        return chain.then(function () {
                            self.uploadProgress(i18n.t("downloadmanager.uploadProgress", [done + 1, queue.length]));

                            return downloads.upload(current.id, item.file, item.name().trim()).then(function (result) {
                                if (result.ok) {
                                    self.pending.remove(item);
                                } else {
                                    item.error(result.message);
                                }

                                done++;
                            });
                        });
                    }, Promise.resolve())
                    .then(function () {
                        var failed = queue.filter(function (item) {
                            return self.pending.indexOf(item) >= 0;
                        }).length;

                        var outcome = failed === 0 ? i18n.t("downloadmanager.uploaded") : i18n.t("downloadmanager.uploadIncomplete");

                        return refreshAfter({ ok: true, message: outcome });
                    })
                    .finally(function () {
                        self.uploadProgress("");
                    });
            });
        };

        self.showDeleted.subscribe(function () {
            return loadDeleted();
        });

        self.activate = function () {
            return browser.activate(loadDeleted);
        };
    };
});
