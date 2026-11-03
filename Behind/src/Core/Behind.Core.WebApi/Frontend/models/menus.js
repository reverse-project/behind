define(["ko", "models/http", "models/results", "models/paging", "models/session"], function (ko, http, results, pagination, session) {
    var sessionExpired = results.sessionExpired;

    var outcome = results.create({
        "name-taken": "Another menu already uses this name. Choose a different one.",
        "built-in": "The built-in menu cannot be renamed, deleted or permanently deleted.",
        "unknown-menu": "This menu no longer exists.",
        "invalid-parent": "Choose a top level link of the same menu as the parent. An item with sub items cannot get a parent.",
        "kind-not-allowed": "User menu items can only be top level items of the main menu.",
        "unknown-role": "One of the selected roles no longer exists.",
        "version-conflict": "The menu was changed at the same time by someone else. Reload it and try again."
    });

    var loaded = outcome.loaded;
    var saved = outcome.saved;

    var publicFailures = {
        400: "Choose at least one menu."
    };

    var readFailures = {
        401: sessionExpired,
        403: "You need menu access to see menus.",
        404: "This menu no longer exists."
    };

    var writeFailures = {
        400: "Enter a name and a title. Menu item URLs must be relative or start with http, https, mailto or tel.",
        401: sessionExpired,
        403: "You are not allowed to change menus.",
        404: "This menu or menu item no longer exists.",
        409: "The menu could not be saved."
    };

    var removeFailures = {
        401: sessionExpired,
        403: "You are not allowed to delete menus.",
        404: "This menu or menu item no longer exists.",
        409: "The menu could not be deleted."
    };

    var deletedReadFailures = {
        401: sessionExpired,
        403: "You need developer access to see deleted menus."
    };

    var deletedActionFailures = {
        401: sessionExpired,
        403: "You need developer access to restore or permanently delete menus.",
        404: "This menu or menu item is not deleted.",
        409: "The menu could not be changed. A deleted menu cannot come back while another uses its name."
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

    var windowFeatures = "popup,width=1024,height=768,noopener,noreferrer";

    var present = function (item) {
        var children = (item.children || []).map(present);
        var opensInNewTab = item.target === "NewTab";
        var opensInNewWindow = item.target === "NewWindow";

        return {
            id: item.id,
            title: item.title,
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

        var reload = function () {
            var ticket = ++latest;

            return load([name]).then(function (result) {
                var found = result.ok ? result.data.menus.filter(function (candidate) {
                    return candidate.name === name;
                })[0] : null;

                if (ticket === latest && result.ok) {
                    menu(found ? { title: found.title, items: found.items.map(present) } : { title: "", items: [] });
                }
            });
        };

        session.isSignedIn.subscribe(reload);
        session.grants.subscribe(reload);
        reload();

        return menu;
    };

    return {
        targets: [
            { value: "CurrentPage", label: "Current page" },
            { value: "NewTab", label: "New tab" },
            { value: "NewWindow", label: "New window" }
        ],

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
                return saved(response, "The menu was saved.", writeFailures);
            });
        },

        save: function (id, fields) {
            return http.post("/menus/" + id, menuDraft(fields)).then(function (response) {
                return saved(response, "The menu was saved.", writeFailures);
            });
        },

        remove: function (id) {
            return http.post("/menus/" + id + "/delete").then(function (response) {
                return saved(response, "The menu was deleted.", removeFailures);
            });
        },

        restore: function (id) {
            return http.post("/menus/" + id + "/restore").then(function (response) {
                return saved(response, "The menu was restored.", deletedActionFailures);
            });
        },

        hardDelete: function (id) {
            return http.post("/menus/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The menu was permanently deleted.", deletedActionFailures);
            });
        },

        createItem: function (menuId, fields) {
            return http.post("/menus/" + menuId + "/items", itemDraft(fields)).then(function (response) {
                return saved(response, "The menu item was saved.", writeFailures);
            });
        },

        saveItem: function (id, fields) {
            return http.post("/menus/items/" + id, itemDraft(fields)).then(function (response) {
                return saved(response, "The menu item was saved.", writeFailures);
            });
        },

        removeItem: function (id) {
            return http.post("/menus/items/" + id + "/delete").then(function (response) {
                return saved(response, "The menu item was deleted.", removeFailures);
            });
        },

        restoreItem: function (id) {
            return http.post("/menus/items/" + id + "/restore").then(function (response) {
                return saved(response, "The menu item was restored.", deletedActionFailures);
            });
        },

        hardDeleteItem: function (id) {
            return http.post("/menus/items/" + id + "/hard/delete").then(function (response) {
                return saved(response, "The menu item was permanently deleted.", deletedActionFailures);
            });
        }
    };
});
