define(["ko", "models/layouts", "models/widgets", "models/session", "models/i18n", "bindings/richeditor"], function (ko, layouts, widgets, session, i18n) {
    var id = function () {
        return window.crypto.randomUUID();
    };
    var editableWidget = function (data) {
        data = data || {};
        return {
            id: data.id || id(),
            type: ko.observable(data.type || "core.content"),
            title: ko.observable(data.title || ""),
            row: ko.observable(data.row || 1),
            column: ko.observable(data.column || 1),
            rowSpan: ko.observable(data.rowSpan || 1),
            columnSpan: ko.observable(data.columnSpan || 1),
            menu: ko.observable(data.menu || "main"),
            content: ko.observable(data.content || ""),
            editor: ko.observable(data.editor || "richtext"),
            limit: ko.observable(data.limit || 5)
        };
    };
    var editableSection = function (data) {
        data = data || { rows: 1, columns: 1, widgets: [] };
        return {
            rows: ko.observable(data.rows),
            columns: ko.observable(data.columns),
            widgets: ko.observableArray(data.widgets.map(editableWidget))
        };
    };
    var editableLayout = function (data) {
        return {
            id: data.id,
            name: ko.observable(data.name),
            deleted: ko.observable(!!data.deleted),
            sections: ko.observableArray(data.sections.map(editableSection))
        };
    };
    var requestWidget = function (widget) {
        return {
            id: widget.id, type: widget.type(), title: widget.title(),
            row: Number(widget.row()), column: Number(widget.column()),
            rowSpan: Number(widget.rowSpan()), columnSpan: Number(widget.columnSpan()),
            menu: widget.menu(), content: widget.content(), editor: widget.editor(), limit: Number(widget.limit())
        };
    };
    var requestLayout = function (layout) {
        return {
            id: layout.id, name: layout.name(), deleted: layout.deleted(),
            sections: layout.sections().map(function (section) {
                return { rows: Number(section.rows()), columns: Number(section.columns()), widgets: section.widgets().map(requestWidget) };
            })
        };
    };

    return function LayoutsViewModel() {
        var self = this;
        var revision = 0;
        self.layouts = ko.observableArray([]);
        self.selected = ko.observable(null);
        self.defaultLayout = ko.observable("");
        self.assignments = ko.observableArray([]);
        self.message = ko.observable("");
        self.busy = ko.observable(false);
        self.canRead = ko.pureComputed(function () {
            return session.hasGrant("Configuration.Read");
        });
        self.canWrite = ko.pureComputed(function () {
            return session.hasGrant("Configuration.Manage") && !self.busy();
        });
        self.canManageDeleted = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.showDeleted = ko.observable(false);
        self.visibleLayouts = ko.pureComputed(function () {
            return self.layouts().filter(function (layout) {
                return !layout.deleted() || self.canManageDeleted() && self.showDeleted();
            });
        });
        self.activeLayouts = ko.pureComputed(function () {
            return self.layouts().filter(function (layout) {
                return !layout.deleted();
            });
        });
        self.widgetTypes = ko.pureComputed(function () {
            return widgets.catalog().map(function (widget) {
                return { key: widget.key, name: i18n.t(widget.name) };
            });
        });
        self.widgetName = function (type) {
            var widget = self.widgetTypes().find(function (item) {
                return item.key === type;
            });
            return widget ? widget.name : type;
        };
        self.label = function (layout) {
            return layout.name() + (layout.deleted() ? " (" + i18n.t("common.deleted") + ")" : "");
        };
        var apply = function (data, selectedId) {
            revision = data.revision;
            self.layouts(data.layouts.map(editableLayout));
            self.defaultLayout(data.defaultLayout);
            self.assignments(data.assignments.map(function (assignment) {
                return { route: ko.observable(assignment.route), layout: ko.observable(assignment.layout) };
            }));
            self.selected(self.layouts().find(function (layout) {
                return layout.id === selectedId;
            }) || self.layouts()[0]);
        };
        self.activate = function () {
            if (!self.canRead()) {
                self.message(i18n.t("layouts.error.denied"));
                return Promise.resolve();
            }
            self.busy(true);
            return layouts.manage().then(function (response) {
                if (response.ok) {
                    apply(response.data);
                } else {
                    self.message(i18n.t("layouts.error.load"));
                }
            }).finally(function () {
                self.busy(false);
            });
        };
        self.addLayout = function () {
            var layout = editableLayout({ id: id(), name: i18n.t("layouts.new"), sections: [{ rows: 1, columns: 1, widgets: [{}] }] });
            self.layouts.push(layout);
            self.selected(layout);
        };
        self.cloneLayout = function () {
            var copy = requestLayout(self.selected());
            copy.id = id();
            copy.deleted = false;
            copy.name += " " + i18n.t("layouts.copy");
            copy.sections.forEach(function (section) {
                section.widgets.forEach(function (widget) {
                    widget.id = id();
                });
            });
            var layout = editableLayout(copy);
            self.layouts.push(layout);
            self.selected(layout);
        };
        self.deleteLayout = function () {
            if (self.selected().id === self.defaultLayout()) {
                self.message(i18n.t("layouts.error.default"));
                return;
            }
            var selected = self.selected();
            selected.deleted(true);
            self.assignments.remove(function (assignment) {
                return assignment.layout() === selected.id;
            });
            self.selected(self.activeLayouts()[0]);
        };
        self.restoreLayout = function () {
            self.selected().deleted(false);
        };
        self.hardDeleteLayout = function () {
            if (window.confirm(i18n.t("layouts.confirm.hardDelete"))) {
                self.layouts.remove(self.selected());
                self.selected(self.activeLayouts()[0]);
            }
        };
        self.addSection = function () {
            self.selected().sections.push(editableSection());
        };
        self.removeSection = function (section) {
            self.selected().sections.remove(section);
        };
        self.moveSection = function (section, direction) {
            var sections = self.selected().sections;
            var index = sections.indexOf(section);
            var next = index + direction;
            if (next >= 0 && next < sections().length) {
                sections.splice(index, 1);
                sections.splice(next, 0, section);
            }
        };
        self.addWidget = function (section) {
            var hasContent = self.selected().sections().some(function (item) {
                return item.widgets().some(function (widget) {
                    return widget.type() === "core.content";
                });
            });
            var position = null;
            for (var row = 1; row <= Number(section.rows()) && !position; row++) {
                for (var column = 1; column <= Number(section.columns()); column++) {
                    if (!section.widgets().some(function (widget) {
                        return row >= Number(widget.row()) && row < Number(widget.row()) + Number(widget.rowSpan())
                            && column >= Number(widget.column()) && column < Number(widget.column()) + Number(widget.columnSpan());
                    })) {
                        position = { row: row, column: column };
                        break;
                    }
                }
            }
            if (!position && Number(section.rows()) < 12) {
                section.rows(Number(section.rows()) + 1);
                position = { row: Number(section.rows()), column: 1 };
            }
            if (!position) {
                self.message(i18n.t("layouts.error.full"));
                return;
            }
            section.widgets.push(editableWidget({ type: hasContent ? "core.news" : "core.content", row: position.row, column: position.column }));
        };
        self.addAssignment = function () {
            self.assignments.push({ route: ko.observable("#/"), layout: ko.observable(self.defaultLayout()) });
        };
        self.removeAssignment = function (assignment) {
            self.assignments.remove(assignment);
        };
        self.save = function () {
            if (!self.canWrite()) {
                return Promise.resolve();
            }
            var selectedId = self.selected() && self.selected().id;
            self.busy(true);
            self.message("");
            return layouts.save({
                revision: revision,
                defaultLayout: self.defaultLayout(),
                layouts: self.layouts().map(requestLayout),
                assignments: self.assignments().map(function (assignment) {
                    return { route: assignment.route().trim(), layout: assignment.layout() };
                })
            }).then(function (response) {
                if (response.ok) {
                    apply(response.data, selectedId);
                    self.message(i18n.t("layouts.saved"));
                } else {
                    self.message(i18n.t(response.status === 409 ? "layouts.error.conflict" : response.status === 403 ? "layouts.error.denied" : "layouts.error.invalid"));
                }
            }).catch(function (error) {
                console.error("Layout save or invalidation failed", error);
                self.message(i18n.t("layouts.error.refresh"));
            }).finally(function () {
                self.busy(false);
            });
        };
    };
});
