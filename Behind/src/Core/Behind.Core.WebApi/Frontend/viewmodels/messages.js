define(["ko", "models/session", "models/messages", "models/managedlist", "models/i18n", "bindings/richeditor"], function (ko, session, messages, createManagedList, i18n) {
    var levelKeys = {
        info: "messages.level.info",
        success: "messages.level.success",
        warning: "messages.level.warning",
        danger: "messages.level.danger"
    };
    var levels = Object.keys(levelKeys);

    var pad = function (value) {
        return value < 10 ? "0" + value : String(value);
    };

    var toLocalInput = function (iso) {
        if (!iso) {
            return "";
        }

        var date = new Date(iso);

        return date.getFullYear() + "-" + pad(date.getMonth() + 1) + "-" + pad(date.getDate()) + "T" + pad(date.getHours()) + ":" + pad(date.getMinutes());
    };

    var toIso = function (local) {
        if (!local) {
            return null;
        }

        var date = new Date(local);

        return isNaN(date.getTime()) ? null : date.toISOString();
    };

    var display = function (iso) {
        return iso ? new Date(iso).toLocaleString() : "";
    };

    return function MessagesViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Message.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Message.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Message.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.editingId = ko.observable(null);
        self.name = ko.observable("");
        self.title = ko.observable("");
        self.level = ko.observable("info");
        self.content = ko.observable("");
        self.startsAt = ko.observable("");
        self.endsAt = ko.observable("");
        self.levels = levels.map(function (level) {
            return { value: level, text: ko.pureComputed(function () {
                return i18n.t(levelKeys[level]);
            }) };
        });

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("messages.editor.edit") : i18n.t("messages.editor.new");
        });

        self.isScheduleValid = ko.pureComputed(function () {
            var start = toIso(self.startsAt());
            var end = toIso(self.endsAt());

            return start === null || end === null || start < end;
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.name().trim() !== "" && self.isScheduleValid();
        });

        var present = function (item) {
            var row = {
                id: item.id,
                name: item.name,
                title: item.title,
                level: item.level,
                levelText: i18n.t(levelKeys[item.level] || levelKeys.info),
                badge: "text-bg-" + (levels.indexOf(item.level) >= 0 ? item.level : "info"),
                startsAt: display(item.startsAt),
                endsAt: display(item.endsAt),
                isScheduled: !!item.startsAt || !!item.endsAt,
                isDeleted: item.isDeleted
            };

            row.edit = function () {
                self.list.busy(true);

                return messages.get(row.id).then(function (result) {
                    self.list.message(result.message);

                    if (result.ok) {
                        self.editingId(row.id);
                        self.name(result.data.name);
                        self.title(result.data.title);
                        self.level(result.data.level);
                        self.content(result.data.content || "");
                        self.startsAt(toLocalInput(result.data.startsAt));
                        self.endsAt(toLocalInput(result.data.endsAt));
                    }
                }).finally(function () {
                    self.list.busy(false);
                });
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("messages.confirm.delete", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return messages.remove(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return messages.restore(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("messages.confirm.hardDelete", [row.name]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return messages.hardDelete(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: messages.manage,
            pageOf: function (data) {
                return data.messages;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.name("");
            self.title("");
            self.level("info");
            self.content("");
            self.startsAt("");
            self.endsAt("");
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = {
                name: self.name().trim(),
                title: self.title().trim(),
                content: self.content(),
                level: self.level(),
                startsAt: toIso(self.startsAt()),
                endsAt: toIso(self.endsAt())
            };

            return self.list.perform(function () {
                var saving = self.isEditing() ? messages.save(self.editingId(), fields) : messages.create(fields);

                return saving.then(function (result) {
                    if (result.ok) {
                        self.reset();
                    }

                    return result;
                });
            });
        };

        self.activate = self.list.activate;
    };
});
