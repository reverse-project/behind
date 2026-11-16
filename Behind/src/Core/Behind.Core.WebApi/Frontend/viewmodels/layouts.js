define(["ko", "models/layouts", "models/messages", "models/widgets", "models/session", "models/i18n", "models/contentlocale", "bindings/richeditor"], function (ko, layouts, messages, widgets, session, i18n, contentLocale) {
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
            layer: ko.observable(data.layer ?? 0),
            cssClass: ko.observable(data.cssClass || ""),
            borderless: ko.observable(!!data.borderless),
            noMargin: ko.observable(!!data.noMargin),
            noPadding: ko.observable(!!data.noPadding),
            hamburgerPosition: ko.observable(data.hamburgerPosition || "default"),
            showLocaleSwitch: ko.observable(!!data.showLocaleSwitch),
            localeSwitchView: ko.observable(data.localeSwitchView || "view"),
            menu: ko.observable(data.menu || "main"),
            content: ko.observable(data.content || ""),
            editor: ko.observable(data.editor || "richtext"),
            limit: ko.observable(data.limit || 5),
            message: ko.observable(data.message || "")
        };
    };
    var editableSection = function (data) {
        data = data || { rows: 1, columns: 1, width: "site", widgets: [] };
        return {
            rows: ko.observable(data.rows),
            columns: ko.observable(data.columns),
            width: ko.observable(data.width || "site"),
            cssClass: ko.observable(data.cssClass || ""),
            overlap: ko.observable(data.overlap ?? 0),
            layer: ko.observable(data.layer ?? 0),
            widgets: ko.observableArray(data.widgets.map(editableWidget))
        };
    };
    var editableBadge = function (data) {
        data = data || {};
        return {
            id: data.id || id(),
            text: ko.observable(data.text || ""),
            image: ko.observable(data.image || ""),
            link: ko.observable(data.link || ""),
            position: ko.observable(data.position || "top-right"),
            width: ko.observable(data.width ?? 180)
        };
    };
    var editableLayout = function (data) {
        return {
            id: data.id,
            name: ko.observable(data.name),
            locale: ko.observable(data.locale || ""),
            deleted: ko.observable(!!data.deleted),
            badges: ko.observableArray((data.badges || []).map(editableBadge)),
            sections: ko.observableArray(data.sections.map(editableSection))
        };
    };
    var requestWidget = function (widget, trimCssClass) {
        return {
            id: widget.id, type: widget.type(), title: widget.title(),
            row: Number(widget.row()), column: Number(widget.column()),
            rowSpan: Number(widget.rowSpan()), columnSpan: Number(widget.columnSpan()),
            layer: Number(widget.layer()), cssClass: trimCssClass ? widget.cssClass().trim() : widget.cssClass(),
            borderless: widget.borderless(), noMargin: widget.noMargin(), noPadding: widget.noPadding(),
            hamburgerPosition: widget.hamburgerPosition(),
            showLocaleSwitch: widget.showLocaleSwitch(),
            localeSwitchView: widget.localeSwitchView(),
            menu: widget.menu(), content: widget.content(), editor: widget.editor(), limit: Number(widget.limit()), message: widget.message()
        };
    };
    var requestLayout = function (layout, trimCssClass) {
        return {
            id: layout.id, name: layout.name(), locale: layout.locale() || null, deleted: layout.deleted(),
            badges: layout.badges().map(function (badge) {
                return {
                    id: badge.id, text: badge.text().trim(), image: badge.image(),
                    link: badge.link().trim(), position: badge.position(), width: Number(badge.width())
                };
            }),
            sections: layout.sections().map(function (section) {
                return { rows: Number(section.rows()), columns: Number(section.columns()), width: section.width(), cssClass: trimCssClass ? section.cssClass().trim() : section.cssClass(), overlap: Number(section.overlap()), layer: Number(section.layer()), widgets: section.widgets().map(function (widget) {
                    return requestWidget(widget, trimCssClass);
                }) };
            })
        };
    };

    return function LayoutsViewModel() {
        var self = this;
        var revision = 0;
        self.schemaVersion = ko.observable(1);
        self.localeLoadFailed = contentLocale.loadFailed;
        self.localeOptions = ko.pureComputed(function () {
            return [{ code: "", name: i18n.t("contentLocale.neutral") }].concat(contentLocale.locales());
        });
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
            return session.hasGrant("Configuration.Manage") && !self.busy() && !self.localeLoadFailed();
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
        self.messages = ko.observableArray([]);
        self.messageCaption = ko.pureComputed(function () {
            return i18n.t("layouts.messageCaption");
        });
        self.messageLoadError = ko.observable("");
        self.uploadImage = function (file) {
            self.busy(true);
            self.message("");
            return layouts.uploadImage(file).then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    return null;
                }
                return result.data.url;
            }).catch(function (error) {
                console.error("Layout image upload failed", error);
                self.message(i18n.t("common.error.generic"));
                return null;
            }).finally(function () {
                self.busy(false);
            });
        };
        self.widgetName = function (type) {
            var widget = self.widgetTypes().find(function (item) {
                return item.key === type;
            });
            return widget ? widget.name : type;
        };
        self.addBadge = function () {
            if (self.selected().badges().length >= 20) {
                self.message(i18n.t("layouts.error.badgeLimit"));
                return;
            }
            self.selected().badges.push(editableBadge());
        };
        self.removeBadge = function (badge) {
            self.selected().badges.remove(badge);
        };
        self.uploadBadge = function (badge, event) {
            var input = event.target;
            var file = input.files && input.files[0];
            if (!file || !self.canWrite()) {
                return Promise.resolve();
            }
            return self.uploadImage(file).then(function (url) {
                if (url) {
                    badge.image(url);
                }
            }).finally(function () {
                input.value = "";
            });
        };
        self.label = function (layout) {
            return layout.name() + (layout.deleted() ? " (" + i18n.t("common.deleted") + ")" : "");
        };
        var apply = function (data, selectedId) {
            revision = data.revision;
            self.schemaVersion(data.schemaVersion ?? 1);
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
            self.messageLoadError("");
            var choices = [];
            var loadMessages = function (page) {
                return messages.manage({ page: page, pageSize: 50, search: "", letter: "", deleted: false }).then(function (result) {
                    if (!result.ok) {
                        self.messageLoadError(result.message || i18n.t("layouts.error.messages"));
                        return;
                    }
                    var data = result.data.messages;
                    choices = choices.concat(data.items);
                    if (data.page < data.pageCount) {
                        return loadMessages(data.page + 1);
                    }
                    self.messages(choices);
                });
            };
            var availableMessages = session.hasGrant("Message.Read")
                ? loadMessages(1).catch(function (error) {
                    console.error("Layout messages failed to load", error);
                    self.messageLoadError(i18n.t("layouts.error.messages"));
                })
                : Promise.resolve();
            var configuration = layouts.manage().then(function (response) {
                if (response.ok) {
                    apply(response.data);
                } else {
                    self.message(i18n.t("layouts.error.load"));
                }
            });
            return Promise.all([configuration, availableMessages]).finally(function () {
                self.busy(false);
            });
        };
        self.addLayout = function () {
            var layout = editableLayout({
                id: id(),
                name: i18n.t("layouts.new"),
                sections: [
                    { rows: 1, columns: 1, width: "full", widgets: [{ type: "core.header" }] },
                    { rows: 1, columns: 1, width: "site", widgets: [{ type: "core.content" }] },
                    { rows: 1, columns: 1, width: "full", widgets: [{ type: "core.footer" }] }
                ]
            });
            self.layouts.push(layout);
            self.selected(layout);
        };
        self.cloneLayout = function () {
            var copy = requestLayout(self.selected());
            copy.id = id();
            copy.deleted = false;
            copy.name += " " + i18n.t("layouts.copy");
            copy.badges.forEach(function (badge) {
                badge.id = id();
            });
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
            if (section.widgets().length >= 100) {
                self.message(i18n.t("layouts.error.full"));
                return;
            }
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
                position = { row: 1, column: 1 };
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
                schemaVersion: self.schemaVersion(),
                defaultLayout: self.defaultLayout(),
                layouts: self.layouts().map(function (layout) {
                    return requestLayout(layout, true);
                }),
                assignments: self.assignments().map(function (assignment) {
                    return { route: assignment.route().trim(), layout: assignment.layout() };
                })
            }, selectedId).then(function (response) {
                if (response.ok) {
                    contentLocale.clearMedia();
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
