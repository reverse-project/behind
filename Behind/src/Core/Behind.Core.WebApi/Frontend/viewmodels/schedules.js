define(["ko", "models/session", "models/schedules", "models/managedlist", "models/i18n", "models/cronfields"], function (ko, session, schedules, createManagedList, i18n, cronfields) {
    var dateTime = function (value, utc) {
        return value ? new Date(value).toLocaleString(undefined, utc ? { timeZone: "UTC", timeZoneName: "short" } : undefined) : "";
    };
    var notifyKeys = { never: "schedules.notify.never", always: "schedules.notify.always", success: "schedules.notify.success", failure: "schedules.notify.failure" };
    var modeKeys = { every: "schedules.cron.mode.every", interval: "schedules.cron.mode.interval", specific: "schedules.cron.mode.specific", range: "schedules.cron.mode.range" };
    var fieldKeys = { minute: "schedules.cron.field.minute", hour: "schedules.cron.field.hour", dayOfMonth: "schedules.cron.field.dayOfMonth", month: "schedules.cron.field.month", dayOfWeek: "schedules.cron.field.dayOfWeek" };
    var validEmail = function (value) {
        return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
    };

    return function SchedulesViewModel() {
        var self = this;
        var suppressCron = false;
        var suppressAction = false;
        var cronTimer = null;
        var participantTimer = null;
        var cronRequest = 0;
        var participantRequest = 0;
        var actionDefinitions = [];

        self.isSignedIn = session.isSignedIn;
        self.i18n = i18n;
        self.canRead = ko.pureComputed(function () { return session.hasGrant("Schedule.Read"); });
        self.canWrite = ko.pureComputed(function () { return session.hasGrant("Schedule.Write"); });
        self.canDelete = ko.pureComputed(function () { return session.hasGrant("Schedule.Delete"); });
        self.canManage = ko.pureComputed(function () { return session.hasGrant("Schedule.Manage"); });
        self.canRawManage = ko.pureComputed(function () { return session.hasGrant("RawDatabase.Manage"); });
        self.isDenied = ko.pureComputed(function () { return session.isSignedIn() && session.grants() !== null && !self.canRead(); });
        self.editingId = ko.observable(null);
        self.editingVersion = ko.observable(null);
        self.name = ko.observable("");
        self.enabled = ko.observable(true);
        self.cron = ko.observable("* * * * *");
        self.actions = ko.observableArray([]);
        self.action = ko.observable("");
        self.parameterFields = ko.observableArray([]);
        self.parameters = ko.observable({});
        self.notify = ko.observable("never");
        self.participants = ko.observableArray([]);
        self.roles = ko.observableArray([]);
        self.roleId = ko.observable("");
        self.userSearch = ko.observable("");
        self.users = ko.observableArray([]);
        self.email = ko.observable("");
        self.activeTab = ko.observable("schedule");
        self.customCron = ko.observable(false);
        self.cronResult = ko.observable(null);
        self.cronMessage = ko.observable("");
        self.busy = ko.observable(false);
        self.saveMessage = ko.observable("");
        self.saveSucceeded = ko.observable(false);
        self.notifyOptions = ["never", "always", "success", "failure"].map(function (value) {
            return { value: value, label: ko.pureComputed(function () { return i18n.t(notifyKeys[value]); }) };
        });
        self.modes = ["every", "interval", "specific", "range"].map(function (value) {
            return { value: value, label: ko.pureComputed(function () { return i18n.t(modeKeys[value]); }) };
        });
        self.fields = cronfields.names.map(function (key) {
            return { key: key, label: ko.pureComputed(function () { return i18n.t(fieldKeys[key]); }), mode: ko.observable("every"), value: ko.observable("") };
        });
        self.isEditing = ko.pureComputed(function () { return self.editingId() !== null; });
        self.heading = ko.pureComputed(function () { return i18n.t(self.isEditing() ? "schedules.editor.edit" : "schedules.editor.new"); });
        self.isCronValid = ko.pureComputed(function () { return !!(self.cronResult() && self.cronResult().valid); });
        self.nextRuns = ko.pureComputed(function () {
            return self.cronResult() && self.cronResult().next.map(function (value) {
                return dateTime(value, true);
            }) || [];
        });
        self.canSave = ko.pureComputed(function () {
            var requiredParametersValid = self.parameterFields().every(function (field) {
                return !field.required || String(field.value()).trim() !== "";
            });
            return self.canWrite() && !self.busy() && self.name().trim() !== "" && self.action() !== "" && self.isCronValid() && requiredParametersValid;
        });
        self.canAddEmail = ko.pureComputed(function () { return validEmail(self.email().trim()) && !self.hasParticipant("email", self.email().trim()); });

        self.hasParticipant = function (kind, value) {
            var normalized = String(value).trim().toLowerCase();
            return self.participants().some(function (participant) {
                return participant.kind === kind && String(participant.value).trim().toLowerCase() === normalized;
            });
        };

        var validateCron = function () {
            var request = ++cronRequest;
            self.cronResult(null);
            self.cronMessage("");
            if (cronTimer !== null) {
                window.clearTimeout(cronTimer);
            }
            var expression = self.cron().trim();
            if (!expression) {
                self.cronResult(null);
                return;
            }
            cronTimer = window.setTimeout(function () {
                schedules.cron(expression, 5).then(function (result) {
                    if (request !== cronRequest) {
                        return;
                    }
                    if (result.ok) {
                        self.cronResult(result.data);
                        self.cronMessage(result.data.valid ? "" : i18n.t("schedules.conflict.invalidCron"));
                    } else {
                        self.cronResult({ valid: false, next: [] });
                        self.cronMessage(result.message);
                    }
                });
            }, 250);
        };

        var writeCalculator = function () {
            if (suppressCron) {
                return;
            }
            self.customCron(false);
            var values = {};
            self.fields.forEach(function (field) {
                values[field.key] = { mode: field.mode(), value: field.value() };
            });
            suppressCron = true;
            self.cron(cronfields.compose(values));
            suppressCron = false;
        };

        var readCalculator = function (expression) {
            var parsed = cronfields.parse(expression);
            suppressCron = true;
            if (parsed) {
                self.customCron(false);
                self.fields.forEach(function (field) {
                    field.mode(parsed[field.key].mode);
                    field.value(parsed[field.key].value);
                });
            } else {
                self.customCron(true);
            }
            suppressCron = false;
        };

        self.cron.subscribe(function (expression) {
            if (!suppressCron) {
                readCalculator(expression);
            }
            validateCron();
        });
        self.fields.forEach(function (field) {
            field.mode.subscribe(writeCalculator);
            field.value.subscribe(writeCalculator);
        });

        var definition = function (key) {
            return actionDefinitions.find(function (item) { return item.key === key; });
        };

        var loadParameterFields = function (actionKey, values) {
            var selected = definition(actionKey);
            if (!selected) {
                self.parameterFields([]);
                self.parameters({});
                return Promise.resolve();
            }
            var fields = selected.parameters.map(function (parameter) {
                var field = {
                    name: parameter.name,
                    label: ko.pureComputed(function () { return i18n.tOr(parameter.labelKey, parameter.name); }),
                    kind: parameter.kind,
                    required: parameter.required,
                    defaultValue: parameter.defaultValue || "",
                    placeholders: parameter.placeholders || [],
                    minimum: parameter.minimum === undefined ? null : parameter.minimum,
                    maximum: parameter.maximum === undefined ? null : parameter.maximum,
                    value: ko.observable(Object.prototype.hasOwnProperty.call(values || {}, parameter.name) ? values[parameter.name] : parameter.defaultValue || ""),
                    options: ko.observableArray([])
                };
                field.value.subscribe(function (value) {
                    var current = self.parameters();
                    var next = Object.assign({}, current);
                    next[field.name] = value;
                    self.parameters(next);
                });
                return field;
            });
            self.parameters({});
            fields.forEach(function (field) {
                var next = Object.assign({}, self.parameters());
                next[field.name] = field.value();
                self.parameters(next);
            });
            self.parameterFields(fields);
            return Promise.all(fields.filter(function (field) { return field.kind === "choice"; }).map(function (field) {
                return schedules.actionOptions(actionKey, field.name).then(function (result) {
                    field.options(result.ok ? result.data : []);
                    if (!result.ok) {
                        self.list.message(result.message);
                    }
                });
            }));
        };

        self.action.subscribe(function (key) {
            if (!suppressAction) {
                loadParameterFields(key, {});
            }
        });

        self.insertPlaceholder = function (field, placeholder, event) {
            var input = event.currentTarget.closest(".behind-schedule-parameter").querySelector(".behind-schedule-template");
            var token = "{{" + placeholder + "}}";
            var current = String(field.value() || "");
            var start = Math.min(input.selectionStart === null ? current.length : input.selectionStart, current.length);
            var end = Math.max(start, Math.min(input.selectionEnd === null ? start : input.selectionEnd, current.length));
            field.value(current.slice(0, start) + token + current.slice(end));
            window.setTimeout(function () {
                input.focus();
                input.setSelectionRange(start + token.length, start + token.length);
            }, 0);
        };
        self.resetParameter = function (field) { field.value(field.defaultValue); };

        self.addRole = function () {
            var role = self.roles().find(function (item) { return item.id === self.roleId(); });
            if (role && !self.hasParticipant("role", role.id)) {
                self.participants.push({ kind: "role", value: role.id, label: role.name });
            }
            self.roleId("");
        };
        self.addUser = function (user) {
            if (!self.hasParticipant("user", user.id)) {
                self.participants.push({ kind: "user", value: user.id, label: user.label });
            }
            self.userSearch("");
            self.users([]);
        };
        self.addEmail = function () {
            var value = self.email().trim();
            if (validEmail(value) && !self.hasParticipant("email", value)) {
                self.participants.push({ kind: "email", value: value, label: value });
                self.email("");
            }
        };
        self.emailKeyDown = function (data, event) {
            if (event.key !== "Enter") {
                return true;
            }
            self.addEmail();
            return false;
        };
        self.removeParticipant = function (participant) { self.participants.remove(participant); };

        self.userSearch.subscribe(function (search) {
            var request = ++participantRequest;
            if (participantTimer !== null) {
                window.clearTimeout(participantTimer);
            }
            if (!search.trim()) {
                self.users([]);
                return;
            }
            participantTimer = window.setTimeout(function () {
                schedules.participants(search.trim()).then(function (result) {
                    if (request === participantRequest) {
                        self.users(result.ok ? result.data.users : []);
                        if (!result.ok) {
                            self.list.message(result.message);
                        }
                    }
                });
            }, 250);
        });

        var present = function (item) {
            var row = {
                id: item.id,
                name: item.name,
                cron: item.cron,
                actionName: ko.pureComputed(function () {
                    var action = definition(item.action);
                    return action ? i18n.tOr(action.nameKey, item.action) : item.action;
                }),
                enabled: item.enabled,
                nextRunAt: dateTime(item.nextRunAt, true),
                isDeleted: item.isDeleted
            };
            row.edit = function () {
                self.list.busy(true);
                return schedules.get(row.id).then(function (result) {
                    self.list.message(result.message);
                    if (result.ok) {
                        var data = result.data;
                        self.saveMessage("");
                        self.editingId(data.id);
                        self.editingVersion(data.version);
                        self.name(data.name);
                        self.enabled(data.enabled);
                        self.cron(data.cron);
                        self.notify(data.notify);
                        self.participants(data.participants || []);
                        suppressAction = true;
                        self.action(data.action);
                        suppressAction = false;
                        return loadParameterFields(data.action, data.parameters || {}).then(function () {
                            self.activeTab("schedule");
                        });
                    }
                    return undefined;
                }).finally(function () { self.list.busy(false); });
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("schedules.confirm.delete", [row.name]))) { return Promise.resolve(); }
                return self.list.perform(function () { return schedules.remove(row.id); });
            };
            row.restore = function () { return self.list.perform(function () { return schedules.restore(row.id); }); };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("schedules.confirm.hardDelete", [row.name]))) { return Promise.resolve(); }
                return self.list.perform(function () { return schedules.hardDelete(row.id); });
            };
            row.run = function () {
                if (!window.confirm(i18n.t("schedules.confirm.run", [row.name]))) { return Promise.resolve(); }
                return self.list.perform(function () { return schedules.run(row.id); });
            };
            row.runsHref = "#/manage/schedules/runs?schedule=" + encodeURIComponent(row.id);
            return row;
        };

        self.list = createManagedList({ allowed: self.canRead, load: schedules.manage, pageOf: function (data) { return data.schedules; }, present: present });
        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.editingVersion(null);
            self.name("");
            self.enabled(true);
            self.cron("* * * * *");
            self.action("");
            self.parameters({});
            self.parameterFields([]);
            self.notify("never");
            self.participants([]);
            self.activeTab("schedule");
            self.saveMessage("");
        };
        self.save = function () {
            if (!self.canSave()) { return Promise.resolve(); }
            var fields = {
                name: self.name().trim(),
                cron: self.cron().trim(),
                action: self.action(),
                parameters: self.parameters(),
                enabled: self.enabled(),
                notify: self.notify(),
                participants: self.participants().map(function (participant) { return { kind: participant.kind, value: participant.value }; })
            };
            if (self.isEditing()) {
                fields.version = self.editingVersion();
            }
            self.saveMessage("");
            return self.list.perform(function () {
                var action = self.isEditing() ? schedules.save(self.editingId(), fields) : schedules.create(fields);
                return action.then(function (result) {
                    if (result.ok) {
                        self.editingId(result.data.id);
                        self.editingVersion(result.data.version);
                    }
                    self.saveSucceeded(result.ok);
                    self.saveMessage(result.message);
                    return result;
                });
            });
        };
        self.preset = function (expression) {
            self.cron(expression);
            validateCron();
        };
        self.addRoleOptions = function () {
            schedules.participants("").then(function (result) {
                if (result.ok) { self.roles(result.data.roles); }
                else { self.list.message(result.message); }
            });
        };
        self.activate = function () {
            var loadingActions = schedules.actions().then(function (result) {
                if (result.ok) {
                    actionDefinitions = result.data;
                    self.actions(actionDefinitions.map(function (item) {
                        return { key: item.key, name: ko.pureComputed(function () { return i18n.tOr(item.nameKey, item.key); }) };
                    }));
                } else {
                    self.list.message(result.message);
                }
            });
            if (self.canWrite()) {
                self.addRoleOptions();
            }
            validateCron();
            return loadingActions.then(function () { return self.list.activate(); });
        };
    };
});
