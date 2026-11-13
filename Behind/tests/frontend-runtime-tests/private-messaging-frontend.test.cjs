const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key, dateTime: value => value };
let token = "user";
const grants = ko.observable(["PrivateMessage.Read", "PrivateMessage.Write", "Report.Write", "Report.Manage"]);
const session = { grants, accessToken: () => token, hasGrant: name => grants().includes(name) };

function load(file, dependencies) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) { result = factory(...names.map(name => dependencies[name])); },
        window: { confirm: () => true }, Promise, Date, Number, encodeURIComponent
    }, { filename: file });
    return result;
}

const pager = load("models/pager.js", { ko, "models/i18n": i18n });
function create(file, api) {
    const Model = load(file, { ko, "models/session": session, "models/privateapi": api, "models/pager": pager, "models/i18n": i18n });
    return new Model({});
}
function apiStub(get, post) {
    return {
        get, post, error: response => response.data?.message || "failed",
        query: values => new URLSearchParams(values).toString()
    };
}
function page(items = []) { return { items, page: 1, total: items.length, pageCount: items.length ? 1 : 0 }; }
function deferred() {
    let resolve;
    const promise = new Promise(done => { resolve = done; });
    return { promise, resolve };
}

test("profile message links preselect the exact recipient outside the search limit and enforce permissions", async () => {
    let allowed = true;
    const calls = [];
    const Model = load("viewmodels/privatemessages.js", {
        ko, "models/session": session, "models/i18n": i18n, "models/pager": pager,
        "models/privateapi": apiStub(async url => {
            calls.push(url);
            return { ok: true, data: { displayName: "Recipient#1234", permissions: { canSendPrivateMessage: allowed } } };
        })
    });
    const model = new Model({ params: { recipientId: "recipient" } });
    await model.activate();
    assert.equal(model.composing(), true);
    assert.equal(model.editorMode(), "visual");
    assert.deepEqual(Array.from(model.userIds()), ["recipient"]);
    assert.equal(model.users()[0].displayName, "Recipient#1234");
    allowed = false;
    const denied = new Model({ params: { recipientId: "self" } });
    await denied.activate();
    assert.equal(denied.userIds().length, 0);
    assert.equal(denied.message(), "privateMessages.forbidden");
    assert.deepEqual(calls, ["/users/profiles/recipient", "/users/profiles/self"]);
    const view = fs.readFileSync(path.join(core, "views/publicprofile.html"), "utf8");
    assert.match(view, /visible: permissions\.canSendPrivateMessage/);
    assert.match(view, /'#\/private-messages\/new\/' \+ encodeURIComponent\(\$root\.profileId\)/);
    model.dispose(); denied.dispose();
});

test("visual editing is the default and first option while BBCode remains available without losing content", () => {
    const model = create("viewmodels/privatemessages.js", apiStub(async () => ({ ok: true, data: page() })));
    assert.equal(model.editorMode(), "visual");
    model.content("[b]Draft[/b]");
    model.reply("[i]Reply[/i]");
    const view = fs.readFileSync(path.join(core, "views/privatemessages.html"), "utf8");
    const modes = Array.from(view.matchAll(/<div class="nav nav-pills gap-2 mb-2"[^>]*>([\s\S]*?)<\/div>/g));
    assert.equal(modes.length, 2);
    for (const mode of modes) {
        assert.ok(mode[1].indexOf("privateMessages.visual") < mode[1].indexOf("privateMessages.source"));
    }
    model.editorMode("source");
    assert.equal(model.content(), "[b]Draft[/b]");
    assert.equal(model.reply(), "[i]Reply[/i]");
    model.editorMode("visual");
    assert.equal(model.content(), "[b]Draft[/b]");
    assert.equal(model.reply(), "[i]Reply[/i]");
    assert.match(fs.readFileSync(path.join(core, "viewmodels/layouts.js"), "utf8"), /editor: ko\.observable\(data\.editor \|\| "richtext"\)/);
    const forum = fs.readFileSync(path.resolve(core, "../../../Forum/Behind.Forum.WebApi/Frontend/modules/forum/viewmodels/forum.js"), "utf8");
    assert.match(forum, /self\.editorMode = ko\.observable\("visual"\)/);
    model.dispose();
});

test("email deep links open the requested conversation and preserve login-required failures", async () => {
    const calls = [];
    let allowed = true;
    const Model = load("viewmodels/privatemessages.js", {
        ko, "models/session": session, "models/i18n": i18n, "models/pager": pager,
        "models/privateapi": apiStub(async url => {
            calls.push(url);
            return allowed ? { ok: true, data: { id: "conversation", isRead: true } }
                : { ok: false, data: { message: "Sign in required" } };
        })
    });
    const model = new Model({ params: { id: "conversation" } });
    await model.activate();
    assert.equal(model.detail().id, "conversation");
    allowed = false;
    await model.activate();
    assert.equal(model.detail(), null);
    assert.equal(model.message(), "Sign in required");
    assert.deepEqual(calls, ["/private-messages/conversation", "/private-messages/conversation"]);
    assert.match(fs.readFileSync(path.join(core, "app.js"), "utf8"), /"#\/private-messages\/:id": "controllers\/privatemessages"/);
    model.dispose();
});

test("all chats use one recipient minimum and role composition and invitations are administrator-only", async () => {
    const calls = [];
    const model = create("viewmodels/privatemessages.js", apiStub(async url => ({
        ok: true, data: url.includes("/recipients") ? { users: [], roles: [{ id: "role" }] } : page()
    }), async (url, body) => {
        calls.push({ url, body });
        return { ok: false, data: { message: "failure" } };
    }));
    await model.find();
    assert.equal(model.roles().length, 0);
    model.subject("Chat"); model.content("Body"); model.userIds(["user"]);
    await model.send();
    assert.equal(calls.length, 1);
    assert.equal("isGroup" in calls[0].body, false);
    model.roleIds(["role"]);
    await model.send();
    assert.equal(calls.length, 1);
    model.roleIds([]); model.bccRoleIds(["role"]);
    await model.send();
    assert.equal(calls.length, 1);
    model.detail({ id: "group", isGroup: true, canManage: true });
    model.inviting(true); model.inviteBccRoleIds(["role"]);
    await model.invite();
    assert.equal(calls.length, 1);
    assert.equal(model.message(), "privateMessages.rolesForbidden");
    const view = fs.readFileSync(path.join(core, "views/privatemessages.html"), "utf8");
    assert.doesNotMatch(view, /checked: isGroup/);
    assert.equal((view.match(/canRoles: (?:\$root\.)?canSystem/g) || []).length, 2);
    assert.match(fs.readFileSync(path.join(core, "components/privaterecipients.html"), "utf8"), /visible: canRoles/);
    model.dispose();
});

test("private-message pills track one active view and preserve navigation and permissions", async () => {
    const model = create("viewmodels/privatemessages.js", apiStub(async url => ({
        ok: true, data: url.includes("/recipients") ? { users: [], roles: [] } : page()
    })));
    const view = fs.readFileSync(path.join(core, "views/privatemessages.html"), "utf8");
    const navbar = view.match(/<div class="nav nav-pills gap-2 mb-3"[\s\S]*?<\/div>/)[0];
    const buttons = Array.from(navbar.matchAll(/<button type="button" class="nav-link" data-bind="([^"]+)"/g));
    assert.equal(buttons.length, 7);
    const bindings = buttons.map(button => ko.bindingProvider.instance.parseBindingsString(
        button[1], { $data: model, $root: model }, {}, { valueAccessors: true }));
    const active = () => bindings.map((binding, index) => {
        assert.equal(binding.css().active, binding.attr()["aria-pressed"]);
        return binding.css().active ? index : -1;
    }).filter(index => index >= 0);
    assert.deepEqual(active(), [0]);
    for (let index = 1; index < bindings.length; index++) {
        await bindings[index].click()();
        assert.deepEqual(active(), [index]);
    }
    await bindings[0].click()();
    assert.deepEqual(active(), [0]);
    const original = grants().slice();
    grants(original.filter(grant => grant !== "PrivateMessage.Write"));
    for (const index of [4, 5, 6]) {
        assert.equal(ko.unwrap(bindings[index].visible()), false);
    }
    grants(original);
    assert.equal((view.match(/class="nav nav-pills gap-2 mb-2"/g) || []).length, 2);
    assert.match(view, /class="btn btn-primary" data-bind="enable: !busy\(\), t: 'privateMessages.send'"/);
    model.dispose();
});

test("section and mode navigation use pills that inherit both site themes while pagers remain buttons", () => {
    const src = path.resolve(core, "../../..");
    for (const file of [
        path.join(core, "views/settings.html"),
        path.join(src, "Contact/Behind.Contact.WebApi/Frontend/modules/contact/views/contactmanager.html"),
        path.join(src, "Forum/Behind.Forum.WebApi/Frontend/modules/forum/views/forum.html")
    ]) {
        const view = fs.readFileSync(file, "utf8");
        assert.match(view, /nav nav-pills/);
        assert.doesNotMatch(view, /nav-tabs|forum-editor-modes btn-group/);
    }
    for (const site of ["Behind.Sites.FSE/wwwroot/css/fse.css", "Behind.Sites.RPDotHu/wwwroot/css/rpdothu.css"]) {
        const css = fs.readFileSync(path.join(src, "Sites", site), "utf8");
        assert.match(css, /\.nav-pills\s*\{[^}]*--bs-nav-pills-link-active-bg:/);
    }
    assert.match(fs.readFileSync(path.join(core, "components/pager.html"), "utf8"), /btn-group/);
    assert.match(fs.readFileSync(path.join(core, "components/letterpager.html"), "utf8"), /btn-group/);
});

test("compose preserves visible and hidden recipients and surfaces request failures", async () => {
    const calls = [];
    const api = apiStub(async () => ({ ok: true, data: page() }), async (url, body) => {
        calls.push({ url, body });
        return { ok: false, data: { message: "delivery failed" } };
    });
    const model = create("viewmodels/privatemessages.js", api);
    model.subject("Subject"); model.content("[b]Hello[/b]");
    model.userIds(["visible"]); model.bccUserIds(["hidden-user"]);
    await model.send();
    assert.equal(calls.length, 1);
    assert.equal(calls[0].url, "/private-messages");
    assert.deepEqual(Array.from(calls[0].body.userIds), ["visible"]);
    assert.deepEqual(Array.from(calls[0].body.bccUserIds), ["hidden-user"]);
    assert.equal(model.message(), "delivery failed");
    assert.equal(model.content(), "[b]Hello[/b]");
    model.dispose();
});

test("blocked users use an independent pager and clamp after the last item is unblocked", async () => {
    const calls = [];
    let removed = false;
    const model = create("viewmodels/privatemessages.js", apiStub(async url => {
        calls.push(url);
        const requested = Number(new URLSearchParams(url.split("?")[1]).get("page"));
        return { ok: true, data: { items: requested === 1 ? [{ id: "first", displayName: "First" }] : removed ? [] : [{ id: "last", displayName: "Last" }],
            page: requested, pageSize: 20, total: removed ? 20 : 21, pageCount: removed ? 1 : 2 } };
    }, async () => {
        removed = true;
        return { ok: true };
    }));
    model.pager.page(3);
    await model.openBlocks();
    assert.equal(model.blocksPager.pageCount(), 2);
    assert.equal(model.blocksPager.total(), 21);
    await model.blocksPager.next();
    assert.equal(model.blocks()[0].id, "last");
    await model.unblock(model.blocks()[0]);
    assert.equal(model.blocksPager.page(), 1);
    assert.equal(model.blocksPager.total(), 20);
    assert.equal(model.blocks()[0].id, "first");
    assert.equal(model.pager.page(), 3);
    assert.deepEqual(calls, [
        "/private-messages/blocks?page=1&pageSize=20", "/private-messages/blocks?page=2&pageSize=20",
        "/private-messages/blocks?page=2&pageSize=20", "/private-messages/blocks?page=1&pageSize=20"
    ]);
    const view = fs.readFileSync(path.join(core, "views/privatemessages.html"), "utf8");
    assert.match(view, /<pager params="pager: blocksPager"><\/pager>/);
    model.dispose();
});

test("stale blocked-user requests cannot replace newer pages or expose a previous identity", async () => {
    const first = deferred();
    const second = deferred();
    let count = 0;
    const model = create("viewmodels/privatemessages.js", apiStub(() => ++count === 1 ? first.promise : second.promise));
    const a = model.openBlocks();
    model.blocksPager.page(2);
    const b = model.loadBlocks();
    second.resolve({ ok: true, data: { items: [{ id: "new" }], page: 2, total: 21, pageCount: 2 } });
    await b;
    first.resolve({ ok: true, data: { items: [{ id: "old" }], page: 1, total: 21, pageCount: 2 } });
    await a;
    assert.equal(model.blocks()[0].id, "new");
    assert.equal(model.blocksPager.page(), 2);
    const pending = deferred();
    const privateModel = create("viewmodels/privatemessages.js", apiStub(() => pending.promise));
    const request = privateModel.openBlocks();
    token = "other";
    grants(grants().slice());
    pending.resolve({ ok: true, data: { items: [{ id: "secret" }], page: 1, total: 1, pageCount: 1 } });
    await request;
    assert.equal(privateModel.blocks().length, 0);
    assert.equal(model.blocksPager.total(), 0);
    assert.equal(model.blocksPager.page(), 1);
    model.dispose(); privateModel.dispose();
    token = "user";
});

test("handled reports display a localized administrator profile link", async () => {
    const model = create("viewmodels/reports.js", apiStub(async () => ({
        ok: true, data: { id: "report", status: "dismissed", resolvedById: "admin", resolvedByName: "Administrator#1234" }
    })));
    await model.open({ id: "report" });
    assert.equal(model.detail().resolvedByName, "Administrator#1234");
    const view = fs.readFileSync(path.join(core, "views/reports.html"), "utf8");
    assert.match(view, /visible: \$data\.resolvedById/);
    assert.match(view, /t: 'reports.handledBy'/);
    assert.match(view, /text: \$data\.resolvedByName/);
    assert.match(view, /'#\/user\/' \+ encodeURIComponent\(\$data\.resolvedById\)/);
    model.dispose();
});

test("stale conversation requests and identity changes clear private data", async () => {
    const first = deferred();
    const second = deferred();
    const api = apiStub(url => url.endsWith("/first") ? first.promise : second.promise, async () => ({ ok: true }));
    const model = create("viewmodels/privatemessages.js", api);
    const a = model.open({ id: "first" });
    const b = model.open({ id: "second" });
    second.resolve({ ok: true, data: { id: "second", isRead: true } });
    await b;
    first.resolve({ ok: true, data: { id: "first", isRead: true } });
    await a;
    assert.equal(model.detail().id, "second");
    token = "other";
    grants(grants().slice());
    assert.equal(model.detail(), null);
    model.dispose();
    token = "user";
});

test("replies preserve explicit parent and are gated by canReply", async () => {
    const calls = [];
    const api = apiStub(async () => ({ ok: true, data: { id: "thread", isRead: true, canReply: true } }), async (url, body) => {
        calls.push({ url, body });
        return { ok: true };
    });
    const model = create("viewmodels/privatemessages.js", api);
    model.detail({ id: "thread", canReply: false });
    model.reply("hello");
    await model.sendReply();
    assert.equal(calls.length, 0);
    model.detail({ id: "thread", canReply: true });
    model.selectReply({ id: "parent", content: "quoted" });
    await model.sendReply();
    assert.equal(calls[0].body.replyToId, "parent");
    assert.equal(model.reply(), "");
    model.detail({ id: "thread", messages: [{ id: "visible", content: "Visible" }] });
    assert.equal(model.parentOf({ replyToId: "hidden" }), null);
    assert.equal(model.parentOf({ replyToId: "visible" }).content, "Visible");
    model.dispose();
});

test("group owners invite visible and hidden users and roles without revealing prior hidden recipients", async () => {
    const original = grants().slice();
    grants([...original, "PrivateMessageModeration.Manage"]);
    const calls = [];
    const api = apiStub(async url => {
        if (url.startsWith("/private-messages/recipients")) {
            return { ok: true, data: { users: [{ id: "alice", displayName: "Alice" }], roles: [] } };
        }
        return { ok: true, data: { id: "group", isGroup: true, isRead: true, canManage: true, participants: [], messages: [] } };
    }, async (url, body) => {
        calls.push({ url, body });
        return { ok: true };
    });
    const model = create("viewmodels/privatemessages.js", api);
    model.detail({ id: "group", isGroup: true, canManage: true });
    await model.startInvite();
    assert.equal(model.inviting(), true);
    assert.equal(model.inviteBccUserIds().length, 0);
    model.inviteUserIds(["alice"]); model.inviteRoleIds(["role"]);
    model.inviteBccUserIds(["hidden"]); model.inviteBccRoleIds(["hidden-role"]);
    await model.invite();
    assert.equal(calls[0].url, "/private-messages/group/participants");
    assert.deepEqual(Array.from(calls[0].body.userIds), ["alice"]);
    assert.deepEqual(Array.from(calls[0].body.roleIds), ["role"]);
    assert.deepEqual(Array.from(calls[0].body.bccUserIds), ["hidden"]);
    assert.deepEqual(Array.from(calls[0].body.bccRoleIds), ["hidden-role"]);
    assert.equal(model.inviting(), false);
    assert.equal(model.inviteBccUserIds().length, 0);
    assert.equal(model.detail().participants.length, 0);
    model.dispose();
    grants(original);
});

test("group invitations and membership require grants and ordinary manageable group", async () => {
    const calls = [];
    const model = create("viewmodels/privatemessages.js", apiStub(async () => ({ ok: true, data: { users: [], roles: [] } }), async (url, body) => {
        calls.push({ url, body });
        return { ok: true };
    }));
    for (const detail of [
        { id: "peer", isGroup: false, canManage: true },
        { id: "system", isGroup: true, isSystem: true, canManage: true },
        { id: "report", isGroup: true, isReport: true, canManage: true },
        { id: "non-owner", isGroup: true, canManage: false }
    ]) {
        model.detail(detail);
        assert.equal(model.canInvite(), false);
        model.inviting(true); model.inviteUserIds(["user"]);
        await model.invite();
        if (detail.id !== "non-owner") {
            assert.equal(model.canManageMembership(), false);
            await model.conversationAction("leave");
        }
    }
    model.detail({ id: "group", isGroup: true, canManage: true });
    const original = grants().slice();
    grants(original.filter(grant => grant !== "PrivateMessage.Write"));
    assert.equal(model.canInvite(), false);
    await model.invite();
    await model.conversationAction("leave");
    assert.equal(calls.length, 0);
    grants(original);
    model.dispose();
});

test("private mailbox delete requires the delete grant", async () => {
    const calls = [];
    const model = create("viewmodels/privatemessages.js", apiStub(async () => ({ ok: true, data: page() }), async url => {
        calls.push(url);
        return { ok: true };
    }));
    model.detail({ id: "thread", isGroup: false });
    await model.conversationAction("delete");
    assert.equal(calls.length, 0);
    const original = grants().slice();
    grants([...original, "PrivateMessage.Delete"]);
    await model.conversationAction("delete");
    assert.deepEqual(calls, ["/private-messages/thread/delete"]);
    grants(original);
    model.dispose();
});

test("invitation failure preserves choices and leaving a group allows rejoining from left folder", async () => {
    const calls = [];
    const api = apiStub(async url => {
        if (url.includes("folder=left")) {
            return { ok: true, data: page([{ id: "group", subject: "Group", isGroup: true }]) };
        }
        return { ok: true, data: page() };
    }, async (url, body) => {
        calls.push({ url, body });
        return url.endsWith("/participants") ? { ok: false, data: { message: "blocked" } } : { ok: true };
    });
    const model = create("viewmodels/privatemessages.js", api);
    model.detail({ id: "group", isGroup: true, canManage: true });
    model.inviting(true); model.inviteBccUserIds(["user"]);
    await model.invite();
    assert.equal(model.message(), "blocked");
    assert.deepEqual(Array.from(model.inviteBccUserIds()), ["user"]);
    await model.conversationAction("leave");
    assert.equal(model.folder(), "left");
    assert.equal(model.items()[0].id, "group");
    assert.equal(model.inviteBccUserIds().length, 0);
    await model.rejoin(model.items()[0]);
    assert.equal(calls.at(-1).url, "/private-messages/group/join");
    assert.equal(model.folder(), "inbox");
    model.dispose();
});

test("late invitation search does not restore recipient choices after leaving detail", async () => {
    const search = deferred();
    const api = apiStub(url => url.includes("/recipients?") ? search.promise : Promise.resolve({ ok: true, data: page() }), async () => ({ ok: true }));
    const model = create("viewmodels/privatemessages.js", api);
    model.detail({ id: "group", isGroup: true, canManage: true });
    const pending = model.startInvite();
    await model.selectFolder("inbox");
    search.resolve({ ok: true, data: { users: [{ id: "secret", displayName: "Secret" }], roles: [] } });
    await pending;
    assert.equal(model.inviting(), false);
    assert.equal(model.users().length, 0);
    model.dispose();
});

test("recipient selector is reused and left groups only show rejoin", () => {
    const view = fs.readFileSync(path.join(core, "views/privatemessages.html"), "utf8");
    const selector = fs.readFileSync(path.join(core, "components/privaterecipients.html"), "utf8");
    assert.equal((view.match(/<private-recipients /g) || []).length, 2);
    assert.match(view, /folder\(\) === 'left' && \$root\.canWrite\(\).*click: \$root\.rejoin/);
    assert.match(view, /visible: \$root\.canInvite\(\)/);
    assert.match(selector, /\$parent\.bccUserIds/);
    assert.match(selector, /\$parent\.bccRoleIds/);
});

test("public chat reports identify anonymous messages by source id and enforce Report.Write", async () => {
    const widget = fs.readFileSync(path.join(core, "components/sitewidget.html"), "utf8");
    const reportMarkup = widget.match(/<report-button params="([^"]+)"><\/report-button>/g);
    assert.deepEqual(reportMarkup, ["<report-button params=\"sourceType: 'chatMessage', sourceId: id\"></report-button>"]);
    assert.match(widget, /<ul class="chat-messages" data-bind="foreach: messages">[\s\S]*<report-button params="sourceType: 'chatMessage', sourceId: id">/);

    const calls = [];
    const component = load("components/reportbutton.js", {
        ko, "models/session": session, "models/i18n": i18n,
        "models/privateapi": apiStub(async () => ({ ok: true, data: { users: [] } }), async (url, body) => {
            calls.push({ url, body });
            return { ok: true };
        }),
        "text!components/reportbutton.html": ""
    });
    const anonymousMessage = { id: "message-id", authorId: null, isAnonymous: true };
    const model = component.viewModel.createViewModel({ sourceType: "chatMessage", sourceId: anonymousMessage.id });
    model.reason("abuse");
    const original = grants().slice();
    grants(original.filter(grant => grant !== "Report.Write"));
    assert.equal(model.allowed(), false);
    await model.submit();
    assert.equal(calls.length, 0);
    grants(original);
    assert.equal(model.allowed(), true);
    await model.submit();
    assert.equal(calls[0].url, "/reports");
    assert.equal(calls[0].body.sourceType, "chatMessage");
    assert.equal(calls[0].body.sourceId, anonymousMessage.id);
    assert.equal(calls[0].body.reason, "abuse");
    assert.equal(calls[0].body.userIds.length, 0);
    assert.equal("authorId" in calls[0].body, false);
    model.dispose();
});

test("general reports require explicit users and have no source", async () => {
    const calls = [];
    const component = load("components/reportbutton.js", {
        ko, "models/session": session, "models/i18n": i18n,
        "models/privateapi": apiStub(async () => ({ ok: true, data: { users: [] } }), async (url, body) => {
            calls.push({ url, body });
            return { ok: true };
        }),
        "text!components/reportbutton.html": ""
    });

    const model = component.viewModel.createViewModel({});
    model.reason("reason");
    await model.submit();
    assert.equal(calls.length, 0);
    model.selected(["user-a", "user-b"]);
    await model.submit();
    assert.deepEqual(Array.from(calls[0].body.userIds), ["user-a", "user-b"]);
    assert.equal(calls[0].body.sourceType, null);
    assert.equal(calls[0].body.sourceId, null);
    model.dispose();
});

test("report target browsing and search request human-only recipients", async () => {
    const calls = [];
    const component = load("components/reportbutton.js", {
        ko, "models/session": session, "models/i18n": i18n,
        "models/privateapi": apiStub(async url => {
            calls.push(url);
            return { ok: true, data: { users: [{ id: "human", displayName: "Human" }] } };
        }),
        "text!components/reportbutton.html": ""
    });
    const model = component.viewModel.createViewModel({});
    await model.find();
    model.search("Human");
    await model.find();
    assert.deepEqual(calls, [
        "/private-messages/recipients?search=&reportTargets=true",
        "/private-messages/recipients?search=Human&reportTargets=true"
    ]);
    assert.equal(model.users()[0].id, "human");
    model.dispose();
});

test("shared BBCode styles load in Core and both site shells without Forum", () => {
    const css = fs.readFileSync(path.join(core, "css/app.css"), "utf8");
    assert.match(css, /\.behind-bbcode a\[data-forum-url\]/);
    assert.match(css, /\.behind-bbcode blockquote/);
    assert.match(css, /\.behind-bbcode pre/);
    const base = path.resolve(__dirname, "../../src/Sites");
    for (const index of [
        path.join(core, "index.html"),
        path.join(base, "Behind.Sites.FSE/wwwroot/index.html"),
        path.join(base, "Behind.Sites.RPDotHu/wwwroot/index.html")
    ]) {
        assert.match(fs.readFileSync(index, "utf8"), /href="css\/app\.css"/);
    }
});

test("messaging and reporting locale keys exist in every Core language", () => {
    const locales = fs.readdirSync(path.join(core, "locales")).filter(name => name.endsWith(".json"));
    const english = JSON.parse(fs.readFileSync(path.join(core, "locales/en-US.json"), "utf8")).messages;
    const keys = Object.keys(english).filter(key => key.startsWith("privateMessages.") || key.startsWith("reports."));
    for (const locale of locales) {
        const messages = JSON.parse(fs.readFileSync(path.join(core, "locales", locale), "utf8")).messages;
        for (const key of keys) {
            assert.ok(messages[key], `${locale}: ${key}`);
        }
        for (const entity of ["PrivateConversation", "PrivateConversationRecipient", "PrivateMessage", "PrivateMailbox", "PrivateMessageRead", "PrivateUserBlock", "UserReport", "ReportEmailDelivery"]) {
            assert.ok(messages["audit.entities." + entity], `${locale}: ${entity}`);
            assert.match(fs.readFileSync(path.join(core, "viewmodels/auditlog.js"), "utf8"), new RegExp(entity + ': "audit.entities.' + entity + '"'));
        }
        assert.ok(messages["menu.item.privateMessages"], `${locale}: private message menu`);
    }
});

test("report component submits source without requiring an anonymous author id", async () => {
    const calls = [];
    const component = load("components/reportbutton.js", {
        ko, "models/session": session, "models/i18n": i18n,
        "models/privateapi": apiStub(async () => ({ ok: true, data: { users: [] } }), async (url, body) => {
            calls.push({ url, body });
            return { ok: true };
        }),
        "text!components/reportbutton.html": ""
    });
    const model = component.viewModel.createViewModel({ sourceType: "newsComment", sourceId: "comment" });
    model.reason("reason");
    await model.submit();
    assert.equal(calls[0].body.sourceType, "newsComment");
    assert.equal(calls[0].body.sourceId, "comment");
    assert.equal(calls[0].body.userIds.length, 0);
    model.dispose();
});

test("report ban date validation and developer deletion gate", async () => {
    const calls = [];
    const api = apiStub(async () => ({ ok: true, data: { id: "report", status: "resolved" } }), async (url, body) => {
        calls.push({ url, body });
        return { ok: true };
    });
    const model = create("viewmodels/reports.js", api);
    model.detail({ id: "report" });
    model.banUntil("2000-01-01T00:00");
    await model.resolve("ban");
    assert.equal(calls.length, 0);
    await model.action("hard/delete");
    assert.equal(calls.length, 0);
    model.banUntil("");
    await model.resolve("ban");
    assert.equal(calls[0].body.banUntil, null);
    assert.equal(calls[0].body.action, "ban");
    model.dispose();
});

test("report moderators can load discussions without private-message read permission", async () => {
    const calls = [];
    const original = grants().slice();
    grants(["Report.Manage"]);
    const model = create("viewmodels/reports.js", apiStub(async url => {
        calls.push(url);
        if (url === "/reports/report%2Fid") {
            return { ok: true, data: { id: "report/id", conversationId: "conversation", status: "open" } };
        }
        return { ok: true, data: { id: "conversation", messages: [{ id: "message", content: "Discussion" }] } };
    }));
    await model.open({ id: "report/id" });
    assert.deepEqual(calls, ["/reports/report%2Fid", "/reports/report%2Fid/conversation"]);
    assert.equal(model.conversation().messages[0].content, "Discussion");
    model.dispose();
    grants(original);
});

test("deleted reports omit discussion and stale discussion responses do not replace newer details", async () => {
    const conversation = deferred();
    const calls = [];
    const model = create("viewmodels/reports.js", apiStub(url => {
        calls.push(url);
        if (url === "/reports/first/conversation") {
            return conversation.promise;
        }
        return Promise.resolve({ ok: true, data: { id: url, conversationId: "thread", status: "open" } });
    }));
    const first = model.open({ id: "first" });
    await Promise.resolve();
    model.showDeleted(true);
    await model.open({ id: "second" });
    conversation.resolve({ ok: true, data: { id: "old", messages: [] } });
    await first;
    assert.deepEqual(calls, ["/reports/first", "/reports/first/conversation", "/reports/second?deleted=true"]);
    assert.equal(model.conversation(), null);
    assert.equal(model.detail().id, "/reports/second?deleted=true");
    model.dispose();
});
