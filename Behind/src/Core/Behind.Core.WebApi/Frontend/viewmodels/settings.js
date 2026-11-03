define(["ko", "models/settings", "models/session", "viewmodels/emailsettings", "viewmodels/loggingsettings"], function (ko, settings, session, EmailSettingsEditor, LoggingSettingsEditor) {
    var emailEditor = "email";
    var loggingEditor = "logging";

    var separator = ":";

    var lastSegment = function (path) {
        return path.substring(path.lastIndexOf(separator) + 1);
    };

    var sectionOf = function (path) {
        var index = path.lastIndexOf(separator);

        return index < 0 ? "General" : path.substring(0, index).split(separator).join(" \u203A ");
    };

    var initialValue = function (field) {
        if (field.type === "boolean") {
            return field.value === "true";
        }

        return field.sensitive ? "" : field.value;
    };

    var requestValue = function (field, value) {
        return field.type === "number" ? Number(value) : value;
    };

    return function SettingsViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canView = ko.pureComputed(function () {
            return session.hasGrant("Configuration.Read");
        });
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("Configuration.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canView();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.failed = ko.observable(false);
        self.readOnly = ko.observable(false);
        self.files = ko.observableArray([]);
        self.selected = ko.observable(null);
        self.generated = ko.observable(null);

        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });
        self.canEdit = ko.pureComputed(function () {
            return self.canManage() && !self.readOnly() && !self.busy();
        });

        var presentField = function (field) {
            var row = {
                path: field.path,
                label: lastSegment(field.path),
                type: field.type,
                sensitive: field.sensitive,
                editable: field.editable,
                action: field.action,
                isBoolean: field.type === "boolean",
                isNumber: field.type === "number",
                isText: field.type === "string",
                shown: field.value,
                hasShown: field.value !== "",
                original: initialValue(field),
                actionLabel: field.action === "rotate" ? "Rotate" : "Generate",
                hasAction: field.action !== null
            };

            row.value = ko.observable(row.original);
            row.placeholder = field.sensitive && field.editable ? "Leave empty to keep the current value" : "";
            row.changed = ko.pureComputed(function () {
                var value = String(row.value()).trim();

                return row.editable && !(row.isNumber && value === "") && String(row.value()) !== String(row.original);
            });
            row.perform = function () {
                return self.runAction(field.action, row);
            };

            return row;
        };

        self.email = new EmailSettingsEditor(self.canEdit);
        self.logging = new LoggingSettingsEditor(self.canEdit);

        var presentEditor = function (file) {
            return {
                name: file.name,
                editor: file.editor,
                sections: [],
                fields: [],
                changes: ko.pureComputed(function () {
                    return [];
                }),
                isChanged: ko.pureComputed(function () {
                    return false;
                })
            };
        };

        var presentFile = function (file) {
            if (file.editor) {
                return presentEditor(file);
            }

            var fields = file.fields.map(presentField);
            var sections = [];

            fields.forEach(function (field) {
                var title = sectionOf(field.path);
                var section = sections.filter(function (item) {
                    return item.title === title;
                })[0];

                if (!section) {
                    section = { title: title, fields: [] };
                    sections.push(section);
                }

                section.fields.push(field);
            });

            var tab = { name: file.name, sections: sections, fields: fields };

            tab.editor = null;
            tab.changes = ko.pureComputed(function () {
                return fields.filter(function (field) {
                    return field.changed();
                });
            });
            tab.isChanged = ko.pureComputed(function () {
                return tab.changes().length > 0;
            });

            return tab;
        };

        var select = function (name) {
            var found = self.files().filter(function (file) {
                return file.name === name;
            })[0];

            self.selected(found || self.files()[0] || null);
        };

        self.isEmailSelected = ko.pureComputed(function () {
            return self.selected() !== null && self.selected().editor === emailEditor;
        });

        self.isLoggingSelected = ko.pureComputed(function () {
            return self.selected() !== null && self.selected().editor === loggingEditor;
        });

        self.isFieldTabSelected = ko.pureComputed(function () {
            return self.selected() !== null && !self.selected().editor;
        });

        var loadEditor = function () {
            var loading = self.isEmailSelected()
                ? self.email.load()
                : self.isLoggingSelected() ? self.logging.load(self.selected().name) : Promise.resolve({ ok: true });

            return loading.then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                }
            });
        };

        self.isSelected = function (file) {
            return self.selected() === file;
        };

        self.choose = function (file) {
            self.selected(file);
            self.generated(null);

            return loadEditor();
        };

        self.load = function () {
            var current = self.selected() ? self.selected().name : null;

            return settings.load().then(function (result) {
                self.failed(!result.ok);

                if (!result.ok) {
                    self.message(result.message);
                    return;
                }

                self.readOnly(result.data.readOnly);
                self.files(result.data.files.map(presentFile));
                select(current);

                return loadEditor();
            });
        };

        var perform = function (operation) {
            self.busy(true);

            return operation()
                .then(function (result) {
                    self.message(result.message);

                    return result.ok ? self.load().then(function () {
                        return result;
                    }) : result;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.save = function () {
            var tab = self.selected();
            var values = {};

            tab.changes().forEach(function (field) {
                values[field.path] = requestValue(field, field.value());
            });

            self.generated(null);

            return perform(function () {
                return settings.save(tab.name, values);
            });
        };

        self.saveEmail = function () {
            return perform(self.email.save);
        };

        self.saveLogging = function () {
            var name = self.selected().name;

            return perform(function () {
                return self.logging.save(name);
            });
        };

        self.discard = function () {
            self.selected().fields.forEach(function (field) {
                field.value(field.original);
            });
        };

        var confirmations = {
            generate: "Generate a new value for {0}? The current value is replaced and cannot be restored.",
            rotate: "Rotate {0}? The current secret becomes the old secret and a new one is used."
        };

        var showGenerated = function (field, data) {
            if (!data.otpUri) {
                return;
            }

            self.generated({ path: field.path, uri: data.otpUri, qrCode: ko.observable("") });
            settings.loadQrCode(data.otpUri).then(function (svg) {
                self.generated().qrCode(svg);
            });
        };

        self.runAction = function (kind, field) {
            if (!window.confirm(confirmations[kind].replace("{0}", field.path))) {
                return Promise.resolve();
            }

            self.generated(null);

            return perform(function () {
                return settings[kind](self.selected().name, field.path);
            }).then(function (result) {
                if (result.ok) {
                    showGenerated(field, result.data);
                }
            });
        };

        self.activate = function () {
            self.canView.subscribe(function (allowed) {
                if (allowed) {
                    self.refresh();
                }
            });

            return self.refresh();
        };

        self.refresh = function () {
            if (!self.canView()) {
                return Promise.resolve();
            }

            self.busy(true);

            return self.load().finally(function () {
                self.busy(false);
            });
        };
    };
});
