const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function load(file, dependencies) {
    dependencies["models/user"] = { href: id => id ? "#/user/" + encodeURIComponent(id) : null };
    dependencies["models/contentlocale"] ||= {
        current: ko.observable(null),
        assignmentRevision: ko.observable(0)
    };
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise,
        console,
        window: { setInterval: callback => { dependencies.tick = callback; return 42; },
            clearInterval: id => dependencies.cleared.push(id), confirm: () => true,
            localStorage: dependencies.storage }
    }, { filename: file });
    return result;
}

async function settle() {
    await new Promise(resolve => setImmediate(resolve));
}

function chatFixture(http, grants = ["Chat.Read", "Chat.Write"], signedIn = true,
    widgetConfig = { type: "core.chat" }, storage) {
    const access = ko.observableArray(grants);
    const token = ko.observable(signedIn ? "token" : null);
    const dependencies = {
        require() {}, ko,
        "models/widgets": { catalog: () => [] },
        "models/articles": {}, "models/menus": {},
        "models/http": http,
        "models/session": {
            isSignedIn: ko.pureComputed(() => !!token()),
            accessToken: token,
            hasGrant: grant => access().includes(grant)
        },
        "models/i18n": { t: key => key },
        "models/invalidation": { subscribe: () => ({ dispose() {} }) },
        "text!components/sitewidget.html": "",
        cleared: [], storage
    };
    const widget = load("components/sitewidget.js", dependencies).viewModel
        .createViewModel({ widget: widgetConfig });
    return { widget, access, token, dependencies };
}

function messageFixture(http, storage = { getItem: () => null, setItem() {} }) {
    return chatFixture(http, [], false, { type: "core.message", message: "notice" }, storage);
}

test("message widgets follow schedule availability through polling and clear their timer", async () => {
    let response = { ok: false, status: 404 };
    const urls = [];
    const fixture = messageFixture({ get: async url => { urls.push(url); return response; } });
    await settle();
    assert.equal(fixture.widget.messageVisible(), false);
    assert.equal(fixture.widget.error(), "");
    response = { ok: true, data: { id: "notice", version: 1, title: "", content: "<p>Open</p>", level: "warning" } };
    fixture.dependencies.tick();
    await settle();
    assert.equal(fixture.widget.messageVisible(), true);
    assert.equal(fixture.widget.message().content, "<p>Open</p>");
    assert.match(fixture.widget.messageClass(), /alert-warning/);
    response = { ok: false, status: 404 };
    fixture.dependencies.tick();
    await settle();
    assert.equal(fixture.widget.messageVisible(), false);
    assert.ok(urls.every(url => url === "/widgets/messages/notice"));
    fixture.widget.dispose();
    assert.deepEqual(fixture.dependencies.cleared, [42]);
});

test("message dismissal persists per version and edits show the message again", async () => {
    const stored = new Map();
    const storage = { getItem: key => stored.get(key) ?? null, setItem: (key, value) => stored.set(key, value) };
    let version = 1;
    const http = { get: async () => ({ ok: true, data: { id: "notice", version, content: "Text", level: "info" } }) };
    const first = messageFixture(http, storage);
    await settle();
    first.widget.dismissMessage();
    assert.equal(stored.get("behind.message.dismissed.notice.1"), "1");
    assert.equal(first.widget.messageVisible(), false);
    first.widget.dispose();
    const next = messageFixture(http, storage);
    await settle();
    assert.equal(next.widget.messageVisible(), false);
    version = 2;
    await next.widget.refresh();
    assert.equal(next.widget.messageVisible(), true);
    next.widget.dispose();
});

test("message storage failures preserve in-memory dismissal", async () => {
    const storage = {
        getItem() { throw new Error("storage blocked"); },
        setItem() { throw new Error("storage blocked"); }
    };
    const { widget } = messageFixture({
        get: async () => ({ ok: true, data: { id: "notice", version: 1, content: "Text", level: "success" } })
    }, storage);
    await settle();
    widget.dismissMessage();
    await widget.refresh();
    assert.equal(widget.messageVisible(), false);
    widget.dispose();
});

test("message request errors are surfaced and late responses cannot resurrect a disposed widget", async () => {
    let finish;
    let reads = 0;
    const { widget } = messageFixture({
        get: () => ++reads === 1 ? Promise.resolve({ ok: false, status: 500 })
            : new Promise(resolve => { finish = resolve; })
    });
    await settle();
    assert.ok(widget.error());
    const pending = widget.refresh();
    widget.dispose();
    finish({ ok: true, data: { id: "notice", version: 1, content: "Text", level: "danger" } });
    await pending;
    assert.equal(widget.message(), null);
});

function message(id, text = id) {
    return {
        id, text, author: "Ada", version: 1, profilePictureUrl: null,
        createdAt: new Date().toISOString(), updatedAt: null, replyToId: null, reply: null,
        editableUntil: new Date(Date.now() + 300000).toISOString(), canEdit: true, canDelete: true
    };
}

function page(items, nextCursor = null) {
    return { ok: true, data: { items, nextCursor, canWrite: true } };
}

test("widget template supplies Bootstrap skins while preserving widget bindings", () => {
    const template = fs.readFileSync(path.join(core, "components/sitewidget.html"), "utf8");
    assert.match(template, /<section class="site-widget behind-widget card">/);
    assert.match(template, /<h2 class="card-header h5 mb-0" data-bind="visible: title, text: title">/);
    assert.match(template, /<div class="card-body">/);
    assert.match(template, /class="alert alert-danger" role="alert" data-bind="visible: error, text: error"/);
    assert.match(template, /class="list-group list-group-flush" data-bind="foreach: items, visible: items\(\)\.length"/);
    assert.match(template, /class="list-group-item"><a data-bind="attr: \{ href: href \}, text: title"/);
    assert.match(template, /class="row mb-0" data-bind="foreach: stats, visible: stats\(\)\.length"/);
    assert.match(template, /<menu-items params="items: menuItems/);
    assert.match(template, /data-bind="html: content"/);
    assert.match(template, /class="chat-history"[^>]*event: \{ scroll: scrollChat \}/);
});

test("bundled site themes customize the shared widget card", () => {
    for (const [site, stylesheet, color] of [
        ["FSE", "fse.css", "fse-green"],
        ["RPDotHu", "rpdothu.css", "rp-blue"]
    ]) {
        const css = fs.readFileSync(path.resolve(__dirname,
            `../../src/Sites/Behind.Sites.${site}/wwwroot/css/${stylesheet}`), "utf8");
        assert.match(css, new RegExp(`\\.behind-layout \\.behind-widget \\{\\s*border-color: var\\(--${color}\\);`));
        assert.match(css, new RegExp(`\\.behind-layout \\.behind-widget > \\.card-header \\{[^}]*background-color: var\\(--bs-primary-bg-subtle\\);[^}]*border-bottom-color: var\\(--${color}\\);`));
        assert.doesNotMatch(css, /\.behind-layout \.behind-widget h2\s*\{/);
    }
});

test("chat scroll loads older history once, deduplicates messages and refresh preserves loaded pages", async () => {
    const reads = [];
    let resolveOlder;
    let refreshing = false;
    const fixture = chatFixture({
        get: url => {
            reads.push(url);
            if (url.includes("before=") && !refreshing) {
                return new Promise(resolve => { resolveOlder = resolve; });
            }
            return Promise.resolve(url.includes("before=")
                ? page([message("older", "edited older")])
                : page([message("latest")], "latest"));
        }
    });
    const { widget } = fixture;
    await settle();
    assert.equal(widget.hasOlder(), true);
    const older = widget.loadOlder();
    await widget.loadOlder();
    assert.equal(reads.length, 2);
    resolveOlder(page([message("older"), message("latest")]));
    await older;
    assert.deepEqual(Array.from(widget.messages(), item => item.id), ["older", "latest"]);
    assert.equal(widget.hasOlder(), false);
    refreshing = true;
    await widget.refresh();
    assert.equal(widget.messages()[0].text, "edited older");
    assert.equal(reads.length, 4);
    widget.dispose();
});

test("scroll triggers loading near the top and retries older history after failure without losing messages", async () => {
    let olderReads = 0;
    const { widget } = chatFixture({
        get: async url => url.includes("before=")
            ? ++olderReads === 1 ? { ok: false, status: 500 } : page([message("old")])
            : page([message("new")], "new")
    });
    await settle();
    const event = { currentTarget: { scrollHeight: 1000, scrollTop: 850, clientHeight: 100 } };
    widget.scrollChat(null, event);
    await settle();
    assert.equal(olderReads, 0);
    event.currentTarget.scrollTop = 20;
    widget.scrollChat(null, event);
    await settle();
    assert.equal(widget.messages().length, 1);
    assert.equal(widget.hasOlder(), true);
    assert.ok(widget.error());
    widget.scrollChat(null, event);
    await settle();
    assert.equal(widget.messages().length, 2);
    assert.deepEqual(Array.from(widget.messages(), item => item.id), ["old", "new"]);
    assert.equal(widget.error(), "");
    widget.dispose();
});

test("refresh retains the oldest loaded message when new messages push it onto another page", async () => {
    let updated = false;
    const now = new Date().toISOString();
    const old = { ...message("a"), createdAt: now };
    const fixture = chatFixture({
        get: async url => {
            if (!updated) {
                return page([old], "a");
            }
            return url.includes("before=") ? page([old], "a") : page([{ ...message("z"), createdAt: now }], "z");
        }
    });
    await settle();
    updated = true;
    await fixture.widget.refresh();
    assert.deepEqual(Array.from(fixture.widget.messages(), item => item.id), ["a", "z"]);
    fixture.widget.dispose();
});

test("reply, edit and removal use the corresponding endpoints and optimistic version", async () => {
    const calls = [];
    const item = message("parent");
    const { widget } = chatFixture({
        get: async () => page([item]),
        post: async (url, body) => { calls.push({ method: "post", url, body }); return { ok: true }; },
        put: async (url, body) => { calls.push({ method: "put", url, body }); return { ok: true }; },
        delete: async url => { calls.push({ method: "delete", url }); return { ok: true }; }
    });
    await settle();
    widget.reply(item);
    widget.text(" reply ");
    await widget.send();
    assert.equal(calls[0].body.replyToId, "parent");
    assert.equal(widget.replyingTo(), null);
    assert.equal(widget.text(), "");
    widget.edit(item);
    widget.editText(" changed ");
    await widget.saveEdit();
    assert.equal(calls[1].url, "/widgets/chat/parent");
    assert.equal(calls[1].body.text, "changed");
    assert.equal(calls[1].body.version, 1);
    assert.equal(widget.editing(), null);
    await widget.remove(item);
    assert.equal(calls[2].method, "delete");
    assert.equal(calls[2].url, "/widgets/chat/parent");
    widget.dispose();
});

test("anonymous chat drafts and reply previews never manufacture a public profile link", async () => {
    const calls = [];
    const item = { ...message("hidden"), author: "", authorId: null, isAnonymous: true };
    const { widget, token } = chatFixture({
        get: async () => page([item]),
        post: async (url, body) => { calls.push(body); return { ok: true }; },
        put: async (url, body) => { calls.push(body); return { ok: true }; }
    });
    await settle();
    assert.equal(widget.author(item), "comments.anonymous");
    assert.equal(widget.userHref(item.authorId), null);
    assert.equal(widget.author({ ...item, authorId: "real", author: "Moderator-visible" }), "Moderator-visible");
    assert.equal(widget.userHref("real"), "#/user/real");
    widget.reply(item);
    widget.text("reply");
    widget.isAnonymous(true);
    await widget.send();
    assert.equal(calls[0].isAnonymous, true);
    assert.equal(calls[0].authorId, undefined);
    assert.equal(widget.isAnonymous(), false);
    widget.edit(item);
    assert.equal(widget.editAnonymous(), true);
    widget.editAnonymous(false);
    await widget.saveEdit();
    assert.equal(calls[1].isAnonymous, false);
    widget.isAnonymous(true);
    token(null);
    await settle();
    assert.equal(widget.isAnonymous(), false);
    assert.equal(widget.chatWritable(), false);
    widget.dispose();
});

test("failed edits keep the draft and expired messages cannot be edited", async () => {
    const item = message("id");
    const { widget } = chatFixture({
        get: async () => page([item]),
        put: async () => ({ ok: false, status: 409 })
    });
    await settle();
    widget.edit(item);
    widget.editText("draft");
    await widget.saveEdit();
    assert.equal(widget.editText(), "draft");
    assert.equal(widget.editing().id, "id");
    assert.ok(widget.error());
    assert.equal(widget.canEdit(item), true);
    assert.equal(widget.canEdit({ ...item, editableUntil: new Date(Date.now() - 1).toISOString() }), false);
    widget.dispose();
});

test("moderation refresh removes selected reply text and cancels editing a removed message", async () => {
    let items = [message("parent")];
    const { widget } = chatFixture({ get: async () => page(items) });
    await settle();
    widget.reply(items[0]);
    items = [];
    await widget.refresh();
    assert.equal(widget.replyingTo(), null);
    items = [message("parent")];
    await widget.refresh();
    widget.edit(items[0]);
    items = [];
    await widget.refresh();
    assert.equal(widget.editing(), null);
    assert.equal(widget.editText(), "");
    widget.dispose();
});

test("read restriction clears history immediately and rejects pending responses; write restriction hides composer", async () => {
    let resolveOld;
    let reads = 0;
    const fixture = chatFixture({
        get: () => ++reads === 1 ? Promise.resolve(page([message("first")]))
            : new Promise(resolve => { resolveOld = resolve; })
    });
    const { widget, access } = fixture;
    await settle();
    assert.equal(widget.messages().length, 1);
    access(["Chat.Read"]);
    assert.equal(widget.chatWritable(), false);
    access([]);
    assert.equal(widget.chatReadable(), false);
    assert.equal(widget.messages().length, 0);
    resolveOld(page([message("stale")]));
    await settle();
    assert.equal(widget.messages().length, 0);
    assert.equal(widget.hasOlder(), false);
    widget.dispose();
});

test("guests can read but cannot send, and server read denial clears previously loaded messages", async () => {
    let reads = 0;
    const { widget } = chatFixture({
        get: async () => ++reads === 1 ? page([message("public")]) : { ok: false, status: 403 },
        post: () => { throw new Error("Guests must not post"); }
    }, [], false);
    await settle();
    assert.equal(widget.chatReadable(), true);
    assert.equal(widget.chatWritable(), false);
    assert.equal(widget.messages().length, 1);
    widget.text("guest");
    await widget.send();
    await widget.refresh();
    assert.equal(widget.messages().length, 0);
    widget.dispose();
});

test("disposal rejects pending older pages and polling does not overlap reads", async () => {
    let reads = 0;
    let resolveOlder;
    const { widget, dependencies } = chatFixture({
        get: url => {
            reads++;
            return url.includes("before=") ? new Promise(resolve => { resolveOlder = resolve; })
                : Promise.resolve(page([message("new")], "new"));
        }
    });
    await settle();
    const older = widget.loadOlder();
    dependencies.tick();
    assert.equal(reads, 2);
    widget.dispose();
    resolveOlder(page([message("old")]));
    await older;
    assert.equal(widget.messages().length, 1);
    assert.deepEqual(dependencies.cleared, [42]);
});

test("catalog contains core entries and installed module entries", () => {
    const model = load("models/widgets.js", {
        "models/modules": { widgets: () => [{ key: "forum.posts", name: "widgets.forumPosts" }] }
    });
    assert.deepEqual(Array.from(model.catalog(), item => item.key), [
        "core.header", "core.content", "core.footer", "core.news", "core.articles", "core.downloads",
        "core.menu-horizontal", "core.menu-vertical", "core.custom", "core.chat", "core.message", "forum.posts"
    ]);
});

test("chat sends text, refreshes on invalidation, and disposes its timer and listener", async () => {
    let handler;
    let disposed = false;
    const posts = [];
    let reads = 0;
    const dependencies = {
        require() {},
        ko,
        "models/widgets": { catalog: () => [] },
        "models/news": {},
        "models/articles": {},
        "models/menus": {},
        "models/http": {
            get: async () => { reads++; return { ok: true, data: { items: [{ author: "<b>", text: "<script>" }], nextCursor: null } }; },
            post: async (url, body) => { posts.push(body); return { ok: true, data: {} }; }
        },
        "models/session": { isSignedIn: ko.observable(true), accessToken: ko.observable("signed-in"), hasGrant: () => true },
        "models/i18n": { t: key => key },
        "models/invalidation": {
            subscribe(topic, callback) {
                if (topic === "widget:core.chat") {
                    handler = callback;
                } else {
                    assert.equal(topic, "widgets");
                }
                return { dispose() { disposed = true; } };
            },
            invalidate(topic) {
                assert.equal(topic, "widget:core.chat");
                return handler();
            }
        },
        "text!components/sitewidget.html": "",
        cleared: []
    };
    const component = load("components/sitewidget.js", dependencies);
    const widget = component.viewModel.createViewModel({ widget: { type: "core.chat", limit: 5 } });
    await settle();
    assert.equal(widget.messages()[0].text, "<script>");
    widget.text("  Hello  ");
    await widget.send();
    assert.equal(posts[0].text, "Hello");
    await handler();
    assert.equal(reads, 3);
    widget.dispose();
    assert.equal(disposed, true);
    assert.deepEqual(dependencies.cleared, [42]);
});

test("custom content is passed through the server sanitizer before rendering", async () => {
    let submitted;
    const dependencies = {
        require() {}, ko,
        "models/widgets": { catalog: () => [] },
        "models/news": {}, "models/articles": {}, "models/menus": {},
        "models/http": {
            post: async (url, body) => {
                submitted = { url, body };
                return { ok: true, data: { content: "<p>safe</p>" } };
            }
        },
        "models/session": { isSignedIn: ko.observable(false), accessToken: ko.observable(null) },
        "models/i18n": { t: key => key },
        "models/invalidation": { subscribe: () => ({ dispose() {} }) },
        "text!components/sitewidget.html": "",
        cleared: []
    };
    const component = load("components/sitewidget.js", dependencies);
    const widget = component.viewModel.createViewModel({ widget: { type: "core.custom", content: "<script>x</script>" } });
    await settle();
    assert.equal(submitted.url, "/widgets/sanitize");
    assert.equal(submitted.body.content, "<script>x</script>");
    assert.equal(widget.content(), "<p>safe</p>");
    widget.dispose();
});

test("menu widgets own and dispose their menu follower", async () => {
    let disposed = false;
    let followed;
    const menu = ko.observable({ items: [{ title: "Test" }] });
    menu.dispose = () => { disposed = true; };
    const dependencies = {
        require() {}, ko,
        "models/widgets": { catalog: () => [] },
        "models/news": {}, "models/articles": {},
        "models/menus": { follow: name => { followed = name; return menu; } },
        "models/http": {},
        "models/session": { isSignedIn: ko.observable(false), accessToken: ko.observable(null) },
        "models/i18n": { t: key => key },
        "models/invalidation": { subscribe: () => ({ dispose() {} }) },
        "text!components/sitewidget.html": "",
        cleared: []
    };
    const component = load("components/sitewidget.js", dependencies);
    const widget = component.viewModel.createViewModel({
        widget: { type: "core.menu-vertical", menu: "Footer" }
    });
    await settle();
    assert.equal(followed, "Footer");
    assert.equal(widget.menuItems()[0].title, "Test");
    widget.dispose();
    assert.equal(disposed, true);
});

test("identity changes clear content immediately and reject previous identity responses", async () => {
    let resolveOld;
    let resolveNew;
    let calls = 0;
    const token = ko.observable("old");
    const dependencies = {
        require() {}, ko,
        "models/widgets": { catalog: () => [] },
        "models/news": {
            list: () => {
                calls++;
                if (calls === 1) {
                    return Promise.resolve({ ok: true, data: { news: { items: [{ id: 1, title: "Protected" }] } } });
                }
                return new Promise(resolve => {
                    if (calls === 2) {
                        resolveOld = resolve;
                    } else {
                        resolveNew = resolve;
                    }
                });
            }
        },
        "models/articles": {}, "models/menus": {}, "models/http": {},
        "models/session": { isSignedIn: ko.observable(true), accessToken: token },
        "models/i18n": { t: key => key },
        "models/invalidation": { subscribe: () => ({ dispose() {} }) },
        "text!components/sitewidget.html": "",
        cleared: []
    };
    dependencies["models/articles"] = dependencies["models/news"];
    const articleList = dependencies["models/articles"].list;
    dependencies["models/articles"].list = () => articleList().then(result => ({
        ok: result.ok, data: { articles: result.data.news }
    }));
    const widget = load("components/sitewidget.js", dependencies).viewModel
        .createViewModel({ widget: { type: "core.articles" } });
    await settle();
    assert.equal(widget.items()[0].title, "Protected");
    const pending = widget.refresh();
    token(null);
    assert.equal(widget.items().length, 0);
    resolveOld({ ok: true, data: { news: { items: [{ id: 2, title: "Old response" }] } } });
    await pending;
    assert.equal(widget.items().length, 0);
    resolveNew({ ok: true, data: { news: { items: [{ id: 3, title: "Public" }] } } });
    await settle();
    assert.equal(widget.items()[0].title, "Public");
    widget.dispose();
});

test("forum statistics include board, topic, and all published message totals", async () => {
    let model;
    const file = path.resolve(__dirname,
        "../../src/Forum/Behind.Forum.WebApi/Frontend/modules/forum/models/widgets.js");
    const totals = { "boards?page=1": 2, "posts?page=1": 3, "widgets/posts?limit=1": 11 };
    vm.runInNewContext(fs.readFileSync(file, "utf8"), {
        define(names, factory) {
            model = factory({ get: async route => ({ total: totals[route] }) });
        },
        Promise
    });
    const result = await model.load("forum.stats", 5);
    assert.deepEqual(Array.from(result.stats, stat => stat.value), [2, 3, 11]);
    assert.equal(result.stats[2].label, "widgets.forumMessages");
});

test("news widgets use the chronological widget endpoint instead of the featured public list", async () => {
    let requested;
    const dependencies = {
        require() {}, ko,
        "models/widgets": { catalog: () => [] },
        "models/articles": {}, "models/menus": {},
        "models/http": {
            get: async route => {
                requested = route;
                return { ok: true, data: [{ id: "latest", title: "Latest" }] };
            }
        },
        "models/session": { isSignedIn: ko.observable(false), accessToken: ko.observable(null) },
        "models/i18n": { t: key => key },
        "models/invalidation": { subscribe: () => ({ dispose() {} }) },
        "text!components/sitewidget.html": "",
        cleared: []
    };
    const widget = load("components/sitewidget.js", dependencies).viewModel
        .createViewModel({ widget: { type: "core.news" } });
    await settle();
    assert.equal(requested, "/widgets/news");
    assert.equal(widget.items()[0].href, "#/news/latest");
    widget.dispose();
});
