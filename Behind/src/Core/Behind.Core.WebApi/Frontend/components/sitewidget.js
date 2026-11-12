define(["require", "ko", "models/widgets", "models/articles", "models/menus",
    "models/http", "models/session", "models/i18n", "models/invalidation", "models/user", "text!components/sitewidget.html"],
function (require, ko, widgets, articles, menus, http, session, i18n, invalidation, user, template) {
    return {
        viewModel: {
            createViewModel: function (params, componentInfo) {
                var widget = ko.unwrap(params.widget) || {};
                var type = ko.unwrap(widget.type);
                var limit = Math.max(1, Math.min(50, Number(ko.unwrap(widget.limit)) || 5));
                var disposed = false;
                var sequence = 0;
                var timer = null;
                var follower = null;
                var chatPages = 1;
                var chatCursor = null;
                var chatRefreshing = false;
                var chatSize = Math.max(20, limit);
                var menuRevision = ko.observable(0);
                var self = {
                    type: type,
                    title: ko.unwrap(widget.title) || "",
                    items: ko.observableArray([]),
                    stats: ko.observableArray([]),
                    messages: ko.observableArray([]),
                    content: ko.observable(""),
                    message: ko.observable(null),
                    messageDismissed: ko.observable(false),
                    text: ko.observable(""),
                    isAnonymous: ko.observable(false),
                    error: ko.observable(""),
                    busy: ko.observable(false),
                    loadingOlder: ko.observable(false),
                    hasOlder: ko.observable(false),
                    replyingTo: ko.observable(null),
                    editing: ko.observable(null),
                    editText: ko.observable(""),
                    editAnonymous: ko.observable(false),
                    signedIn: session.isSignedIn,
                    userHref: user.href,
                    author: function (message) {
                        return message.isAnonymous && !message.authorId ? i18n.t("comments.anonymous") : message.author;
                    },
                    t: i18n.t
                };

                self.chatReadable = ko.pureComputed(function () {
                    return !session.isSignedIn() || session.hasGrant("Chat.Read");
                });
                self.chatWritable = ko.pureComputed(function () {
                    return session.isSignedIn() && session.hasGrant("Chat.Read") && session.hasGrant("Chat.Write");
                });

                if (type === "core.menu-horizontal" || type === "core.menu-vertical") {
                    follower = menus.follow(ko.unwrap(widget.menu) || "main");
                }

                self.menuItems = ko.pureComputed(function () {
                    menuRevision();
                    return follower ? follower().items : [];
                });

                var checked = function (response) {
                    if (!response.ok) {
                        var error = new Error(i18n.t(type === "core.chat" ? "widgets.chatError" : "common.error.generic"));
                        error.status = response.status;
                        throw error;
                    }

                    return response.data;
                };

                var chatPage = function (cursor) {
                    return http.get("/widgets/chat?limit=" + chatSize + (cursor ? "&before=" + encodeURIComponent(cursor) : ""))
                        .then(checked);
                };

                var chatHistory = function () {
                    var entries = [];
                    var oldest = self.messages()[0];
                    var cutoff = oldest ? Date.parse(oldest.createdAt) : null;
                    var read = function (cursor, count) {
                        return chatPage(cursor).then(function (page) {
                            entries = page.items.concat(entries);
                            var last = page.items[page.items.length - 1];
                            var lastTime = last ? Date.parse(last.createdAt) : null;
                            var reachedOldest = cutoff === null || !last
                                || lastTime < cutoff || lastTime === cutoff && last.id <= oldest.id;
                            return page.nextCursor && (count < chatPages || !reachedOldest)
                                ? read(page.nextCursor, count + 1)
                                : { messages: entries, nextCursor: page.nextCursor, pageCount: count };
                        });
                    };
                    return read(null, 1);
                };

                var historyElement = function () {
                    return componentInfo ? componentInfo.element.querySelector(".chat-history") : null;
                };

                var list = function (model, property, route) {
                    return model.list({ page: 1, pageSize: limit, category: "", search: "", letter: "" })
                        .then(function (result) {
                            if (!result.ok) {
                                throw new Error(result.message);
                            }

                            return { items: result.data[property].items.map(function (item) {
                                return { title: item.title, href: "#/" + route + "/" + item.id };
                            }) };
                        });
                };

                var moduleLoad = function () {
                    var descriptor = widgets.catalog().filter(function (item) {
                        return item.key === type && item.model;
                    })[0];

                    if (!descriptor) {
                        return Promise.resolve({});
                    }

                    return new Promise(function (resolve, reject) {
                        require([descriptor.model], function (model) {
                            Promise.resolve(model.load(type, limit)).then(resolve, reject);
                        }, reject);
                    });
                };

                var messageId = ko.unwrap(widget.message) || "";
                var dismissedMessageKey = null;
                var messageLevels = ["info", "success", "warning", "danger"];
                var dismissalKey = function (message) {
                    return "behind.message.dismissed." + message.id + "." + message.version;
                };
                var isDismissed = function (message) {
                    if (dismissedMessageKey === dismissalKey(message)) {
                        return true;
                    }
                    try {
                        return window.localStorage.getItem(dismissalKey(message)) !== null;
                    } catch (error) {
                        console.warn("Message dismissal storage is unavailable", error);
                        return false;
                    }
                };

                self.messageClass = ko.pureComputed(function () {
                    var message = self.message();
                    var level = message && messageLevels.indexOf(message.level) >= 0 ? message.level : "info";
                    return "alert alert-" + level + " alert-dismissible behind-message";
                });
                self.messageVisible = ko.pureComputed(function () {
                    return self.message() !== null && !self.messageDismissed();
                });
                self.dismissMessage = function () {
                    var message = self.message();

                    if (message) {
                        dismissedMessageKey = dismissalKey(message);
                        try {
                            window.localStorage.setItem(dismissalKey(message), "1");
                        } catch (error) {
                            console.warn("Message dismissal could not be stored", error);
                        }
                    }

                    self.messageDismissed(true);
                };

                var loadMessage = function () {
                    if (!messageId) {
                        return Promise.resolve({ message: null });
                    }

                    return http.get("/widgets/messages/" + encodeURIComponent(messageId)).then(function (response) {
                        if (response.status === 404) {
                            return { message: null };
                        }

                        return { message: checked(response) };
                    });
                };

                var load = function () {
                    if (type === "core.message") {
                        return loadMessage();
                    }

                    if (type === "core.news") {
                        return http.get("/widgets/news").then(checked).then(function (data) {
                            return { items: data.slice(0, limit).map(function (item) {
                                return { title: item.title, href: "#/news/" + item.id };
                            }) };
                        });
                    }

                    if (type === "core.articles") {
                        return list(articles, "articles", "articles");
                    }

                    if (type === "core.downloads") {
                        return http.get("/widgets/downloads").then(checked).then(function (data) {
                            return { items: data.files.slice(0, limit).map(function (file) {
                                return { title: file.displayName, href: file.url };
                            }) };
                        });
                    }

                    if (type === "core.custom") {
                        return http.post("/widgets/sanitize", { content: ko.unwrap(widget.content) || "" })
                            .then(checked).then(function (data) { return { content: data.content }; });
                    }

                    if (type === "core.chat") {
                        return chatHistory();
                    }

                    return moduleLoad();
                };

                self.refresh = function () {
                    if (disposed) {
                        return Promise.resolve();
                    }

                    if (type === "core.chat" && !self.chatReadable()) {
                        sequence++;
                        self.messages([]);
                        self.hasOlder(false);
                        self.loadingOlder(false);
                        self.cancelReply();
                        self.cancelEdit();
                        chatCursor = null;
                        chatRefreshing = false;
                        self.error("");
                        return Promise.resolve();
                    }

                    var ticket = ++sequence;
                    var owner = session.accessToken();
                    chatRefreshing = type === "core.chat";
                    self.loadingOlder(false);
                    return load().then(function (data) {
                        if (!disposed && ticket === sequence && owner === session.accessToken()) {
                            self.items(data.items || []);
                            self.stats(data.stats || []);
                            var history = historyElement();
                            var previousHeight = history ? history.scrollHeight : 0;
                            var previousTop = history ? history.scrollTop : 0;
                            var previousBottom = history
                                ? history.scrollHeight - history.scrollTop - history.clientHeight
                                : null;
                            var previousFirst = self.messages()[0];
                            self.messages(data.messages || []);
                            if (type === "core.chat") {
                                var reply = self.replyingTo();
                                if (reply) {
                                    self.replyingTo(self.messages().filter(function (message) { return message.id === reply.id; })[0] || null);
                                }
                                var edit = self.editing();
                                if (edit && !self.messages().some(function (message) { return message.id === edit.id; })) {
                                    self.cancelEdit();
                                }
                                chatCursor = data.nextCursor;
                                chatPages = data.pageCount;
                                self.hasOlder(!!chatCursor);
                                var previousFirstIndex = previousFirst
                                    ? self.messages().findIndex(function (message) { return message.id === previousFirst.id; })
                                    : -1;
                                if (history && previousFirstIndex > 0) {
                                    history.scrollTop = previousTop + history.scrollHeight - previousHeight;
                                } else if (history && previousFirst && previousBottom < 80) {
                                    history.scrollTop = history.scrollHeight;
                                }
                            }
                            self.content(data.content || "");
                            self.message(data.message || null);
                            self.messageDismissed(data.message ? isDismissed(data.message) : false);
                            self.error("");
                        }
                    }).catch(function (error) {
                        if (!disposed && ticket === sequence && owner === session.accessToken()) {
                            self.items([]);
                            self.stats([]);
                            if (type !== "core.chat" || error.status === 401 || error.status === 403) {
                                self.messages([]);
                            }
                            if (type === "core.chat" && (error.status === 401 || error.status === 403)) {
                                chatCursor = null;
                                self.hasOlder(false);
                                self.cancelReply();
                                self.cancelEdit();
                            }
                            self.content("");
                            self.message(null);
                            self.error(error.message || i18n.t("common.error.generic"));
                        }
                    }).finally(function () {
                        if (ticket === sequence) {
                            chatRefreshing = false;
                        }
                    });
                };

                self.loadOlder = function () {
                    if (disposed || !chatCursor || self.loadingOlder() || chatRefreshing || self.busy()) {
                        return Promise.resolve();
                    }

                    var ticket = ++sequence;
                    var owner = session.accessToken();
                    self.loadingOlder(true);
                    return chatPage(chatCursor).then(function (page) {
                        if (!disposed && ticket === sequence && owner === session.accessToken()) {
                            var ids = new Set(self.messages().map(function (message) { return message.id; }));
                            var history = historyElement();
                            var previousHeight = history ? history.scrollHeight : 0;
                            var previousTop = history ? history.scrollTop : 0;
                            self.messages(page.items.filter(function (message) { return !ids.has(message.id); })
                                .concat(self.messages()));
                            if (history) {
                                history.scrollTop = previousTop + history.scrollHeight - previousHeight;
                            }
                            chatPages++;
                            chatCursor = page.nextCursor;
                            self.hasOlder(!!chatCursor);
                            self.error("");
                        }
                    }).catch(function (error) {
                        if (!disposed && ticket === sequence && owner === session.accessToken()) {
                            if (error.status === 401 || error.status === 403) {
                                self.messages([]);
                                chatCursor = null;
                                self.hasOlder(false);
                                self.cancelReply();
                                self.cancelEdit();
                            }
                            self.error(error.message);
                        }
                    }).finally(function () {
                        if (!disposed && ticket === sequence) {
                            self.loadingOlder(false);
                        }
                    });
                };

                self.scrollChat = function (model, event) {
                    var element = event.currentTarget;
                    if (element.scrollTop < 80) {
                        self.loadOlder();
                    }
                    return true;
                };

                self.formatTime = function (value) {
                    return value ? new Date(value).toLocaleString() : "";
                };
                self.canEdit = function (message) {
                    return self.chatWritable() && message.canEdit && Date.now() < Date.parse(message.editableUntil);
                };
                self.reply = function (message) {
                    self.replyingTo(message);
                    self.editing(null);
                };
                self.cancelReply = function () { self.replyingTo(null); };
                self.edit = function (message) {
                    self.editing(message);
                    self.editText(message.text);
                    self.editAnonymous(!!message.isAnonymous);
                    self.replyingTo(null);
                };
                self.cancelEdit = function () { self.editing(null); self.editText(""); self.editAnonymous(false); };

                var mutateChat = function (request, completed) {
                    var owner = session.accessToken();
                    self.busy(true);
                    return request().then(checked).then(function () {
                        if (!disposed && owner === session.accessToken()) {
                            completed();
                            return self.refresh();
                        }
                    }).catch(function (error) {
                        if (!disposed && owner === session.accessToken()) {
                            self.error(error.message);
                        }
                    }).finally(function () {
                        if (!disposed && owner === session.accessToken()) {
                            self.busy(false);
                        }
                    });
                };

                self.saveEdit = function () {
                    var message = self.editing();
                    var text = self.editText().trim();
                    if (!message || !text || text.length > 1000 || self.busy() || !self.chatWritable()) {
                        return;
                    }
                    return mutateChat(function () {
                        return http.put("/widgets/chat/" + message.id, {
                            text: text, version: message.version, isAnonymous: self.editAnonymous()
                        });
                    }, self.cancelEdit);
                };

                self.remove = function (message) {
                    if (self.busy() || !window.confirm(i18n.t("widgets.chatConfirmRemove"))) {
                        return;
                    }
                    return mutateChat(function () {
                        return http.delete("/widgets/chat/" + message.id);
                    }, function () { self.cancelReply(); self.cancelEdit(); });
                };

                self.send = function () {
                    var text = self.text().trim();

                    if (!text || text.length > 1000 || self.busy() || !self.chatWritable()) {
                        return;
                    }

                    var parent = self.replyingTo();
                    return mutateChat(function () {
                        return http.post("/widgets/chat", {
                            text: text, replyToId: parent ? parent.id : null, isAnonymous: self.isAnonymous()
                        });
                    }, function () { self.text(""); self.isAnonymous(false); self.cancelReply(); });
                };

                var identity = ko.pureComputed(function () {
                    return type === "core.chat"
                        ? JSON.stringify([session.accessToken(), self.chatReadable(), self.chatWritable(),
                            session.hasGrant("Chat.Delete"), session.hasGrant("Chat.Manage")])
                        : session.accessToken();
                });
                var identitySubscription = identity.subscribe(function () {
                    sequence++;
                    self.items([]);
                    self.stats([]);
                    self.messages([]);
                    self.content("");
                    self.message(null);
                    self.text("");
                    self.isAnonymous(false);
                    self.error("");
                    chatPages = 1;
                    chatCursor = null;
                    chatRefreshing = false;
                    self.loadingOlder(false);
                    self.hasOlder(false);
                    self.busy(false);
                    self.cancelReply();
                    self.cancelEdit();

                    if (follower) {
                        follower.dispose();
                        follower = menus.follow(ko.unwrap(widget.menu) || "main");
                        menuRevision(menuRevision() + 1);
                    }

                    self.refresh();
                });
                var subscription = invalidation.subscribe("widgets", self.refresh);
                self.refresh();

                if (type === "core.chat") {
                    timer = window.setInterval(function () {
                        if (!chatRefreshing && !self.loadingOlder() && !self.busy()) {
                            self.refresh();
                        }
                    }, 15000);
                }

                if (type === "core.message") {
                    timer = window.setInterval(function () {
                        self.refresh();
                    }, 60000);
                }

                self.dispose = function () {
                    disposed = true;
                    sequence++;
                    subscription.dispose();
                    identitySubscription.dispose();
                    identity.dispose();
                    self.menuItems.dispose();
                    self.chatReadable.dispose();
                    self.chatWritable.dispose();
                    self.messageClass.dispose();
                    self.messageVisible.dispose();

                    if (follower) {
                        follower.dispose();
                    }

                    if (timer !== null) {
                        window.clearInterval(timer);
                    }
                };

                return self;
            }
        },
        template: template
    };
});
