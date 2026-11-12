define(["ko", "models/session", "models/i18n", "models/pager", "modules/forum/models/forum", "models/user"], function (ko, session, i18n, createPager, api, user) {
    return function ForumManagerViewModel() {
        var self = this;

        self.busy = ko.observable(false);
        self.userHref = user.href;
        self.message = ko.observable("");
        self.deleted = ko.observable(false);
        self.reviewState = ko.observable("Pending");
        self.boardId = ko.observable(null);
        self.boardName = ko.observable("");
        self.description = ko.observable("");
        self.boardMode = ko.observable("");
        self.boardCategoryId = ko.observable("");
        self.categoryNameInput = ko.observable("");
        self.categoryId = ko.observable(null);
        self.categoryOptions = ko.observableArray([{ value: "", label: i18n.t("forum.uncategorized") }]);
        self.userId = ko.observable("");
        self.userMode = ko.observable("Pre");
        self.settings = ko.observable({ allowBoardOverrides: false, allowUserOverrides: false });
        self.canManage = ko.pureComputed(function () { return session.hasGrant("Forum.Manage"); });
        self.canModerate = ko.pureComputed(function () { return session.hasGrant("ForumModeration.Manage"); });
        self.canViewDeleted = ko.pureComputed(function () { return session.hasGrant("RawDatabase.Manage"); });
        self.canSettings = ko.pureComputed(function () { return session.hasGrant("Configuration.Manage"); });
        self.unknownAuthor = i18n.t("forum.unknownAuthor");
        self.modes = [
            { value: "", label: i18n.t("forum.inherit") },
            { value: "Post", label: i18n.t("forum.postModeration") },
            { value: "Pre", label: i18n.t("forum.preModeration") }
        ];
        self.userModes = self.modes.slice(1);
        self.categoryName = function (id) {
            var category = self.categoryOptions().find(function (option) { return option.value === id; });
            return category ? category.label : i18n.t("forum.uncategorized");
        };
        self.states = [
            { value: "Pending", label: i18n.t("forum.state.Pending") },
            { value: "Published", label: i18n.t("forum.state.Published") },
            { value: "Rejected", label: i18n.t("forum.state.Rejected") }
        ];

        var perform = function (action) {
            self.busy(true);
            self.message("");

            return action().catch(function (error) {
                self.message(error.message);
            }).finally(function () {
                self.busy(false);
            });
        };
        var section = function (path, permitted, map) {
            var list = { items: ko.observableArray([]) };

            list.pager = createPager(function () { return perform(list.load); });
            list.load = function () {
                if (!permitted()) {
                    list.items([]);
                    return Promise.resolve();
                }

                var separator = path.indexOf("?") >= 0 ? "&" : "?";
                return api.get(path + separator + "page=" + list.pager.page() + "&deleted=" + self.deleted()
                    + (path === "manage/posts" ? "&state=" + self.reviewState() : "")).then(function (data) {
                    list.items(data.items.map(map));
                    list.pager.update({ page: data.index, pageCount: data.pageCount, total: data.total });
                });
            };
            return list;
        };
        var actions = function (path, row, id) {
            var change = function (action) {
                if ((action === "delete" || action === "hard/delete")
                    && !window.confirm(i18n.t(action === "delete" ? "forum.confirmDelete" : "forum.confirmHardDelete"))) {
                    return Promise.resolve();
                }

                return perform(function () {
                    return api.save(path + "/" + id + "/" + action).then(self.reload);
                });
            };
            row.remove = function () { return change("delete"); };
            row.restore = function () { return change("restore"); };
            row.hardDelete = function () { return change("hard/delete"); };
            return row;
        };
        self.boards = section("boards", self.canManage, function (row) {
            row.edit = function () {
                self.boardId(row.key);
                self.boardName(row.entity.name);
                self.description(row.entity.description);
                self.boardMode(self.settings().allowBoardOverrides ? row.entity.moderation || "" : "");
                self.boardCategoryId(row.entity.categoryId || "");
            };
            return actions("boards", row, row.key);
        });
        self.categories = section("manage/categories", self.canManage, function (row) {
            row.edit = function () {
                self.categoryId(row.key);
                self.categoryNameInput(row.entity.name);
            };
            return actions("categories", row, row.key);
        });
        self.posts = section("manage/posts", self.canModerate, function (row) {
            row.stateLabel = i18n.t("forum.state." + row.state);
            row.approve = function () {
                return perform(function () {
                    return api.save("posts/" + row.id + "/review", { state: "Published" }).then(self.reload);
                });
            };
            row.reject = function () {
                return perform(function () {
                    return api.save("posts/" + row.id + "/review", { state: "Rejected" }).then(self.reload);
                });
            };
            return actions("posts", row, row.id);
        });
        self.rules = section("manage/users", self.canManage, function (row) {
            row.label = (row.name || row.userId) + " - " + i18n.t("forum." + (row.moderation === "Pre" ? "preModeration" : "postModeration"));
            row.edit = function () {
                self.userId(row.userId);
                self.userMode(row.moderation);
            };
            return actions("manage/users", row, row.id);
        });
        self.reload = function () {
            return Promise.all([self.boards.load(), self.categories.load(), self.posts.load(), self.rules.load()]);
        };
        self.refresh = function () { return perform(self.reload); };
        self.reset = function () {
            self.boardId(null);
            self.boardName("");
            self.description("");
            self.boardMode("");
            self.boardCategoryId("");
        };
        self.resetCategory = function () {
            self.categoryId(null);
            self.categoryNameInput("");
        };
        self.loadCategoryOptions = function () {
            return api.get("categories").then(function (categories) {
                self.categoryOptions([{ value: "", label: i18n.t("forum.uncategorized") }].concat(
                    categories.map(function (category) {
                        return { value: category.key, label: category.entity.name };
                    })));
            });
        };
        self.saveBoard = function () {
            return perform(function () {
                return api.save("boards" + (self.boardId() ? "/" + self.boardId() : ""), {
                    name: self.boardName(),
                    description: self.description(),
                    moderation: self.boardMode() || null,
                    categoryId: self.boardCategoryId() || null
                }).then(function () {
                    self.reset();
                    return self.reload();
                });
            });
        };
        self.saveCategory = function () {
            return perform(function () {
                return api.save("categories" + (self.categoryId() ? "/" + self.categoryId() : ""), {
                    name: self.categoryNameInput()
                }).then(function () {
                    self.resetCategory();
                    return Promise.all([self.loadCategoryOptions(), self.reload()]);
                });
            });
        };
        self.saveUser = function () {
            return perform(function () {
                return api.save("manage/users", { userId: self.userId(), moderation: self.userMode() }).then(self.reload);
            });
        };
        self.activate = function () {
            return perform(function () {
                return api.get("settings").then(function (settings) {
                    self.settings(settings);
                    return self.loadCategoryOptions().then(self.reload);
                });
            });
        };
    };
});
