define(["ko", "models/session", "models/i18n", "models/pager", "modules/forum/models/forum", "models/user"], function (ko, session, i18n, createPager, api, user) {
    return function ForumViewModel() {
        var self = this;

        self.boards = ko.observableArray([]);
        self.categories = ko.observableArray([]);
        self.topics = ko.observableArray([]);
        self.replies = ko.observableArray([]);
        self.board = ko.observable(null);
        self.topic = ko.observable(null);
        self.title = ko.observable("");
        self.body = ko.observable("");
        self.isAnonymous = ko.observable(false);
        self.editing = ko.observable(null);
        self.userHref = user.href;
        self.editorMode = ko.observable("visual");
        self.message = ko.observable("");
        self.busy = ko.observable(false);
        self.isSignedIn = session.isSignedIn;
        self.unknownAuthor = i18n.t("forum.unknownAuthor");
        self.i18nLabel = i18n.t("forum.navigation");
        self.editorLabel = i18n.t("forum.editorMode");
        self.canSubmit = ko.pureComputed(function () {
            return session.isSignedIn() && !self.busy() && self.body().trim().length > 0 && self.body().length <= 20000
                && (self.topic() || self.title().trim().length > 0);
        });
        self.showVisual = function () { self.editorMode("visual"); };
        self.showSource = function () { self.editorMode("source"); };
        self.showPreview = function () { self.editorMode("preview"); };
        self.canManage = ko.pureComputed(function () {
            return session.hasGrant("Forum.Manage") || session.hasGrant("ForumModeration.Manage");
        });
        self.boardPager = createPager(function () { return self.loadBoards(); });
        self.topicPager = createPager(function () { return self.loadTopics(); });
        self.replyPager = createPager(function () { return self.loadReplies(); });
        self.boardGroups = ko.pureComputed(function () {
            var labels = {};
            self.categories().forEach(function (category) {
                labels[category.key] = category.entity.name;
            });

            var groups = [];
            self.boards().forEach(function (board) {
                var id = board.entity.categoryId || "";
                var group = groups[groups.length - 1];

                if (!group || group.id !== id) {
                    group = {
                        id: id,
                        name: id ? labels[id] || i18n.t("forum.uncategorized") : i18n.t("forum.uncategorized"),
                        boards: []
                    };
                    groups.push(group);
                }

                group.boards.push(board);
            });

            return groups;
        });

        var perform = function (action) {
            self.busy(true);
            self.message("");

            return action().catch(function (error) {
                self.message(error.message);
            }).finally(function () {
                self.busy(false);
            });
        };

        var update = function (pager, data) {
            pager.update({ page: data.index, pageCount: data.pageCount, total: data.total });
        };

        var present = function (item) {
            if (item.isAnonymous && !item.authorId) {
                item.author = i18n.t("comments.anonymous");
            }
            return item;
        };
        self.edit = function (item) {
            self.editing(item);
            self.title(item.title);
            self.body(item.body);
            self.isAnonymous(item.isAnonymous);
        };
        self.cancelEdit = function () {
            self.editing(null);
            self.title("");
            self.body("");
            self.isAnonymous(false);
        };
        self.remove = function (item) {
            if (!window.confirm(i18n.t("forum.confirmDelete"))) {
                return Promise.resolve();
            }
            return perform(function () {
                return api.save("posts/" + item.id + "/delete", {}).then(function () {
                    if (self.topic() && self.topic().id === item.id) {
                        self.topic(null);
                    }
                    return self.topic() ? loadReplies() : loadTopics();
                });
            });
        };
        self.loadBoards = function () {
            return perform(function () {
                return Promise.all([
                    api.get("boards?page=" + self.boardPager.page()),
                    api.get("categories")
                ]).then(function (results) {
                    var data = results[0];
                    self.boards(data.items);
                    self.categories(results[1]);
                    update(self.boardPager, data);
                });
            });
        };
        var loadTopics = function () {
            return api.get("posts?boardId=" + self.board().key + "&page=" + self.topicPager.page()).then(function (data) {
                self.topics(data.items.map(present));
                update(self.topicPager, data);
            });
        };
        var loadReplies = function () {
            return api.get("posts?boardId=" + self.board().key + "&topicId=" + self.topic().id + "&page=" + self.replyPager.page()).then(function (data) {
                self.replies(data.items.map(present));
                update(self.replyPager, data);
            });
        };
        self.loadTopics = function () { return perform(loadTopics); };
        self.loadReplies = function () { return perform(loadReplies); };
        self.openBoard = function (board) {
            self.cancelEdit();
            self.board(board);
            self.topic(null);
            self.title("");
            self.body("");
            self.topicPager.page(1);

            return self.loadTopics();
        };
        self.openTopic = function (topic) {
            self.cancelEdit();
            self.topic(topic);
            self.body("");
            self.replyPager.page(1);

            return self.loadReplies();
        };
        self.back = function () {
            self.cancelEdit();
            if (self.topic()) {
                self.topic(null);
                return self.loadTopics();
            }

            self.board(null);
            return self.loadBoards();
        };
        self.home = function () {
            self.cancelEdit();
            self.board(null);
            self.topic(null);
            self.body("");
            self.title("");
            return self.loadBoards();
        };
        self.submit = function () {
            if (!self.canSubmit()) {
                self.message(i18n.t(self.body().length > 20000 ? "forum.tooLong" : "forum.required"));
                return Promise.resolve();
            }

            return perform(function () {
                var edited = self.editing();
                return api.save(edited ? "posts/" + edited.id : "posts", {
                    boardId: self.board().key,
                    topicId: self.topic() ? self.topic().id : null,
                    title: self.title(),
                    body: self.body(),
                    isAnonymous: self.isAnonymous()
                }).then(function (data) {
                    self.cancelEdit();
                    var message = i18n.t(data.state === "Pending" ? "forum.pending" : "forum.saved");

                    var refreshTopic = edited && self.topic() && edited.id === self.topic().id
                        ? api.get("posts/" + edited.id).then(function (post) { self.topic(present(post)); })
                        : Promise.resolve();
                    return refreshTopic.then(function () {
                        return self.topic() ? loadReplies() : loadTopics();
                    }).then(function () {
                        self.message(message);
                    });
                });
            });
        };
        self.activate = self.loadBoards;
    };
});
