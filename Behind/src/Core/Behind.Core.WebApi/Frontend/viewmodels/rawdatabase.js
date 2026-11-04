define(["ko", "models/session", "models/rawdatabase", "models/pager", "models/i18n"], function (ko, session, rawDatabase, createPager, i18n) {
    var pageSize = 20;
    var maxCellLength = 120;

    var textOf = function (value) {
        if (value === null || value === undefined) {
            return "";
        }

        return typeof value === "string" ? value : String(value);
    };

    var shorten = function (text) {
        return text.length > maxCellLength ? text.slice(0, maxCellLength) + "..." : text;
    };

    var presentTable = function (table) {
        return {
            name: table.name,
            entityName: table.entityName,
            isReadOnly: table.isReadOnly,
            rows: table.rows,
            columns: table.columns,
            label: i18n.t("rawDatabase.tableLabel", [table.name, table.rows])
        };
    };

    var keyOf = function (table, values) {
        var key = {};

        table.columns.forEach(function (column) {
            if (column.isKey) {
                key[column.name] = values[column.name];
            }
        });

        return key;
    };

    var presentCells = function (table, values) {
        return table.columns.map(function (column) {
            var value = values[column.name];
            var text = textOf(value);

            return { isNull: value === null || value === undefined, text: shorten(text), title: text };
        });
    };

    var presentDetails = function (table, values) {
        return table.columns.map(function (column) {
            var value = values[column.name];

            return {
                name: column.name,
                storeType: column.storeType,
                isKey: column.isKey,
                isSensitive: column.isSensitive,
                isNull: value === null || value === undefined,
                text: textOf(value)
            };
        });
    };

    var createField = function (column, values) {
        var existing = values !== null;
        var value = existing ? values[column.name] : undefined;
        var isNull = existing && (value === null || value === undefined);
        var text = existing && !isNull ? textOf(value) : (column.kind === "boolean" ? "false" : "");
        var disabled = existing && column.isKey;

        return {
            name: column.name,
            kind: column.kind,
            storeType: column.storeType,
            isKey: column.isKey,
            isSensitive: column.isSensitive,
            isBoolean: column.kind === "boolean",
            disabled: disabled,
            canBeNull: column.isNullable && !disabled,
            text: ko.observable(text),
            isNull: ko.observable(isNull),
            isChanged: function (field) {
                return field.text() !== text || field.isNull() !== isNull;
            }
        };
    };

    var valueOf = function (field) {
        return field.isNull() ? null : field.text();
    };

    var changedValues = function (fields) {
        var values = {};

        fields.forEach(function (field) {
            if (!field.isKey && field.isChanged(field)) {
                values[field.name] = valueOf(field);
            }
        });

        return values;
    };

    var newValues = function (fields) {
        var values = {};

        fields.forEach(function (field) {
            if (field.isNull() || field.text() !== "") {
                values[field.name] = valueOf(field);
            }
        });

        return values;
    };

    return function RawDatabaseViewModel() {
        var self = this;

        self.isSignedIn = session.isSignedIn;
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("RawDatabase.Manage");
        });
        self.isDenied = ko.pureComputed(function () {
            return session.isSignedIn() && session.grants() !== null && !self.canManage();
        });
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.tables = ko.observableArray([]);
        self.selectedName = ko.observable("");
        self.search = ko.observable("");
        self.orderBy = ko.observable("");
        self.descending = ko.observable(false);
        self.rows = ko.observableArray([]);
        self.editor = ko.observable(null);
        self.viewer = ko.observable(null);
        self.pager = createPager(function () {
            return self.refreshRows();
        });

        self.caption = ko.pureComputed(function () {
            return i18n.t("rawDatabase.chooseTable");
        });
        self.hasMessage = ko.pureComputed(function () {
            return self.message() !== "";
        });
        self.findTable = function (name) {
            return self.tables().filter(function (table) {
                return table.name === name;
            })[0] || null;
        };
        self.selected = ko.pureComputed(function () {
            return self.findTable(self.selectedName());
        });
        self.canWrite = ko.pureComputed(function () {
            return self.selected() !== null && !self.selected().isReadOnly;
        });
        self.hasTables = ko.pureComputed(function () {
            return self.tables().length > 0;
        });
        self.hasRows = ko.pureComputed(function () {
            return self.rows().length > 0;
        });
        self.viewerIndex = ko.pureComputed(function () {
            return self.rows().indexOf(self.viewer());
        });
        self.hasPrevious = ko.pureComputed(function () {
            return self.viewerIndex() > 0;
        });
        self.hasNext = ko.pureComputed(function () {
            return self.viewerIndex() >= 0 && self.viewerIndex() < self.rows().length - 1;
        });
        self.viewerHeading = ko.pureComputed(function () {
            var table = self.selected();

            if (!self.viewer() || !table) {
                return "";
            }

            return i18n.t("rawDatabase.viewer.heading", [table.name, self.viewerIndex() + 1, self.rows().length]);
        });
        self.editorHeading = ko.pureComputed(function () {
            var editor = self.editor();
            var table = self.selected();

            if (!editor || !table) {
                return "";
            }

            return i18n.t(editor.isNew ? "rawDatabase.editor.new" : "rawDatabase.editor.edit", [table.name]);
        });

        self.refreshTables = function () {
            if (!self.canManage()) {
                self.tables([]);
                return Promise.resolve();
            }

            return rawDatabase.tables().then(function (result) {
                if (!result.ok) {
                    self.message(result.message);
                    self.tables([]);
                    return;
                }

                self.tables(result.data.tables.map(presentTable));
            });
        };

        self.refreshRows = function () {
            var table = self.findTable(self.selectedName());

            if (!self.canManage() || table === null) {
                self.rows([]);
                return Promise.resolve();
            }

            self.busy(true);

            return rawDatabase.rows(table.name, {
                page: self.pager.page(),
                pageSize: pageSize,
                search: self.search().trim(),
                orderBy: self.orderBy(),
                descending: self.descending()
            })
                .then(function (result) {
                    self.message(result.message);

                    if (!result.ok) {
                        self.rows([]);
                        return undefined;
                    }

                    if (self.pager.isOutOfRange(result.data)) {
                        self.pager.clampIfOutOfRange(result.data);
                        return self.refreshRows();
                    }

                    self.viewer(null);
                    self.pager.update(result.data);
                    self.rows(result.data.items.map(function (values) {
                        return self.presentRow(table, values);
                    }));

                    return undefined;
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.presentRow = function (table, values) {
            var row = {
                cells: presentCells(table, values),
                details: presentDetails(table, values),
                view: function () {
                    self.editor(null);
                    self.viewer(row);
                },
                edit: function () {
                    self.viewer(null);
                    self.editor({ isNew: false, key: keyOf(table, values), fields: table.columns.map(function (column) {
                        return createField(column, values);
                    }) });
                },
                remove: function () {
                    if (!window.confirm(i18n.t("rawDatabase.confirmDelete", [table.name]))) {
                        return Promise.resolve();
                    }

                    return self.perform(function () {
                        return rawDatabase.remove(table.name, keyOf(table, values));
                    });
                }
            };

            return row;
        };

        self.previous = function () {
            if (self.hasPrevious()) {
                self.viewer(self.rows()[self.viewerIndex() - 1]);
            }
        };

        self.next = function () {
            if (self.hasNext()) {
                self.viewer(self.rows()[self.viewerIndex() + 1]);
            }
        };

        self.closeViewer = function () {
            self.viewer(null);
        };

        self.perform = function (action) {
            self.busy(true);

            return action()
                .then(function (result) {
                    var reload = result.ok
                        ? Promise.all([self.refreshTables(), self.refreshRows()])
                        : Promise.resolve();

                    return reload.then(function () {
                        self.message(result.message);

                        return result;
                    });
                })
                .finally(function () {
                    self.busy(false);
                });
        };

        self.find = function () {
            self.pager.page(1);
            return self.refreshRows();
        };

        self.sortBy = function (column) {
            self.descending(self.orderBy() === column.name ? !self.descending() : false);
            self.orderBy(column.name);
            return self.find();
        };

        self.open = function (table) {
            self.selectedName(table.name);
        };

        self.close = function () {
            self.selectedName("");
        };

        self.newRow = function () {
            var table = self.selected();

            if (table === null || table.isReadOnly) {
                return;
            }

            self.viewer(null);
            self.editor({ isNew: true, key: {}, fields: table.columns.map(function (column) {
                return createField(column, null);
            }) });
        };

        self.cancel = function () {
            self.editor(null);
        };

        self.save = function () {
            var editor = self.editor();
            var table = self.selected();

            if (!editor || table === null || self.busy()) {
                return Promise.resolve();
            }

            var values = editor.isNew ? newValues(editor.fields) : changedValues(editor.fields);

            if (!editor.isNew && Object.keys(values).length === 0) {
                self.editor(null);
                return Promise.resolve();
            }

            return self.perform(function () {
                return editor.isNew
                    ? rawDatabase.insert(table.name, values)
                    : rawDatabase.update(table.name, editor.key, values);
            }).then(function (result) {
                if (result.ok) {
                    self.editor(null);
                }
            });
        };

        self.selectedName.subscribe(function () {
            self.editor(null);
            self.viewer(null);
            self.search("");
            self.orderBy("");
            self.descending(false);
            self.pager.page(1);
            self.refreshRows();
        });

        self.activate = function () {
            self.canManage.subscribe(function (allowed) {
                if (allowed) {
                    self.refreshTables();
                }
            });

            return self.refreshTables();
        };
    };
});
