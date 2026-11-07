define(["ko", "models/session", "models/managedlist", "models/i18n", "modules/quotes/models/quotes"], function (ko, session, createManagedList, i18n, quotes) {
    return function QuoteManagerViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Quotes.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Quotes.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Quotes.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });

        self.editingId = ko.observable(null);
        self.text = ko.observable("");
        self.author = ko.observable("");

        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });

        self.heading = ko.pureComputed(function () {
            return self.isEditing() ? i18n.t("quotes.editor.edit") : i18n.t("quotes.editor.new");
        });

        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && self.text().trim() !== "" && self.author().trim() !== "";
        });

        var present = function (item) {
            var row = {
                id: item.id,
                text: item.text,
                author: item.author,
                isDeleted: item.isDeleted
            };

            row.edit = function () {
                self.editingId(row.id);
                self.text(row.text);
                self.author(row.author);
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("quotes.confirm.delete", [row.author]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return quotes.remove(row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return quotes.restore(row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("quotes.confirm.hardDelete", [row.author]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return quotes.hardDelete(row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: quotes.manage,
            pageOf: function (data) {
                return data.quotes;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            self.text("");
            self.author("");
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var fields = { text: self.text().trim(), author: self.author().trim() };

            return self.list.perform(function () {
                var saving = self.isEditing() ? quotes.save(self.editingId(), fields) : quotes.create(fields);

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
