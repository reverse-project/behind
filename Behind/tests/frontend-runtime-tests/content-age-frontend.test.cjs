const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function load(file, dependencies) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise
    });
    return result;
}

function component(http, allowed = true) {
    return load("components/contentage.js", {
        ko, "models/http": http,
        "models/session": { hasGrant: grant => allowed && grant === "RawDatabase.Manage" },
        "models/i18n": { t: key => key },
        "text!components/contentage.html": ""
    }).viewModel;
}

test("age controls lazily load persisted minimum ages and show globally disabled default", async () => {
    const calls = [];
    const Model = component({
        get: async url => {
            calls.push(url);
            return { ok: true, data: { minimumAge: 18, enabled: false } };
        }
    });
    const model = new Model({ id: ko.observable("item-id"), entityType: "News" });
    assert.equal(calls.length, 0);
    assert.equal(model.canManage(), true);
    await model.toggle();
    assert.equal(calls[0], "/content-restrictions/News/item-id");
    assert.equal(model.minimumAge(), 18);
    assert.equal(model.enabled(), false);
    assert.equal(model.busy(), false);
    await model.toggle();
    assert.equal(calls.length, 1);
    model.dispose();
});

test("zero clears an item restriction and invalid ages never reach the server", async () => {
    const calls = [];
    const Model = component({
        post: async (url, body) => {
            calls.push({ url, body });
            return { ok: true };
        }
    });
    const model = new Model({ id: "image-id", entityType: "VfsFile" });
    for (const invalid of [-1, 151, 3.5, "invalid"]) {
        model.minimumAge(invalid);
        await model.save();
    }
    assert.equal(calls.length, 0);
    model.minimumAge("0");
    await model.save();
    assert.equal(calls[0].url, "/content-restrictions");
    assert.equal(calls[0].body.entityId, "image-id");
    assert.equal(calls[0].body.entityType, "VfsFile");
    assert.equal(calls[0].body.minimumAge, 0);
    assert.equal(model.message(), "contentRestrictions.saved");
    model.dispose();
});

test("age management controls require permission and a persisted item identity", () => {
    const Unauthorized = component({}, false);
    const denied = new Unauthorized({ id: "id", entityType: "News" });
    assert.equal(denied.canManage(), false);
    denied.dispose();
    const Model = component({});
    const missing = new Model({ id: null, entityType: "News" });
    assert.equal(missing.canManage(), false);
    missing.dispose();
});

test("failed age updates remain visible instead of reporting success", async () => {
    const Model = component({ post: async () => ({ ok: false }) });
    const model = new Model({ id: "id", entityType: "News" });
    model.minimumAge(18);
    await model.save();
    assert.equal(model.message(), "contentRestrictions.failed");
    assert.equal(model.busy(), false);
    model.dispose();
});

test("global management saves versioned rules and supports delete restore and permanent delete", async () => {
    const calls = [];
    const Model = load("viewmodels/contentrestrictions.js", {
        ko, "models/session": { hasGrant: () => true }, "models/i18n": { t: key => key },
        "models/http": {
            get: async url => {
                calls.push(url);
                return { ok: true, data: { enabled: false, types: ["News"], items: [] } };
            },
            post: async (url, body) => {
                calls.push(url);
                if (body) {
                    assert.equal(body.entityType, "News");
                    assert.equal(body.minimumAge, 18);
                }
                return { ok: true };
            }
        }
    });
    const model = new Model();
    await model.activate();
    assert.equal(model.enabled(), false);
    model.select({ entityType: "News", entityId: "news-id", minimumAge: 18 });
    await model.save();
    await model.remove({ id: "restriction-id" });
    await model.restore({ id: "restriction-id" });
    await model.hardDelete({ id: "restriction-id" });
    assert.ok(calls.includes("/content-restrictions"));
    assert.ok(calls.includes("/content-restrictions/restriction-id/delete"));
    assert.ok(calls.includes("/content-restrictions/restriction-id/restore"));
    assert.ok(calls.includes("/content-restrictions/restriction-id/hard/delete"));
    model.showDeleted(true);
    await Promise.resolve();
    assert.ok(calls.includes("/content-restrictions?deleted=true"));
    model.dispose();
});

test("global management explains the opt-in policy and all locales include its messages", () => {
    const template = fs.readFileSync(path.join(core, "views/contentrestrictions.html"), "utf8");
    assert.match(template, /contentRestrictions.disabled/);
    assert.match(template, /#\/settings/);
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(core, "locales", locale + ".json"), "utf8")).messages;
        for (const key of ["title", "policy", "disabled", "settings", "type", "item", "minimumAge", "save", "saved", "failed"]) {
            assert.ok(messages["contentRestrictions." + key]);
        }
    }
});

test("item browser searches pages and selects labels without submitting display names as IDs", async () => {
    const calls = [];
    const Model = load("viewmodels/contentrestrictions.js", {
        ko, "models/session": { hasGrant: () => true }, "models/i18n": { t: key => key },
        "models/http": { get: async url => {
            calls.push(url);
            return { ok: true, data: { items: [{ id: "uuid", displayName: "Photo.png" }], pageCount: 2 } };
        } }
    });
    const model = new Model();
    model.entityType("VfsFile");
    model.browserSearch("Photo");
    await model.findItems();
    assert.match(calls[0], /browse\/VfsFile\?page=1&search=Photo/);
    assert.equal(model.browserItems()[0].displayName, "Photo.png");
    await model.nextItems();
    assert.match(calls[1], /page=2/);
    model.chooseItem(model.browserItems()[0]);
    assert.equal(model.entityId(), "uuid");
    assert.equal(model.selectedName(), "Photo.png");
    assert.equal(model.browserOpen(), false);
    model.entityType("News");
    assert.equal(model.entityId(), "");
    assert.equal(model.selectedName(), "");
    model.dispose();
});

test("item browser rejects stale responses after a content-type change", async () => {
    let finish;
    const Model = load("viewmodels/contentrestrictions.js", {
        ko, "models/session": { hasGrant: () => true }, "models/i18n": { t: key => key },
        "models/http": { get: () => new Promise(resolve => { finish = resolve; }) }
    });

    test("item browser failures are visible and never retain old candidates", async () => {
        const Model = load("viewmodels/contentrestrictions.js", {
            ko, "models/session": { hasGrant: () => true }, "models/i18n": { t: key => key },
            "models/http": { get: async () => ({ ok: false }) }
        });
        const model = new Model();
        model.entityType("News");
        model.browserItems([{ id: "old", displayName: "Stale" }]);
        await model.browse();
        assert.equal(model.browserItems().length, 0);
        assert.equal(model.browserMessage(), "contentRestrictions.failed");
        assert.equal(model.browserBusy(), false);
        model.dispose();
    });
    const model = new Model();
    model.entityType("News");
    const pending = model.browse();
    model.browserOpen(false);
    model.entityType("VfsFile");
    finish({ ok: true, data: { items: [{ id: "wrong", displayName: "Old" }], pageCount: 1 } });
    await pending;
    assert.equal(model.browserItems().length, 0);
    model.dispose();
});
