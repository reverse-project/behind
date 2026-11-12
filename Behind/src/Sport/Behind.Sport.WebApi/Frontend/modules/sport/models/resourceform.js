define(["ko", "models/i18n", "modules/sport/models/sport"], function (ko, i18n, sport) {
    var pageSize = 20;
    var searchDelay = 300;

    var isBlank = function (value) {
        return value === null || value === undefined || String(value).trim() === "";
    };

    var drafts = {
        text: function (value) {
            return value.trim() || null;
        },
        textarea: function (value) {
            return value.trim() || null;
        },
        number: function (value) {
            return isBlank(value) ? null : Number(value);
        },
        date: function (value) {
            return value || null;
        },
        enum: function (value) {
            return value || null;
        },
        checkbox: function (value) {
            return !!value;
        }
    };

    var initialOf = function (definition) {
        if (definition.initial !== undefined) {
            return definition.initial;
        }

        return definition.type === "checkbox" ? false : "";
    };

    var base = function (definition) {
        return {
            id: "sport-field-" + definition.name,
            name: definition.name,
            type: definition.type,
            labelKey: definition.labelKey,
            required: definition.required,
            maxLength: definition.maxLength,
            min: definition.min,
            max: definition.max,
            step: definition.step,
            pattern: definition.pattern,
            hintKey: definition.hintKey
        };
    };

    var labelOf = function (item) {
        return item.disciplineName ? item.name + " · " + item.disciplineName : item.name;
    };

    var scalarField = function (definition) {
        var field = base(definition);
        var value = ko.observable(initialOf(definition));

        field.value = value;
        field.choices = ko.pureComputed(function () {
            return (definition.options || []).map(function (option) {
                return { value: option.value, label: i18n.t(option.labelKey) };
            });
        });
        field.caption = definition.required ? undefined : ko.pureComputed(function () {
            return i18n.t("common.none");
        });
        field.reset = function () {
            value(initialOf(definition));
        };
        field.load = function (row) {
            var loaded = row[definition.name];

            if (definition.type === "checkbox") {
                value(!!loaded);
            } else {
                value(isBlank(loaded) ? initialOf(definition) : String(loaded));
            }
        };
        field.isFilled = function () {
            return !definition.required || definition.type === "checkbox" || !isBlank(value());
        };
        field.draft = function () {
            return drafts[definition.type](value());
        };

        return field;
    };

    var imageField = function (definition) {
        var field = base(definition);
        var value = ko.observable("");
        var url = ko.observable("");

        field.value = value;
        field.url = url;
        field.message = ko.observable("");
        field.uploading = ko.observable(false);
        field.kind = definition.kind;
        field.hasImage = ko.pureComputed(function () {
            return url() !== "";
        });
        field.reset = function () {
            value("");
            url("");
            field.message("");
        };
        field.load = function (row) {
            value(row[definition.name] || "");
            url(row[definition.urlName] || "");
            field.message("");
        };
        field.remove = function () {
            value("");
            url("");
        };
        field.upload = function (data, event) {
            var file = event.target.files[0];

            if (!file) {
                return Promise.resolve();
            }

            field.uploading(true);
            field.message("");

            return sport
                .uploadImage(definition.upload, file)
                .then(function (result) {
                    if (result.ok) {
                        value(result.data.id);
                        url(result.data.url);
                    } else {
                        field.message(result.message);
                    }
                })
                .finally(function () {
                    event.target.value = "";
                    field.uploading(false);
                });
        };
        field.isFilled = function () {
            return !definition.required || value() !== "";
        };
        field.draft = function () {
            return value() || null;
        };

        return field;
    };

    var referenceField = function (definition, form) {
        var field = base(definition);
        var selected = ko.observable(null);
        var value = ko.observable("");
        var found = ko.observableArray([]);
        var query = ko.observable("").extend({ rateLimit: { timeout: searchDelay, method: "notifyWhenChangesStop" } });
        var ticket = 0;
        field.message = ko.observable("");

        var context = function () {
            var resource = typeof definition.target === "function" ? definition.target(form) : definition.target;
            var scope = definition.filter ? definition.filter(form) : {};

            return resource !== null && scope !== null ? { resource: resource, scope: scope } : null;
        };

        var key = ko.pureComputed(function () {
            var current = context();

            return current === null ? null : JSON.stringify(current);
        });

        var choose = function (item) {
            selected(item);
            value(item ? item.id : "");
        };

        var refresh = function () {
            var current = context();
            var mine = ++ticket;

            if (current === null) {
                found([]);
                field.message("");
                return Promise.resolve();
            }

            return sport
                .manage(current.resource, Object.assign({ page: 1, pageSize: pageSize, search: query().trim() }, current.scope))
                .then(function (result) {
                    if (mine === ticket) {
                        found(result.ok ? result.data.page.items : []);
                        field.message(result.ok ? "" : result.message);
                    }
                }).catch(function () {
                    if (mine === ticket) {
                        found([]);
                        field.message(i18n.t("common.error.generic"));
                    }
                });
        };

        var last = key();

        key.subscribe(function (current) {
            if (last !== null || current === null) {
                choose(null);
            }

            last = current;
            return refresh();
        });

        query.subscribe(refresh);

        field.value = value;
        field.query = query;
        field.ready = ko.pureComputed(function () {
            return key() !== null;
        });
        field.options = ko.pureComputed(function () {
            var chosen = selected();
            var items = chosen ? [chosen].concat(found().filter(function (item) {
                return item.id !== chosen.id;
            })) : found();

            return items.map(function (item) {
                return { id: item.id, label: labelOf(item), item: item };
            });
        });

        value.subscribe(function (id) {
            var chosen = selected();

            if ((chosen ? chosen.id : "") === (id || "")) {
                return;
            }

            var match = field.options().filter(function (option) {
                return option.id === id;
            })[0];

            choose(match ? match.item : null);
        });

        field.search = refresh;
        field.keydown = function (data, event) {
            if (event.key !== "Enter") {
                return true;
            }

            refresh();

            return false;
        };
        field.reset = function () {
            query("");
            choose(null);
        };
        field.load = function (row) {
            var id = row[definition.name];

            if (!id) {
                choose(null);
                return Promise.resolve();
            }

            choose(Object.assign({ id: id, name: row[definition.display] }, definition.fromRow ? definition.fromRow(row) : {}));

            if (!definition.hydrate) {
                return Promise.resolve();
            }

            return definition.hydrate(id).then(function (fetched) {
                var current = selected();

                if (fetched && current && current.id === id) {
                    selected(Object.assign({}, current, fetched));
                }
            });
        };
        field.start = refresh;
        field.isFilled = function () {
            return !definition.required || value() !== "";
        };
        field.draft = function () {
            return value() || null;
        };
        field.selected = selected;

        return field;
    };

    var builders = {
        reference: referenceField,
        image: imageField
    };

    return function createForm(definition) {
        var states = {};
        var form = {};

        form.item = function (name) {
            return states[name] && states[name].selected ? states[name].selected() : null;
        };
        form.id = function (name) {
            return states[name] ? states[name].value() || null : null;
        };

        form.fields = definition.fields.map(function (fieldDefinition) {
            var state = (builders[fieldDefinition.type] || scalarField)(fieldDefinition, form);

            states[fieldDefinition.name] = state;

            return state;
        });

        form.uploading = ko.pureComputed(function () {
            return form.fields.some(function (state) {
                return state.uploading && state.uploading();
            });
        });
        form.isComplete = ko.pureComputed(function () {
            return form.fields.every(function (state) {
                return state.isFilled();
            });
        });
        form.draft = function () {
            return form.fields.reduce(function (draft, state) {
                draft[state.name] = state.draft();

                return draft;
            }, {});
        };
        form.reset = function () {
            form.fields.forEach(function (state) {
                state.reset();
            });
        };
        form.load = function (row) {
            return Promise.all(form.fields.map(function (state) {
                return state.load(row);
            }));
        };
        form.start = function () {
            return Promise.all(form.fields.map(function (state) {
                return state.start ? state.start() : undefined;
            }));
        };

        return form;
    };
});
