define(["ko", "models/session", "models/managedlist", "models/i18n", "modules/sport/models/sport", "modules/sport/models/resources", "modules/sport/models/resourceform", "modules/sport/models/links"], function (ko, session, createManagedList, i18n, sport, resources, createForm, links) {
    return function ManagerViewModel(context) {
        var self = this;
        var definition = resources.find(context.params.resource);
        var form = definition ? createForm(definition) : null;

        self.links = links;
        self.definition = definition;
        self.isHub = definition === null;
        self.sections = resources.all.map(function (section) {
            return { titleKey: section.titleKey, icon: section.icon, href: links.manageResource(section.name) };
        });
        self.titleKey = definition ? definition.titleKey : "sport.manage.title";
        self.publicHref = definition ? definition.publicHref : links.home;
        self.isSignedIn = session.isSignedIn;
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Sport.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Sport.Write");
        });
        self.canDelete = ko.pureComputed(function () {
            return session.hasGrant("Sport.Delete");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canRead();
        });
        self.chooseLabel = ko.pureComputed(function () {
            return i18n.t("sport.manage.choose");
        });

        if (self.isHub) {
            self.activate = function () {
                return Promise.resolve();
            };

            return;
        }

        self.form = form;
        self.columns = definition.columns;
        self.editingId = ko.observable(null);
        self.isEditing = ko.pureComputed(function () {
            return self.editingId() !== null;
        });
        self.heading = ko.pureComputed(function () {
            return i18n.t(self.isEditing() ? "sport.editor.edit" : "sport.editor.new", [i18n.t(definition.singularKey)]);
        });
        self.canSave = ko.pureComputed(function () {
            return self.canWrite() && !self.busy() && !form.uploading() && form.isComplete();
        });

        var present = function (item) {
            var row = {
                id: item.id,
                isDeleted: item.isDeleted,
                cells: definition.columns.map(function (column) {
                    var shown = Object.assign(
                        { kind: null, id: null, season: null, text: "", image: null, imageKind: null },
                        column.cell(item)
                    );

                    return Object.assign({ hasImage: !!shown.image }, shown);
                })
            };

            row.edit = function () {
                self.editingId(row.id);
                form.reset();

                return form.load(item);
            };
            row.remove = function () {
                if (!window.confirm(i18n.t("sport.confirm.delete", [definition.label(item)]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return sport.remove(definition.name, row.id);
                });
            };
            row.restore = function () {
                return self.list.perform(function () {
                    return sport.restore(definition.name, row.id);
                });
            };
            row.hardDelete = function () {
                if (!window.confirm(i18n.t("sport.confirm.hardDelete", [definition.label(item)]))) {
                    return Promise.resolve();
                }

                return self.list.perform(function () {
                    return sport.hardDelete(definition.name, row.id);
                });
            };

            return row;
        };

        self.list = createManagedList({
            allowed: self.canRead,
            load: function (query) {
                return sport.manage(definition.name, query);
            },
            pageOf: function (data) {
                return data.page;
            },
            present: present
        });

        self.busy = self.list.busy;

        self.reset = function () {
            self.editingId(null);
            form.reset();
        };

        self.save = function () {
            if (!self.canSave()) {
                return Promise.resolve();
            }

            var draft = form.draft();

            return self.list.perform(function () {
                var saving = self.isEditing()
                    ? sport.save(definition.name, self.editingId(), draft)
                    : sport.create(definition.name, draft);

                return saving.then(function (result) {
                    if (result.ok) {
                        self.reset();
                    }

                    return result;
                });
            });
        };

        self.activate = function () {
            return Promise.all([self.list.activate(), form.start()]);
        };
    };
});
