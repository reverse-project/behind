define(["ko", "models/settings", "models/session", "models/i18n", "viewmodels/emailsettings", "viewmodels/loggingsettings", "viewmodels/opentelemetrysettings", "viewmodels/oidcsettings", "viewmodels/forwardedheaderssettings"], function (ko, settings, session, i18n, EmailSettingsEditor, LoggingSettingsEditor, OpenTelemetrySettingsEditor, OidcSettingsEditor, ForwardedHeadersSettings) {
    var emailEditor = "email";
    var loggingEditor = "logging";

    var separator = ":";

    var lastSegment = function (path) {
        return path.substring(path.lastIndexOf(separator) + 1);
    };

    var sectionOf = function (path) {
        var index = path.lastIndexOf(separator);

        return index < 0 ? i18n.t("settings.general") : path.substring(0, index).split(separator).join(" \u203A ");
    };

    var initialValue = function (field) {
        if (field.type === "boolean") {
            return field.value === "true";
        }

        return field.sensitive && field.type !== "object" && field.type !== "array" ? "" : field.value;
    };

    var optionsOf = function (field) {
        var options = field.options || [];
        var known = options.some(function (option) {
            return option.value === field.value;
        });

        return known || field.value === "" ? options : options.concat([{ value: field.value, label: field.value }]);
    };

    var requestValue = function (field, value) {
        return field.type === "number" ? Number(value) : field.isStructured ? JSON.parse(value) : value;
    };

    var secretName = function (name) {
        return /password|secret|signingkey|apikey|token|credential|^connectionstring$/i.test(name);
    };

    var secretIn = function (value) {
        if (Array.isArray(value)) {
            return value.some(secretIn);
        }
        return value !== null && typeof value === "object" && Object.keys(value).some(function (key) {
            return secretName(key) || secretIn(value[key]);
        });
    };

    var structuredEditor = function (field, raw) {
        var path = field.path.toLowerCase();
        var connections = path === "connectionstrings" && field.type === "object";
        var serilog = ["serilog:writeto", "serilog:auditto", "serilog:enrich", "serilog:filter", "serilog:destructure"].indexOf(path) >= 0 && field.type === "array";
        var items = ko.observableArray([]);
        var visual = ko.observable(false);
        var supported = ko.observable(false);

        var property = function (target, key, value) {
            Object.defineProperty(target, key, { value: value, enumerable: true, writable: true, configurable: true });
        };
        var argument = function (name, value, isNew) {
            var secret = secretName(name);
            return {
                name: ko.observable(name),
                originalName: name,
                original: value,
                isNew: isNew,
                json: ko.observable(typeof value !== "string"),
                value: ko.observable(secret && !isNew ? "" : typeof value === "string" ? value : JSON.stringify(value))
            };
        };
        var makeArgument = function (name, value, isNew) {
            var arg = argument(name, value, isNew);
            arg.sensitive = ko.pureComputed(function () { return secretName(arg.originalName) || secretName(arg.name()); });
            arg.jsonSensitive = ko.pureComputed(function () {
                if (!arg.json()) { return false; }
                try { return secretIn(JSON.parse(arg.value())); } catch (error) { return true; }
            });
            return arg;
        };
        var entry = function (value) {
            if (connections) {
                return { name: ko.observable(value[0]), originalName: value[0], original: value[1], value: ko.observable(""), isNew: false };
            }
            if (typeof value === "string") {
                return { isNamed: false, value: ko.observable(value) };
            }
            return {
                isNamed: true, original: value, name: ko.observable(value.Name),
                args: ko.observableArray(Object.keys(value.Args || {}).map(function (name) {
                    return makeArgument(name, value.Args[name], false);
                }))
            };
        };
        var compatible = function (value) {
            if (connections) {
                return value !== null && typeof value === "object" && !Array.isArray(value) &&
                    Object.keys(value).every(function (key) { return value[key] === null || typeof value[key] === "string"; });
            }
            return Array.isArray(value) && value.every(function (item) {
                if (typeof item === "string") { return true; }
                return serilog && item !== null && typeof item === "object" && !Array.isArray(item) &&
                    typeof item.Name === "string" && (item.Args === undefined ||
                        (item.Args !== null && typeof item.Args === "object" && !Array.isArray(item.Args)));
            });
        };
        var reset = function (text) {
            try {
                var parsed = JSON.parse(text);
                supported(compatible(parsed));
                items(supported() ? (connections ? Object.keys(parsed).map(function (key) {
                    return entry([key, parsed[key]]);
                }) : parsed.map(entry)) : []);
            } catch (error) {
                supported(false);
                items([]);
            }
            if (!supported()) { visual(false); }
        };
        reset(raw());
        visual(supported());
        raw.subscribe(reset);
        var validNames = function (rows) {
            var seen = Object.create(null);
            return rows.every(function (row) {
                var name = row.name().trim().toLowerCase();
                if (!name || seen[name]) { return false; }
                seen[name] = true;
                return true;
            });
        };
        var valid = ko.pureComputed(function () {
            if (!visual()) { return true; }
            if (connections) {
                return validNames(items()) && items().every(function (item) { return item.name().indexOf(":") < 0; });
            }
            return items().every(function (item) {
                if (!item.isNamed) { return true; }
                return item.name().trim() !== "" && validNames(item.args()) && item.args().every(function (arg) {
                    if (!arg.isNew && secretName(arg.originalName) && arg.name() !== arg.originalName && arg.value().trim() === "") { return false; }
                    if (!arg.json() || (arg.sensitive() && !arg.isNew && arg.value().trim() === "")) { return true; }
                    try { JSON.parse(arg.value()); return true; } catch (error) { return false; }
                });
            });
        });
        var serialize = function () {
            if (connections) {
                var map = Object.create(null);
                items().forEach(function (item) {
                    property(map, item.name(), !item.isNew && item.value().trim() === "" ? item.original : item.value());
                });
                return JSON.stringify(map);
            }
            return JSON.stringify(items().map(function (item) {
                if (!item.isNamed) { return item.value(); }
                var result = JSON.parse(JSON.stringify(item.original));
                result.Name = item.name();
                if (item.original.Args !== undefined || item.args().length) {
                    var args = Object.create(null);
                    item.args().forEach(function (arg) {
                        var value = !arg.isNew && secretName(arg.originalName) && arg.value().trim() === ""
                            ? arg.original : arg.json() ? JSON.parse(arg.value()) : arg.value();
                        property(args, arg.name(), value);
                    });
                    result.Args = args;
                }
                return result;
            }));
        };
        var retainBlankSecrets = function (value, original, key) {
            if (key && secretName(key) && typeof value === "string" && value.trim() === "" && original !== undefined && original !== null) {
                return original;
            }
            if (Array.isArray(value)) {
                return value.map(function (item, index) {
                    var previous;
                    if (Array.isArray(original) && item && typeof item === "object" && !Array.isArray(item) && typeof item.Name === "string") {
                        var matches = original.filter(function (candidate) {
                            return candidate && typeof candidate === "object" && candidate.Name === item.Name;
                        });
                        var incomingMatches = value.filter(function (candidate) {
                            return candidate && typeof candidate === "object" && candidate.Name === item.Name;
                        });
                        if (matches.length === 1 && incomingMatches.length === 1) { previous = matches[0]; }
                    } else if (Array.isArray(original) && value.length === original.length &&
                        JSON.stringify(item) === JSON.stringify(original[index])) {
                        previous = original[index];
                    }
                    return retainBlankSecrets(item, previous, "");
                });
            }
            if (value !== null && typeof value === "object") {
                var result = Object.create(null);
                Object.keys(value).forEach(function (name) {
                    var previous = original && typeof original === "object" && !Array.isArray(original) ? original[name] : undefined;
                    if (connections && typeof value[name] === "string" && value[name].trim() === "" && typeof previous === "string") {
                        property(result, name, previous);
                    } else {
                        property(result, name, retainBlankSecrets(value[name], previous, name));
                    }
                });
                return result;
            }
            return value;
        };
        var current = ko.pureComputed(function () {
            if (!visual()) {
                try {
                    return JSON.stringify(retainBlankSecrets(JSON.parse(raw()), JSON.parse(field.value), ""));
                } catch (error) { return raw(); }
            }
            if (!valid()) { return raw(); }
            return serialize();
        });
        var preview = ko.pureComputed(function () {
            try {
                var redact = function (value, key, connectionMap) {
                    if (connectionMap || (key && secretName(key))) { return "••••"; }
                    if (Array.isArray(value)) {
                        return value.map(function (item) { return redact(item, "", false); });
                    }
                    if (value !== null && typeof value === "object") {
                        var result = Object.create(null);
                        Object.keys(value).forEach(function (name) {
                            property(result, name, redact(value[name], name, connections));
                        });
                        return result;
                    }
                    return value;
                };
                return JSON.stringify(field.sensitive && !connections ? "••••" : redact(JSON.parse(raw()), "", false), null, 2);
            } catch (error) {
                return "";
            }
        });
        return {
            items: items, visual: visual, supported: supported, connections: connections, serilog: serilog,
            valid: valid, current: current, preview: preview,
            toggle: function () {
                if (visual()) {
                    if (!valid()) { return; }
                    raw(serialize());
                    visual(false);
                } else {
                    reset(raw());
                    if (supported()) { visual(true); }
                }
            },
            add: function () {
                items.push(connections ? { name: ko.observable(""), originalName: "", original: "", value: ko.observable(""), isNew: true } :
                    { isNamed: false, value: ko.observable("") });
            },
            addNamed: function () { items.push({ isNamed: true, original: { Name: "" }, name: ko.observable(""), args: ko.observableArray([]) }); },
            remove: function (item) { items.remove(item); },
            addArgument: function (item) { item.args.push(makeArgument("", "", true)); },
            removeArgument: function (item, arg) { item.args.remove(arg); },
            reset: function () { reset(raw()); visual(supported()); }
        };
    };

    return function SettingsViewModel() {
        var self = this;

        self.text = i18n.t;
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
            var labels = {
                "serilog:using": "settings.serilog.assemblies",
                "serilog:enrich": "settings.serilog.enrichers",
                "serilog:writeto": "settings.serilog.sinks",
                "serilog:auditto": "settings.serilog.auditSinks",
                "serilog:filter": "settings.serilog.filters",
                "serilog:destructure": "settings.serilog.destructure"
            };
            var label = labels[field.path.toLowerCase()];
            var row = {
                path: field.path,
                label: label ? i18n.t(label) : lastSegment(field.path),
                type: field.type,
                sensitive: field.sensitive,
                editable: field.editable,
                action: field.action,
                isBoolean: field.type === "boolean",
                isNumber: field.type === "number",
                isText: field.type === "string",
                isStructured: field.type === "array" || field.type === "object",
                options: optionsOf(field),
                hasOptions: !!field.options,
                shown: field.value,
                hasShown: field.value !== "",
                original: initialValue(field),
                actionLabel: i18n.t(field.action === "rotate" ? "settings.rotate" : "settings.generate"),
                hasAction: field.action !== null
            };

            row.value = ko.observable(row.original);
            row.structured = row.isStructured ? structuredEditor(field, row.value) : null;
            row.current = ko.pureComputed(function () {
                return row.structured ? row.structured.current() : row.value();
            });
            row.valid = ko.pureComputed(function () {
                if (!row.isStructured) {
                    return true;
                }
                try {
                    var parsed = JSON.parse(row.current());
                    return row.structured.valid() && (row.type === "array" ? Array.isArray(parsed) : parsed !== null && typeof parsed === "object" && !Array.isArray(parsed));
                } catch (error) {
                    return false;
                }
            });
            row.placeholder = field.sensitive && field.editable ? i18n.t("settings.keepCurrent") : "";
            row.changed = ko.pureComputed(function () {
                var value = String(row.current()).trim();

                if (!row.editable || (row.isNumber && value === "")) { return false; }
                if (row.isStructured && !row.valid()) { return true; }
                if (row.isStructured && row.valid()) {
                    try { return JSON.stringify(JSON.parse(row.current())) !== JSON.stringify(JSON.parse(row.original)); } catch (error) { return true; }
                }
                return String(row.current()) !== String(row.original);
            });
            row.perform = function () {
                return self.runAction(field.action, row);
            };

            return row;
        };

        self.email = new EmailSettingsEditor(self.canEdit);
        self.logging = new LoggingSettingsEditor(self.canEdit);
        self.openTelemetry = new OpenTelemetrySettingsEditor(self.canEdit);
        self.forwarded = new ForwardedHeadersSettings(self.canEdit);
        self.oidc = new OidcSettingsEditor(self.canEdit);
        self.canSaveAppSettings = ko.pureComputed(function () {
            return self.canEdit() && self.selected() !== null && self.selected().isValid() && self.logging.canSave() && self.openTelemetry.canSave();
        });

        var visibleFields = function (file) {
            var hasMap = file.fields.some(function (field) {
                return field.path.toLowerCase() === "connectionstrings" && field.type === "object";
            });
            return file.fields.filter(function (field) {
                return !hasMap || field.path.toLowerCase().indexOf("connectionstrings:") !== 0;
            }).map(presentField);
        };

        var presentEditor = function (file) {
            var fields = visibleFields(file);
            return {
                name: file.name,
                editor: file.editor,
                sections: [],
                fields: fields,
                changes: ko.pureComputed(function () {
                    return fields.filter(function (field) {
                        return field.changed();
                    });
                }),
                isChanged: ko.pureComputed(function () {
                    return fields.some(function (field) {
                        return field.changed();
                    });
                }),
                isValid: ko.pureComputed(function () {
                    return fields.every(function (field) {
                        return field.valid();
                    });
                })
            };
        };

        var presentFile = function (file) {
            if (file.editor) {
                return presentEditor(file);
            }

            var fields = visibleFields(file);
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
            tab.isValid = ko.pureComputed(function () {
                return fields.every(function (field) {
                    return field.valid();
                });
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

        self.isBackendSelected = ko.pureComputed(function () {
            return self.selected() !== null && self.selected().name === "backend.json";
        });

        self.isOidcSelected = ko.pureComputed(function () {
            return self.selected() !== null && self.selected().name === "security.json";
        });

        self.isFieldTabSelected = ko.pureComputed(function () {
            return self.selected() !== null && !self.selected().editor;
        });

        var loadEditor = function (skipOidc) {
            if (!self.isBackendSelected()) {
                self.forwarded.invalidate();
            }
            if (self.isBackendSelected() && !skipOidc) {
                return self.forwarded.load().then(function (result) {
                    if (!result.ok) {
                        self.message(result.message);
                    }
                });
            }
            var loading = self.isEmailSelected()
                ? self.email.load()
                : self.isLoggingSelected()
                    ? Promise.all([
                        self.logging.load(self.selected().name),
                        self.openTelemetry.load(self.selected().name)
                    ]).then(function (results) {
                        return results.filter(function (result) {
                            return !result.ok;
                        })[0] || { ok: true };
                    })
                    : self.isOidcSelected() && !skipOidc
                        ? self.oidc.load()
                        : Promise.resolve({ ok: true });

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

        self.load = function (skipOidc) {
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

                return loadEditor(skipOidc);
            });
        };

        var perform = function (operation, reload) {
            self.busy(true);

            return operation()
                .then(function (result) {
                    self.message(result.message);

                    return result.ok ? (reload || self.load)().then(function () {
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
            if (!self.canEdit()) { return Promise.resolve(); }
            if (!tab.isValid()) {
                self.message(i18n.t("settings.invalidJson"));
                return Promise.resolve();
            }

            tab.changes().forEach(function (field) {
                values[field.path] = requestValue(field, field.current());
            });
            var modulesChanged = tab.name === "backend.json" && Object.keys(values).some(function (path) {
                return path.toLowerCase().indexOf("backend:modules:") === 0;
            });

            self.generated(null);

            return perform(function () {
                return settings.save(tab.name, values);
            }, function () {
                return self.load(true);
            }).then(function (result) {
                if (result.ok && modulesChanged) {
                    window.location.reload();
                }
                return result;
            });
        };

        self.saveForwardedHeaders = function () {
            if (!self.isBackendSelected() || !self.forwarded.canSave() ||
                !window.confirm(i18n.t("settings.forwarded.confirm"))) {
                return Promise.resolve();
            }
            var request = self.forwarded.request();
            return perform(function () {
                return settings.saveForwardedHeaders(request);
            }, function () {
                return self.isBackendSelected() ? loadEditor() : Promise.resolve();
            });
        };

        self.saveEmail = function () {
            return perform(self.email.save);
        };

        self.saveOidc = function () {
            if (!self.isOidcSelected() || !self.oidc.canSave()) {
                return Promise.resolve();
            }

            return perform(self.oidc.save, function () {
                return self.oidc.load().then(function (result) {
                    if (!result.ok) {
                        self.message(result.message);
                    }
                });
            });
        };

        self.saveAppSettings = function () {
            var tab = self.selected();
            var values = {};
            if (!self.canSaveAppSettings()) {
                if (!tab.isValid()) { self.message(i18n.t("settings.invalidJson")); }
                return Promise.resolve();
            }
            tab.changes().forEach(function (field) {
                values[field.path] = requestValue(field, field.current());
            });

            return perform(function () {
                return settings.saveAppSettings(tab.name, {
                    values: values,
                    logging: self.logging.request(),
                    openTelemetry: self.openTelemetry.request()
                });
            });
        };

        self.discard = function () {
            self.selected().fields.forEach(function (field) {
                field.value(field.original);
                if (field.structured) {
                    field.structured.reset();
                }
            });
        };

        self.discardAppSettings = function () {
            if (!self.isLoggingSelected() || !self.canEdit()) {
                return Promise.resolve();
            }
            self.discard();
            self.busy(true);
            return loadEditor().finally(function () {
                self.busy(false);
            });
        };

        var confirmations = {
            generate: "settings.confirmGenerate",
            rotate: "settings.confirmRotate"
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
            if (!window.confirm(i18n.t(confirmations[kind], [field.path]))) {
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
                } else {
                    self.forwarded.invalidate();
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
