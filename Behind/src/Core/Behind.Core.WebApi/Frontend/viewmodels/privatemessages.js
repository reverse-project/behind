define(["ko", "models/session", "models/privateapi", "models/pager", "models/i18n"], function (ko, session, api, createPager, i18n) {
    ko.bindingHandlers.privateDispose = {
        init: function (element, valueAccessor) {
            ko.utils.domNodeDisposal.addDisposeCallback(element, function () { valueAccessor().dispose(); });
        }
    };
    return function PrivateMessagesViewModel(context) {
        var self = this;
        var alive = true;
        var listTicket = 0;
        var detailTicket = 0;
        var searchTicket = 0;
        self.t = i18n.t;
        self.dateTime = i18n.dateTime;
        self.canRead = ko.pureComputed(function () { return session.hasGrant("PrivateMessage.Read"); });
        self.canWrite = ko.pureComputed(function () { return session.hasGrant("PrivateMessage.Write"); });
        self.canDelete = ko.pureComputed(function () { return session.hasGrant("PrivateMessage.Delete"); });
        self.canSystem = ko.pureComputed(function () { return session.hasGrant("PrivateMessageModeration.Manage"); });
        self.folder = ko.observable("inbox");
        self.items = ko.observableArray([]);
        self.detail = ko.observable(null);
        self.busy = ko.observable(false);
        self.message = ko.observable("");
        self.composing = ko.observable(false);
        self.showBlocks = ko.observable(false);
        self.blocks = ko.observableArray([]);
        self.search = ko.observable("");
        self.users = ko.observableArray([]);
        self.roles = ko.observableArray([]);
        self.userIds = ko.observableArray([]);
        self.roleIds = ko.observableArray([]);
        self.bccUserIds = ko.observableArray([]);
        self.bccRoleIds = ko.observableArray([]);
        self.inviting = ko.observable(false);
        self.inviteUserIds = ko.observableArray([]);
        self.inviteRoleIds = ko.observableArray([]);
        self.inviteBccUserIds = ko.observableArray([]);
        self.inviteBccRoleIds = ko.observableArray([]);
        self.subject = ko.observable("");
        self.content = ko.observable("");
        self.isSystem = ko.observable(false);
        self.reply = ko.observable("");
        self.replyTo = ko.observable(null);
        self.editorMode = ko.observable("visual");
        self.pager = createPager(function () { return self.load(); });
        self.blocksPager = createPager(function () { return self.loadBlocks(); });
        self.canManageMembership = ko.pureComputed(function () {
            var detail = self.detail();
            return self.canRead() && self.canWrite() && !!detail && detail.isGroup && !detail.isSystem && !detail.isReport && self.folder() !== "deleted";
        });
        self.canInvite = ko.pureComputed(function () {
            return self.canManageMembership() && !!self.detail().canManage;
        });
        self.cancelInvite = function () {
            self.inviting(false);
            self.inviteUserIds([]); self.inviteRoleIds([]); self.inviteBccUserIds([]); self.inviteBccRoleIds([]);
            searchTicket++;
        };
        var identity = session.accessToken();
        var identitySubscription = session.grants.subscribe(function () {
            if (identity === session.accessToken()) {
                return;
            }
            identity = session.accessToken();
            listTicket++; detailTicket++; searchTicket++;
            self.items([]); self.detail(null); self.blocks([]); self.users([]); self.roles([]);
            self.blocksPager.update({ page: 1, pageCount: 0, total: 0 });
            self.composing(false); self.showBlocks(false);
            self.cancelInvite();
            self.subject(""); self.content(""); self.reply(""); self.replyTo(null);
            self.userIds([]); self.roleIds([]); self.bccUserIds([]); self.bccRoleIds([]);
            self.message("");
        });
        var failed = function (response) {
            self.message(api.error(response));
        };
        var catchError = function () {
            if (alive) {
                self.message(i18n.t("privateMessages.requestFailed"));
            }
        };
        self.load = function () {
            if (!self.canRead()) {
                return Promise.resolve();
            }
            var ticket = ++listTicket;
            return api.get("/private-messages?" + api.query({ folder: self.folder(), page: self.pager.page(), pageSize: 20 })).then(function (response) {
                if (!alive || ticket !== listTicket) {
                    return;
                }
                if (!response.ok) {
                    failed(response);
                    return;
                }
                self.items(response.data.items || []);
                self.pager.update({ page: response.data.page || response.data.pageNumber || self.pager.page(), pageCount: response.data.pageCount, total: response.data.total });
            }).catch(function () {
                if (alive && ticket === listTicket) {
                    catchError();
                }
            });
        };
        self.selectFolder = function (folder) {
            self.cancelInvite();
            self.folder(folder);
            self.pager.page(1);
            self.detail(null);
            self.composing(false);
            self.showBlocks(false);
            detailTicket++;
            return self.load();
        };
        self.open = function (item) {
            self.cancelInvite();
            var ticket = ++detailTicket;
            self.detail(null);
            self.composing(false);
            self.showBlocks(false);
            self.replyTo(null);
            return api.get("/private-messages/" + encodeURIComponent(item.id)).then(function (response) {
                if (!alive || ticket !== detailTicket) {
                    return;
                }
                if (!response.ok) {
                    failed(response);
                    return;
                }
                self.detail(response.data);
                if (!response.data.isRead && self.folder() !== "deleted") {
                    return api.post("/private-messages/" + encodeURIComponent(item.id) + "/read").then(function (readResponse) {
                        if (alive && ticket === detailTicket && !readResponse.ok) {
                            failed(readResponse);
                        }
                    });
                }
            }).catch(function () {
                if (alive && ticket === detailTicket) {
                    catchError();
                }
            });
        };
        self.find = function () {
            var ticket = ++searchTicket;
            return api.get("/private-messages/recipients?" + api.query({ search: self.search() })).then(function (response) {
                if (!alive || ticket !== searchTicket) {
                    return;
                }
                if (response.ok) {
                    self.users(response.data.users || []);
                    self.roles(self.canSystem() ? response.data.roles || [] : []);
                } else {
                    failed(response);
                }
            }).catch(function () {
                if (alive && ticket === searchTicket) {
                    catchError();
                }
            });
        };
        self.action = function (url, body, done) {
            if (self.busy()) {
                return Promise.resolve();
            }
            self.busy(true);
            self.message("");
            var ticket = detailTicket;
            return api.post(url, body).then(function (response) {
                if (!alive || ticket !== detailTicket) {
                    return;
                }
                if (response.ok) {
                    return done && done(response.data);
                }
                failed(response);
            }).catch(function () {
                if (alive && ticket === detailTicket) {
                    catchError();
                }
            }).finally(function () {
                if (alive) {
                    self.busy(false);
                }
            });
        };
        self.compose = function () {
            self.cancelInvite();
            self.detail(null);
            self.showBlocks(false);
            self.composing(true);
            detailTicket++;
            return self.find();
        };
        self.send = function () {
            var count = self.userIds().length + self.roleIds().length + self.bccUserIds().length + self.bccRoleIds().length;
            if (!self.canWrite() || !self.subject().trim() || !self.content().trim() || count < 1
                || ((self.isSystem() || self.roleIds().length || self.bccRoleIds().length) && !self.canSystem())) {
                self.message(i18n.t("privateMessages.invalidCompose"));
                return;
            }
            return self.action("/private-messages", {
                subject: self.subject().trim(), content: self.content(), userIds: self.userIds().slice(), roleIds: self.roleIds().slice(),
                bccUserIds: self.bccUserIds().slice(), bccRoleIds: self.bccRoleIds().slice(), isSystem: self.isSystem()
            }, function () {
                self.subject(""); self.content(""); self.userIds([]); self.roleIds([]); self.bccUserIds([]); self.bccRoleIds([]);
                return self.selectFolder("sent");
            });
        };
        self.selectReply = function (item) { self.replyTo(item); };
        self.parentOf = function (item) {
            var detail = self.detail();
            if (!detail || !item.replyToId) {
                return null;
            }
            return (detail.messages || []).find(function (message) { return message.id === item.replyToId; }) || null;
        };
        self.sendReply = function () {
            var detail = self.detail();
            if (!self.canWrite() || !detail || !detail.canReply || !self.reply().trim()) {
                return;
            }
            return self.action("/private-messages/" + encodeURIComponent(detail.id) + "/reply", {
                content: self.reply(), replyToId: self.replyTo() ? self.replyTo().id : null
            }, function () {
                self.reply(""); self.replyTo(null);
                return self.open(detail);
            });
        };
        self.startInvite = function () {
            if (!self.canInvite() || self.busy()) {
                return;
            }
            self.cancelInvite();
            self.search(""); self.users([]); self.roles([]);
            self.inviting(true);
            return self.find();
        };
        self.invite = function () {
            var detail = self.detail();
            if (!self.canInvite() || !self.inviting()) {
                return;
            }
            if ((self.inviteRoleIds().length || self.inviteBccRoleIds().length) && !self.canSystem()) {
                self.message(i18n.t("privateMessages.rolesForbidden"));
                return;
            }
            if (!self.inviteUserIds().length && !self.inviteRoleIds().length && !self.inviteBccUserIds().length && !self.inviteBccRoleIds().length) {
                self.message(i18n.t("privateMessages.inviteRequired"));
                return;
            }
            return self.action("/private-messages/" + encodeURIComponent(detail.id) + "/participants", {
                userIds: self.inviteUserIds().slice(), roleIds: self.inviteRoleIds().slice(),
                bccUserIds: self.inviteBccUserIds().slice(), bccRoleIds: self.inviteBccRoleIds().slice()
            }, function () {
                self.cancelInvite();
                return self.open(detail);
            });
        };
        self.rejoin = function (item) {
            if (!self.canRead() || !self.canWrite() || self.folder() !== "left" || !item.isGroup || item.isSystem || item.isReport) {
                return;
            }
            return self.action("/private-messages/" + encodeURIComponent(item.id) + "/join", undefined, function () {
                return self.selectFolder("inbox");
            });
        };
        self.conversationAction = function (action) {
            var detail = self.detail();
            if (!detail || !self.canRead()) {
                return;
            }
            if (action === "delete" && !self.canDelete()) {
                return;
            }
            if ((action === "join" || action === "leave") && !self.canManageMembership()) {
                return;
            }
            return self.action("/private-messages/" + encodeURIComponent(detail.id) + "/" + action, undefined, function () {
                if (action === "delete" || action === "restore") {
                    self.detail(null);
                    return self.load();
                }
                if (action === "leave") {
                    return self.selectFolder("left");
                }
                return self.open(detail);
            });
        };
        self.loadBlocks = function () {
            if (!self.canRead()) {
                return Promise.resolve();
            }
            self.cancelInvite();
            self.showBlocks(true);
            self.detail(null);
            self.composing(false);
            var ticket = ++detailTicket;
            return api.get("/private-messages/blocks?" + api.query({ page: self.blocksPager.page(), pageSize: 20 })).then(function (response) {
                if (!alive || ticket !== detailTicket || !self.showBlocks()) {
                    return;
                }
                if (response.ok) {
                    if (self.blocksPager.isOutOfRange(response.data)) {
                        self.blocksPager.clampIfOutOfRange(response.data);
                        return self.loadBlocks();
                    }
                    self.blocks(response.data.items || []);
                    self.blocksPager.update(response.data);
                } else {
                    failed(response);
                }
            }).catch(function () {
                if (alive && ticket === detailTicket) {
                    catchError();
                }
            });
        };
        self.openBlocks = function () {
            self.blocksPager.page(1);
            return self.loadBlocks();
        };
        self.unblock = function (user) {
            if (!self.canWrite()) {
                return;
            }
            return self.action("/private-messages/blocks/" + encodeURIComponent(user.id) + "/delete", undefined, self.loadBlocks);
        };
        self.block = function (user) {
            if (!self.canWrite()) {
                return;
            }
            return self.action("/private-messages/blocks/" + encodeURIComponent(user.id), undefined, self.loadBlocks);
        };
        self.activate = function () {
            var recipientId = context && context.params && context.params.recipientId;
            if (recipientId) {
                if (!self.canRead() || !self.canWrite()) {
                    self.message(i18n.t("privateMessages.forbidden"));
                    return Promise.resolve();
                }
                self.composing(true);
                var ticket = ++detailTicket;
                return api.get("/users/profiles/" + encodeURIComponent(recipientId)).then(function (response) {
                    if (!alive || ticket !== detailTicket) {
                        return;
                    }
                    if (!response.ok) {
                        failed(response);
                        return;
                    }
                    if (!response.data.permissions.canSendPrivateMessage) {
                        self.message(i18n.t("privateMessages.forbidden"));
                        return;
                    }
                    self.userIds([recipientId]);
                    self.users([{ id: recipientId, displayName: response.data.displayName }]);
                }).catch(catchError);
            }
            var id = context && context.params && context.params.id;
            return id ? self.open({ id: id }) : self.load();
        };
        self.dispose = function () {
            alive = false;
            listTicket++; detailTicket++; searchTicket++;
            identitySubscription.dispose();
            self.canRead.dispose(); self.canWrite.dispose(); self.canDelete.dispose(); self.canSystem.dispose();
            self.canManageMembership.dispose(); self.canInvite.dispose();
        };
    };
});
