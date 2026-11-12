define(["ko", "models/http", "models/results", "models/paging", "models/session", "models/i18n", "models/invalidation", "models/contentlocale"], function (ko, http, results, pagination, session, i18n, invalidation, contentLocale) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "name-taken": "menus.conflict.nameTaken",
        "built-in": "menus.conflict.builtIn",
        "unknown-menu": "menus.conflict.unknownMenu",
        "invalid-parent": "menus.conflict.invalidParent",
        "kind-not-allowed": "menus.conflict.kindNotAllowed",
        "unknown-role": "menus.conflict.unknownRole",
        "version-conflict": "menus.conflict.versionConflict"
    });

    var loaded = outcome.loaded;
    var saved = function (response, message, failures) {
        var result = outcome.saved(response, message, failures);

        if (!result.ok) {
            return result;
        }

        return invalidation.invalidate("menus").then(function () {
            return result;
        });
    };

    var publicFailures = {
        400: "menus.error.chooseOne"
    };

    var readFailures = {
        401: sessionExpired,
        403: "menus.error.readDenied",
        404: "menus.error.notFound"
    };

    var writeFailures = {
        400: "menus.error.invalid",
        401: sessionExpired,
        403: "menus.error.writeDenied",
        404: "menus.error.itemNotFound",
        409: "menus.error.saveFailed"
    };

    var removeFailures = {
        401: sessionExpired,
        403: "menus.error.deleteDenied",
        404: "menus.error.itemNotFound",
        409: "menus.error.deleteFailed"
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "menus.error.deletedReadDenied"
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "menus.error.deletedActionDenied",
        404: "menus.error.itemNotDeleted",
        409: "menus.error.changeFailed"
    };

    var menuDraft = function (fields) {
        return {
            name: fields.name,
            title: fields.title
        };
    };

    var itemDraft = function (fields) {
        return {
            title: fields.title,
            url: fields.url,
            target: fields.target,
            kind: fields.kind,
            position: fields.position,
            parentId: fields.parentId || null,
            roleIds: fields.roleIds || []
        };
    };

    var names = function (list) {
        return list.map(function (name) {
            return "name=" + encodeURIComponent(name);
        }).join("&");
    };

    var builtInNames = {
        "Main menu": "menu.item.mainMenu",
        "Index": "menu.item.index",
        "News": "menu.item.news",
        "Articles": "menu.item.articles",
        "Galleries": "menu.item.galleries",
        "Downloads": "menu.item.downloads",
        "Wall of shame": "menu.item.wallOfShame",
        "Profile": "menu.item.profile",
        "Session": "menu.item.session",
        "Sessions": "menu.item.sessions",
        "Administration": "menu.item.administration"
    };

    var keyOf = function (name) {
        var words = name.split(/[^A-Za-z0-9]+/).filter(function (word) {
            return word !== "";
        });

        return "menu.item." + words.map(function (word, index) {
            var lowered = word.toLowerCase();

            return index === 0 ? lowered : lowered.charAt(0).toUpperCase() + lowered.slice(1);
        }).join("");
    };

    var labelOf = function (name) {
        return Object.prototype.hasOwnProperty.call(builtInNames, name)
            ? i18n.tOr(builtInNames[name], name)
            : i18n.tOr(keyOf(name), name);
    };

    var windowFeatures = "popup,width=1024,height=768,noopener,noreferrer";

    var present = function (item) {
        var children = (item.children || []).map(present);
        var opensInNewTab = item.target === "NewTab";
        var opensInNewWindow = item.target === "NewWindow";

        return {
            id: item.id,
            title: ko.pureComputed(function () {
                return labelOf(item.title);
            }),
            url: item.url,
            kind: item.kind,
            children: children,
            hasChildren: children.length > 0,
            attributes: {
                href: item.url,
                target: opensInNewTab ? "_blank" : null,
                rel: opensInNewTab ? "noopener noreferrer" : null
            },
            open: function () {
                if (opensInNewWindow) {
                    window.open(item.url, "_blank", windowFeatures);
                    return false;
                }

                return true;
            }
        };
    };

    var load = function (list) {
        return http.get("/menus?" + names(list)).then(function (response) {
            return loaded(response, publicFailures);
        });
    };

    var follow = function (name) {
        var menu = ko.observable({ title: "", items: [] });
        var latest = 0;
        var disposed = false;
        menu.error = ko.observable("");

        var reload = function () {
            if (disposed) {
                return Promise.resolve({ ok: true });
            }

            var ticket = ++latest;

            return load([name]).then(function (result) {
                var found = result.ok ? result.data.menus.filter(function (candidate) {
                    return candidate.name === name;
                })[0] : null;

                if (ticket === latest && result.ok) {
                    menu(found ? { title: found.title, items: found.items.map(present) } : { title: "", items: [] });
                }

                if (ticket === latest) {
                    menu.error(result.ok ? "" : result.message);
                }

                return result;
            }).catch(function (error) {
                if (ticket === latest) {
                    menu.error(error.message);
                }

                return { ok: false, message: error.message };
            });
        };

        var subscriptions = [
            session.isSignedIn.subscribe(reload),
            session.grants.subscribe(reload),
            contentLocale.current.subscribe(reload),
            contentLocale.assignmentRevision.subscribe(reload),
            invalidation.subscribe("menus", function () {
                return reload().then(function (result) {
                    if (!result.ok) {
                        throw new Error(result.message);
                    }
                });
            })
        ];

        menu.dispose = function () {
            if (disposed) {
                return;
            }

            disposed = true;
            latest++;
            subscriptions.forEach(function (subscription) {
                subscription.dispose();
            });
        };

        reload();

        return menu;
    };

    return {
        targets: [
            { value: "CurrentPage", label: "menuitems.target.currentPage" },
            { value: "NewTab", label: "menuitems.target.newTab" },
            { value: "NewWindow", label: "menuitems.target.newWindow" }
        ],

        label: labelOf,

        present: present,

        load: load,

        follow: follow,

        manage: function (query) {
            return http.get(pagination.managing("/menus/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        manageItems: function (id, query) {
            return http.get(pagination.managing("/menus/" + id + "/items/manage", query)).then(function (response) {
                return loaded(response, query.deleted ? deletedReadFailures : readFailures);
            });
        },

        create: function (fields) {
            return http.post("/menus", menuDraft(fields)).then(function (response) {
                return saved(response, "menus.done.saved", writeFailures);
            });
        },

        save: function (id, fields) {
            return http.post("/menus/" + id, menuDraft(fields)).then(function (response) {
                return saved(response, "menus.done.saved", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/menus/" + id + "/delete").then(function (response) {
                return saved(response, "menus.done.deleted", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/menus/" + id + "/restore").then(function (response) {
                return saved(response, "menus.done.restored", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/menus/" + id + "/hard/delete").then(function (response) {
                return saved(response, "menus.done.permanentlyDeleted", deletedActionFailures);
            });
        },

        createItem: function (menuId, fields) {
            return http.post("/menus/" + menuId + "/items", itemDraft(fields)).then(function (response) {
                return saved(response, "menus.done.itemSaved", writeFailures);
            });
        },

        saveItem: function (id, fields) {
            return http.post("/menus/items/" + id, itemDraft(fields)).then(function (response) {
                return saved(response, "menus.done.itemSaved", writeFailures);
            });
        },

        removeItem: function (id) {
            return http.post("/menus/items/" + id + "/delete").then(function (response) {
                return saved(response, "menus.done.itemDeleted", removeFailures);
            });
        },

        restoreItem: function (id) {
            return http.post("/menus/items/" + id + "/restore").then(function (response) {
                return saved(response, "menus.done.itemRestored", deletedActionFailures);
            });
        },

        hardDeleteItem: function (id) {
            return http.post("/menus/items/" + id + "/hard/delete").then(function (response) {
                return saved(response, "menus.done.itemPermanentlyDeleted", deletedActionFailures);
            });
        }
    };
});
