const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key, tOr: (key, fallback) => fallback };

function load(name, dependencies = {}) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, name), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise,
        AggregateError
    }, { filename: name });
    return result;
}

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((yes, no) => {
        resolve = yes;
        reject = no;
    });
    return { promise, resolve, reject };
}

async function settle() {
    await new Promise(resolve => setImmediate(resolve));
}

function fixture() {
    const invalidation = load("models/invalidation.js");
    const results = load("models/results.js", { "models/i18n": i18n });
    const session = {
        isSignedIn: ko.observable(false),
        grants: ko.observable(null),
        displayName: ko.observable(""),
        isImpersonating: ko.observable(false)
    };
    const calls = [];
    let title = "Before";
    const http = {
        get: async url => {
            calls.push(url);
            const name = decodeURIComponent(url.split("name=")[1]);
            return {
                ok: true,
                data: {
                    menus: [{
                        name, title,
                        items: [{ id: name, title, kind: "Link", url: "#/", target: "CurrentPage" }]
                    }]
                }
            };
        },
        post: async () => ({ ok: true, data: { id: "saved" } })
    };
    const menus = load("models/menus.js", {
        ko, "models/http": http, "models/results": results, "models/paging": {},
        "models/session": session, "models/i18n": i18n, "models/invalidation": invalidation,
        "models/contentlocale": {
            current: ko.observable(null),
            assignmentRevision: ko.observable(0)
        }
    });
    return { menus, http, session, invalidation, calls, setTitle: value => { title = value; } };
}

test("invalidation is topic-scoped, awaits async handlers, and disposes idempotently", async () => {
    const invalidation = load("models/invalidation.js");
    const gate = deferred();
    const calls = [];
    const subscription = invalidation.subscribe("menus", async () => {
        calls.push("start");
        await gate.promise;
        calls.push("end");
    });
    invalidation.subscribe("layouts", () => { calls.push("unrelated"); });
    let finished = false;
    const pending = invalidation.invalidate("menus").then(() => { finished = true; });
    await settle();
    assert.deepEqual(calls, ["start"]);
    assert.equal(finished, false);
    gate.resolve();
    await pending;
    assert.deepEqual(calls, ["start", "end"]);
    subscription.dispose();
    subscription.dispose();
    const replacement = invalidation.subscribe("menus", () => { calls.push("replacement"); });
    subscription.dispose();
    await invalidation.invalidate("menus");
    assert.deepEqual(calls, ["start", "end", "replacement"]);
    replacement.dispose();
    await invalidation.invalidate("missing");
});

test("invalidation reports synchronous and asynchronous errors only after all handlers finish", async () => {
    const invalidation = load("models/invalidation.js");
    const syncError = new Error("sync");
    const asyncError = new Error("async");
    const gate = deferred();
    let completed = false;
    invalidation.subscribe("menus", () => { throw syncError; });
    invalidation.subscribe("menus", async () => { throw asyncError; });
    invalidation.subscribe("menus", async () => {
        await gate.promise;
        completed = true;
    });
    const failure = assert.rejects(invalidation.invalidate("menus"), error => {
        assert.ok(error instanceof AggregateError);
        assert.deepEqual(error.errors, [syncError, asyncError]);
        assert.match(error.message, /menus/);
        assert.equal(completed, true);
        return true;
    });
    await settle();
    assert.equal(completed, false);
    gate.resolve();
    await failure;
});

test("invalidation skips disposed pending subscriptions and defers newly added handlers", async () => {
    const invalidation = load("models/invalidation.js");
    let calls = 0;
    const removed = invalidation.subscribe("menus", () => { calls++; });
    const pending = invalidation.invalidate("menus");
    removed.dispose();
    invalidation.subscribe("menus", () => { calls++; });
    await pending;
    assert.equal(calls, 0);
    await invalidation.invalidate("menus");
    assert.equal(calls, 1);
    assert.throws(() => invalidation.subscribe("", () => {}), /topic/);
    assert.throws(() => invalidation.subscribe("menus", null), /handler/);
    assert.throws(() => invalidation.invalidate(null), /topic/);
});

const mutations = [
    ["create", [{ name: "main", title: "Main" }]],
    ["save", ["menu", { name: "main", title: "Main" }]],
    ["remove", ["menu"]],
    ["restore", ["menu"]],
    ["hardDelete", ["menu"]],
    ["createItem", ["menu", { title: "Item" }]],
    ["saveItem", ["item", { title: "Item" }]],
    ["removeItem", ["item"]],
    ["restoreItem", ["item"]],
    ["hardDeleteItem", ["item"]]
];

for (const [operation, args] of mutations) {
    test(`${operation} refreshes mounted main and footer menus only on successful mutations`, async () => {
        const { menus, http, calls, setTitle } = fixture();
        const main = menus.follow("main");
        const footer = menus.follow("footer");
        await settle();
        assert.equal(main().items[0].title(), "Before");
        assert.equal(footer().items[0].title(), "Before");
        setTitle("After");
        const result = await menus[operation](...args);
        assert.equal(result.ok, true);
        assert.equal(result.data.id, "saved");
        assert.equal(main().items[0].title(), "After");
        assert.equal(footer().items[0].title(), "After");
        assert.equal(calls.length, 4);
        for (const status of [400, 401, 403, 404, 409]) {
            http.post = async () => ({ ok: false, status });
            assert.equal((await menus[operation](...args)).ok, false);
        }
        assert.equal(calls.length, 4);
        main.dispose();
        footer.dispose();
    });
}

test("failed menu refreshes are explicit without losing the previous visible menu", async () => {
    const { menus, http, invalidation } = fixture();
    const menu = menus.follow("main");
    await settle();
    http.get = async () => ({ ok: false, status: 400 });
    await assert.rejects(menus.remove("item"), error => {
        assert.equal(error.errors[0].message, "menus.error.chooseOne");
        return true;
    });
    assert.equal(menu().title, "Before");
    assert.equal(menu.error(), "menus.error.chooseOne");
    http.get = async () => { throw new Error("offline"); };
    await assert.rejects(invalidation.invalidate("menus"), /Failed to invalidate menus/);
    assert.equal(menu.error(), "offline");
    http.get = async () => ({ ok: true, data: { menus: [] } });
    await invalidation.invalidate("menus");
    assert.equal(menu().items.length, 0);
    assert.equal(menu.error(), "");
    menu.dispose();
});

test("follow ignores stale responses and in-flight responses after disposal", async () => {
    const { menus, http, invalidation, session } = fixture();
    const requests = [];
    http.get = () => {
        const request = deferred();
        requests.push(request);
        return request.promise;
    };
    const menu = menus.follow("main");
    const refresh = invalidation.invalidate("menus");
    await settle();
    requests[1].resolve({ ok: true, data: { menus: [{ name: "main", title: "Latest", items: [] }] } });
    await refresh;
    requests[0].resolve({ ok: true, data: { menus: [{ name: "main", title: "Stale", items: [] }] } });
    await settle();
    assert.equal(menu().title, "Latest");
    session.grants(["Menu.Read"]);
    assert.equal(requests.length, 3);
    menu.dispose();
    menu.dispose();
    requests[2].resolve({ ok: true, data: { menus: [] } });
    await settle();
    assert.equal(menu().title, "Latest");
    session.isSignedIn(true);
    session.grants([]);
    await invalidation.invalidate("menus");
    assert.equal(requests.length, 3);
    assert.equal(session.isSignedIn.getSubscriptionsCount(), 0);
    assert.equal(session.grants.getSubscriptionsCount(), 0);
});

test("header and menu widgets refresh while mounted and release every subscription on disposal", async () => {
    const { menus, session, invalidation, calls, setTitle } = fixture();
    const Header = load("viewmodels/header.js", {
        ko, "models/auth": { loadSession() {} }, "models/session": session, "models/menus": menus,
        "models/accounts": {}, "models/branding": {}, "models/i18n": i18n
    });
    const component = load("components/menunav.js", {
        ko, "models/menus": menus, "text!components/menunav.html": ""
    });
    for (let index = 0; index < 3; index++) {
        const header = new Header({});
        const footer = component.viewModel.createViewModel({ name: "footer" });
        await settle();
        setTitle(`Updated ${index}`);
        await invalidation.invalidate("menus");
        assert.equal(header.links()[0].title(), `Updated ${index}`);
        assert.equal(footer.items()[0].title(), `Updated ${index}`);
        header.dispose();
        footer.dispose();
        assert.equal(session.isSignedIn.getSubscriptionsCount(), 0);
        assert.equal(session.grants.getSubscriptionsCount(), 0);
        assert.equal(session.displayName.getSubscriptionsCount(), 0);
        const previous = calls.length;
        session.isSignedIn(!session.isSignedIn());
        session.grants([]);
        await invalidation.invalidate("menus");
        assert.equal(calls.length, previous);
    }
});
