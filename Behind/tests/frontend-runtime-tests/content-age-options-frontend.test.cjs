const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const settle = () => new Promise(resolve => setImmediate(resolve));

function create({ http = {}, allowed = ko.observable(true), developer = ko.observable(true), confirm = () => true } = {}) {
    let Model;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/contentageoptions.js"), "utf8"), {
        define(names, factory) {
            const dependencies = {
                ko,
                "models/http": http,
                "models/i18n": { t: (key, args) => key + (args ? ":" + args.join(",") : "") },
                "models/session": { hasGrant: grant => grant === "ContentAge.Manage" ? allowed() : grant === "RawDatabase.Manage" && developer() }
            };
            Model = factory(...names.map(name => dependencies[name]));
        },
        window: { confirm },
        Promise
    });
    return new Model();
}

test("deleted age management requires the developer grant", async () => {
    const calls = [];
    const model = create({ developer: ko.observable(false), http: {
        get: async url => {
            calls.push(url);
            return { ok: true, data: { items: [{ id: "age-id", age: 18 }] } };
        },
        post: async url => { calls.push(url); return { ok: true }; }
    } });
    await model.activate();
    assert.equal(model.canViewDeleted(), false);
    model.showDeleted(true);
    await settle();
    await model.restore({ id: "age-id", age: 18 });
    await model.hardDelete({ id: "age-id", age: 18 });
    assert.deepEqual(calls, ["/content-age-options?deleted=false"]);
    model.dispose();
});

test("loads active ages and creates numeric ages within inclusive bounds", async () => {
    const calls = [];
    const model = create({ http: {
        get: async url => {
            calls.push(url);
            return { ok: true, data: { items: [{ id: "age-id", age: 18 }] } };
        },
        post: async (url, body) => {
            calls.push([url, body.age, body.name]);
            return { ok: true };
        }
    } });
    await model.activate();
    assert.equal(calls[0], "/content-age-options?deleted=false");
    assert.equal(model.items()[0].age, 18);
    for (const age of [1, "150"]) {
        model.age(age);
        model.name(" Adult ");
        assert.equal(model.canSave(), true);
        await model.save();
        assert.equal(model.age(), "");
        assert.equal(model.busy(), false);
    }
    assert.deepEqual(calls.filter(Array.isArray), [
        ["/content-age-options", 1, "Adult"],
        ["/content-age-options", 150, "Adult"]
    ]);
    model.dispose();
});

test("invalid ages never submit and produce validation feedback", async () => {
    let posts = 0;
    const model = create({ http: { post: async () => { ++posts; } } });
    model.name("Adult");
    for (const age of ["", " ", 0, -1, 151, 2.5, "invalid", null, true, [], Infinity]) {
        model.age(age);
        assert.equal(model.canSave(), false);
        await model.save();
        assert.equal(model.message(), "contentAgeOptions.invalidAge");
    }
    assert.equal(posts, 0);
    model.dispose();
});

test("names are required and editable without changing the age", async () => {
    const calls = [];
    const model = create({ http: {
        get: async () => ({ ok: true, data: { items: [{ id: "age/id", age: 18, name: "Adult" }] } }),
        post: async (url, body) => { calls.push({ url, name: body.name, age: body.age }); return { ok: true }; }
    } });
    await model.activate();
    model.age(18);
    for (const name of ["", "   ", "a".repeat(201)]) {
        model.name(name);
        assert.equal(model.canSave(), false);
        await model.save();
        assert.equal(model.message(), "contentAgeOptions.invalidName");
    }
    model.edit(model.items()[0]);
    assert.equal(model.name(), "Adult");
    assert.equal(model.age(), 18);
    model.name("  Grown-up  ");
    await model.save();
    assert.deepEqual(calls, [{ url: "/content-age-options/age%2Fid/update", name: "Grown-up", age: undefined }]);
    assert.equal(model.editingId(), null);
    assert.equal(model.name(), "");
    model.dispose();
});

test("editing is canceled when switching to deleted ages or losing permission", async () => {
    const allowed = ko.observable(true);
    const model = create({ allowed, http: {
        get: async () => ({ ok: true, data: { items: [{ id: "age-id", age: 18, name: "Adult" }] } })
    } });
    await model.activate();
    model.edit(model.items()[0]);
    model.showDeleted(true);
    await settle();
    assert.equal(model.editingId(), null);
    assert.equal(model.name(), "");
    model.showDeleted(false);
    await settle();
    model.edit(model.items()[0]);
    allowed(false);
    assert.equal(model.editingId(), null);
    assert.equal(model.name(), "");
    model.dispose();
});

test("delete and permanent delete require confirmation; deleted listing supports restoration", async () => {
    let confirmed = false;
    const calls = [];
    const questions = [];
    const model = create({
        confirm: question => {
            questions.push(question);
            return confirmed;
        },
        http: {
            get: async url => {
                calls.push(url);
                return { ok: true, data: { items: [{ id: "age/id", age: 18 }] } };
            },
            post: async url => {
                calls.push(url);
                return { ok: true };
            }
        }
    });
    await model.activate();
    await model.remove(model.items()[0]);
    assert.equal(calls.length, 1);
    confirmed = true;
    await model.remove(model.items()[0]);
    assert.ok(calls.includes("/content-age-options/age%2Fid/delete"));
    model.showDeleted(true);
    await settle();
    assert.ok(calls.includes("/content-age-options?deleted=true"));
    model.age(21);
    await model.save();
    assert.equal(model.canSave(), false);
    await model.restore(model.items()[0]);
    assert.ok(calls.includes("/content-age-options/age%2Fid/restore"));
    confirmed = false;
    await model.hardDelete(model.items()[0]);
    assert.ok(!calls.includes("/content-age-options/age%2Fid/hard/delete"));
    confirmed = true;
    await model.hardDelete(model.items()[0]);
    assert.ok(calls.includes("/content-age-options/age%2Fid/hard/delete"));
    assert.ok(questions.includes("contentAgeOptions.confirm.delete:18"));
    assert.ok(questions.includes("contentAgeOptions.confirm.hardDelete:18"));
    model.dispose();
});

test("all requests require ContentAge.Manage and permission loss clears listed data", async () => {
    const allowed = ko.observable(false);
    let requests = 0;
    const model = create({ allowed, http: {
        get: async () => {
            ++requests;
            return { ok: true, data: { items: [{ id: "id", age: 18 }] } };
        },
        post: async () => { ++requests; }
    } });
    await model.activate();
    model.age(18);
    model.name("Adult");
    await model.save();
    await model.remove({ id: "id", age: 18 });
    await model.restore({ id: "id", age: 18 });
    await model.hardDelete({ id: "id", age: 18 });
    assert.equal(requests, 0);
    allowed(true);
    await settle();
    assert.equal(model.items().length, 1);
    allowed(false);
    assert.equal(model.items().length, 0);
    model.dispose();
});

test("failed and rejected requests show errors and release loading and saving states", async () => {
    for (const rejected of [false, true]) {
        const fail = async () => {
            if (rejected) {
                throw new Error("network unavailable");
            }
            return { ok: false };
        };
        const model = create({ http: { get: fail, post: fail } });
        await model.activate();
        assert.equal(model.message(), "contentAgeOptions.failed");
        assert.equal(model.busy(), false);
        model.age(18);
        model.name("Adult");
        await model.save();
        assert.equal(model.message(), "contentAgeOptions.failed");
        assert.equal(model.age(), 18);
        assert.equal(model.busy(), false);
        model.dispose();
    }
});

test("loading blocks duplicate operations and stale list responses cannot replace a new filter", async () => {
    const pending = [];
    let posts = 0;
    const model = create({ http: {
        get: () => new Promise(resolve => pending.push(resolve)),
        post: async () => { ++posts; }
    } });
    const first = model.activate();
    assert.equal(model.loading(), true);
    model.age(18);
    model.name("Adult");
    await model.save();
    assert.equal(posts, 0);
    model.showDeleted(true);
    pending[1]({ ok: true, data: { items: [{ id: "deleted", age: 21 }] } });
    await settle();
    pending[0]({ ok: true, data: { items: [{ id: "active", age: 18 }] } });
    await first;
    assert.equal(model.items()[0].id, "deleted");
    assert.equal(model.busy(), false);
    model.dispose();
});

test("saving blocks duplicate submissions and refresh errors do not report success", async () => {
    let finish;
    let posts = 0;
    const model = create({ http: {
        get: async () => ({ ok: false }),
        post: () => {
            ++posts;
            return new Promise(resolve => { finish = resolve; });
        }
    } });
    model.age(18);
    model.name("Adult");
    const saving = model.save();
    assert.equal(model.saving(), true);
    await model.save();
    assert.equal(posts, 1);
    finish({ ok: true });
    await saving;
    assert.equal(model.message(), "contentAgeOptions.failed");
    assert.equal(model.busy(), false);
    model.dispose();
});

test("disposal unsubscribes and ignores pending responses", async () => {
    let finish;
    let requests = 0;
    const allowed = ko.observable(true);
    const model = create({ allowed, http: {
        get: () => {
            ++requests;
            return new Promise(resolve => { finish = resolve; });
        }
    } });
    const loading = model.activate();
    model.dispose();
    model.showDeleted(true);
    allowed(false);
    allowed(true);
    finish({ ok: true, data: { items: [{ id: "late", age: 18 }] } });
    await loading;
    assert.equal(requests, 1);
    assert.equal(model.items().length, 0);
});

test("management template localizes removal consequences, controls and status", () => {
    const template = fs.readFileSync(path.join(core, "views/contentageoptions.html"), "utf8");
    for (const key of ["removalWarning", "denied", "showDeleted", "loading", "saving", "hardDelete", "restore"]) {
        assert.ok(template.includes("contentAgeOptions." + key));
    }
    assert.match(template, /type="number" min="1" max="150" step="1" required/);
    assert.match(template, /role="alert"/);
    assert.match(template, /role="status"/);
    assert.doesNotMatch(template, /data-bind="html:/);
});
