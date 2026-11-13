const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function component(http, { signedIn = true, grants = ["ContentRestriction.Write"] } = {}) {
    let result;
    const dependencies = {
        ko,
        "models/http": http,
        "models/session": {
            isSignedIn: () => ko.unwrap(signedIn),
            hasGrant: grant => ko.unwrap(grants).includes(grant)
        },
        "models/i18n": { t: (key, args) => key + (args ? ":" + args.join(",") : "") },
        "text!components/contentage.html": ""
    };
    vm.runInNewContext(fs.readFileSync(path.join(core, "components/contentage.js"), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise
    });
    return result.viewModel;
}

function response(url, minimumAge = 18) {
    return url === "/content-age-options"
        ? { ok: true, data: { items: [{ id: "age-12", age: 12, name: "Child" }, { id: "age-18", age: 18, name: "Adult" }] } }
        : { ok: true, data: { minimumAge, enabled: false } };
}

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((complete, fail) => {
        resolve = complete;
        reject = fail;
    });
    return { promise, resolve, reject };
}

test("age controls lazily load predefined ages and the persisted restriction", async () => {
    const calls = [];
    const Model = component({
        get: async url => {
            calls.push(url);
            return response(url);
        }
    });
    const model = new Model({ id: ko.observable("item/id"), entityType: ko.observable("News") });
    assert.equal(calls.length, 0);
    assert.equal(model.canManage(), true);
    assert.equal(model.canSave(), false);
    await model.toggle();
    assert.deepEqual(calls, ["/content-restrictions/News/item%2Fid", "/content-age-options"]);
    assert.deepEqual(Array.from(model.options(), item => ({ age: item.age, label: item.label })), [
        { age: 0, label: "contentAgeOptions.unrestricted" },
        { age: 12, label: "contentAgeOptions.optionLabel:Child,12" },
        { age: 18, label: "contentAgeOptions.optionLabel:Adult,18" }
    ]);
    assert.equal(model.minimumAge(), 18);
    assert.equal(model.enabled(), false);
    assert.equal(model.busy(), false);
    assert.equal(model.canSave(), true);
    await model.toggle();
    assert.equal(model.open(), false);
    assert.equal(calls.length, 2);
    model.dispose();
});

test("controls require a signed-in identity, restriction write grant, and owning editor permission", () => {
    for (const options of [
        { signedIn: false },
        { grants: [] },
        { grants: ["RawDatabase.Manage"] },
        { grants: ["ContentAge.Manage"] }
    ]) {
        const Model = component({}, options);
        const model = new Model({ id: "id", entityType: "News", allowed: true });
        assert.equal(model.canManage(), false);
        model.dispose();
    }
    const grants = ko.observable(["ContentRestriction.Write"]);
    const signedIn = ko.observable(true);
    const allowed = ko.observable(false);
    const id = ko.observable("id");
    const Model = component({}, { grants, signedIn });
    const model = new Model({ id, entityType: "News", allowed });
    assert.equal(model.canManage(), false);
    allowed(true);
    assert.equal(model.canManage(), true);
    grants([]);
    assert.equal(model.canManage(), false);
    grants(["ContentRestriction.Write"]);
    signedIn(false);
    assert.equal(model.canManage(), false);
    signedIn(true);
    id(null);
    assert.equal(model.canManage(), false);
    model.dispose();
    const denied = new Model({ id: "id", entityType: "News", allowed: false });
    assert.equal(denied.canManage(), false);
    denied.dispose();
});

test("only predefined ages are offered and unrestricted zero saves a numeric clear", async () => {
    const calls = [];
    const Model = component({
        get: async url => response(url),
        post: async (url, body) => {
            calls.push({ url, body });
            return { ok: true };
        }
    });
    const model = new Model({ id: "image-id", entityType: "VfsFile", allowed: true });
    await model.save();
    assert.equal(calls.length, 0);
    await model.toggle();
    for (const age of [18, "0"]) {
        model.minimumAge(age);
        await model.save();
    }
    assert.equal(calls.length, 2);
    assert.equal(calls[0].body.minimumAge, 18);
    assert.equal(calls[1].url, "/content-restrictions");
    assert.equal(calls[1].body.entityId, "image-id");
    assert.equal(calls[1].body.entityType, "VfsFile");
    assert.equal(calls[1].body.minimumAge, 0);
    assert.equal(model.message(), "contentRestrictions.saved");
    assert.equal(model.busy(), false);
    model.dispose();
});

test("an empty predefined list still offers unrestricted zero", async () => {
    const Model = component({ get: async url => url === "/content-age-options"
        ? { ok: true, data: { items: [] } }
        : response(url, 0) });
    const model = new Model({ id: "id", entityType: "News" });
    await model.toggle();
    assert.equal(model.options().length, 1);
    assert.equal(model.options()[0].age, 0);
    assert.equal(model.minimumAge(), 0);
    assert.equal(model.canSave(), true);
    model.dispose();
});

test("removed ages remain visible and saveable until explicitly changed", async () => {
    const saved = [];
    const Model = component({
        get: async url => response(url, 21),
        post: async (url, body) => { saved.push(body.minimumAge); return { ok: true }; }
    });
    const model = new Model({ id: "id", entityType: "News" });
    await model.toggle();
    assert.equal(model.minimumAge(), 21);
    assert.equal(model.options().find(item => item.age === 21).label, "contentAgeOptions.removedOption:21");
    await model.save();
    assert.equal(model.minimumAge(), 21);
    model.minimumAge(12);
    await model.save();
    assert.equal(model.minimumAge(), 12);
    assert.equal(model.options().some(item => item.age === 21), false);
    model.minimumAge(0);
    await model.save();
    assert.deepEqual(saved, [21, 12, 0]);
    model.dispose();
});

test("a failed change keeps the removed age available for the existing restriction", async () => {
    const Model = component({
        get: async url => response(url, 21),
        post: async () => ({ ok: false })
    });
    const model = new Model({ id: "id", entityType: "News" });
    await model.toggle();
    model.minimumAge(12);
    await model.save();
    assert.equal(model.message(), "contentRestrictions.failed");
    assert.equal(model.options().some(item => item.age === 21), true);
    model.minimumAge(21);
    assert.equal(model.canSave(), true);
    model.dispose();
});

for (const failingUrl of ["/content-age-options", "/content-restrictions/News/id"]) {
    for (const rejected of [false, true]) {
        test(`${failingUrl} ${rejected ? "rejection" : "failure"} leaves saving disabled`, async () => {
            let posts = 0;
            const Model = component({
                get: async url => {
                    if (url === failingUrl) {
                        if (rejected) {
                            throw new Error("network unavailable");
                        }
                        return { ok: false };
                    }
                    return response(url);
                },
                post: async () => { ++posts; }
            });
            const model = new Model({ id: "id", entityType: "News" });
            await model.toggle();
            assert.equal(model.message(), "contentRestrictions.failed");
            assert.equal(model.busy(), false);
            assert.equal(model.canSave(), false);
            assert.equal(model.loadedId(), null);
            await model.save();
            assert.equal(posts, 0);
            model.dispose();
        });
    }
}

for (const rejected of [false, true]) {
    test(`save ${rejected ? "rejection" : "failure"} remains visible and permits retry`, async () => {
        let attempts = 0;
        const Model = component({
            get: async url => response(url),
            post: async () => {
                if (++attempts === 1) {
                    if (rejected) {
                        throw new Error("network unavailable");
                    }
                    return { ok: false };
                }
                return { ok: true };
            }
        });
        const model = new Model({ id: "id", entityType: "News" });
        await model.toggle();
        await model.save();
        assert.equal(model.message(), "contentRestrictions.failed");
        assert.equal(model.busy(), false);
        assert.equal(model.canSave(), true);
        await model.save();
        assert.equal(model.message(), "contentRestrictions.saved");
        model.dispose();
    });
}

test("responses for a previous id cannot populate or enable the new item's editor", async () => {
    const pending = [];
    let posts = 0;
    const Model = component({
        get: () => {
            const request = deferred();
            pending.push(request);
            return request.promise;
        },
        post: async () => { ++posts; }
    });
    const id = ko.observable("old-id");
    const model = new Model({ id, entityType: "News" });
    const loading = model.toggle();
    assert.equal(model.busy(), true);
    id("new-id");
    pending[0].resolve(response("/content-restrictions/News/old-id"));
    pending[1].resolve(response("/content-age-options"));
    await loading;
    assert.equal(model.loadedId(), null);
    assert.equal(model.options().length, 0);
    assert.equal(model.canSave(), false);
    assert.equal(model.busy(), false);
    await model.save();
    assert.equal(posts, 0);
    model.dispose();
});

test("previous successful loads cannot be saved after id or entity type changes", async () => {
    let posts = 0;
    const Model = component({
        get: async url => response(url),
        post: async () => { ++posts; }
    });
    const id = ko.observable("id");
    const entityType = ko.observable("News");
    const model = new Model({ id, entityType });
    await model.toggle();
    id("another-id");
    assert.equal(model.canSave(), false);
    await model.save();
    id("id");
    entityType("Article");
    assert.equal(model.canSave(), false);
    await model.save();
    assert.equal(posts, 0);
    model.dispose();
});

test("closing and reopening ignores stale results while retaining the latest request", async () => {
    const pending = [];
    const Model = component({ get: () => {
        const request = deferred();
        pending.push(request);
        return request.promise;
    } });
    const id = ko.observable("old-id");
    const model = new Model({ id, entityType: "News" });
    const old = model.toggle();
    await model.toggle();
    id("new-id");
    const current = model.toggle();
    pending[2].resolve(response("/content-restrictions/News/new-id", 12));
    pending[3].resolve(response("/content-age-options"));
    await current;
    pending[0].resolve(response("/content-restrictions/News/old-id", 18));
    pending[1].resolve(response("/content-age-options"));
    await old;
    assert.equal(model.minimumAge(), 12);
    assert.equal(model.loadedId(), "new-id");
    assert.equal(model.canSave(), true);
    model.dispose();
});

test("disposal ignores pending loads and saves", async () => {
    const loading = deferred();
    const saving = deferred();
    const Model = component({
        get: url => url === "/content-age-options" ? Promise.resolve(response(url)) : loading.promise,
        post: () => saving.promise
    });
    const model = new Model({ id: "id", entityType: "News" });
    const pending = model.toggle();
    model.dispose();
    loading.resolve(response("/content-restrictions/News/id"));
    await pending;
    assert.equal(model.loadedId(), null);
    assert.equal(model.options().length, 0);
    const SaveModel = component({ get: async url => response(url), post: () => saving.promise });
    const saved = new SaveModel({ id: "id", entityType: "News" });
    await saved.toggle();
    const post = saved.save();
    saved.dispose();
    saving.resolve({ ok: true });
    await post;
    assert.equal(saved.message(), "");
});

test("component uses a predefined-age select with load-gated saving and localized policy", () => {
    const template = fs.readFileSync(path.join(core, "components/contentage.html"), "utf8");
    assert.match(template, /if: canManage/);
    assert.match(template, /<select[^>]+options: options/);
    assert.match(template, /optionsValue: 'age'/);
    assert.match(template, /enable: canSave/);
    assert.match(template, /contentRestrictions.disabled/);
    assert.match(template, /role="status"/);
    assert.doesNotMatch(template, /type="number"/);
});
