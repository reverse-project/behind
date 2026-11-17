const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

test("an older run filter response neither replaces newer rows nor clears busy", async () => {
    const pending = [];
    const schedules = { runs: () => new Promise(resolve => pending.push(resolve)) };
    const pager = () => ({ page: () => 1, isOutOfRange: () => false, clampIfOutOfRange: () => {}, update: () => {} });
    const dependencies = {
        ko, "models/session": { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: grant => grant !== "Schedule.Delete" },
        "models/schedules": schedules, "models/pager": pager,
        "models/i18n": { t: key => key, tOr: (key, fallback) => fallback }, "models/paging": {}
    };
    let Runs;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/scheduleruns.js"), "utf8"), {
        define(names, factory) { Runs = factory(...names.map(name => dependencies[name])); },
        window: { location: { hash: "#/manage/schedules/runs" } },
        Promise, setImmediate
    });
    const view = new Runs();
    assert.equal(view.canDelete(), false);
    const page = name => ({ ok: true, message: "", data: { items: [{ id: name, scheduleName: name, action: "a", email: "sent" }] } });

    const settle = () => new Promise(resolve => setImmediate(resolve));

    view.status("failed");
    view.status("succeeded");
    assert.equal(pending.length, 2);

    pending[0](page("failed"));
    await settle();
    assert.equal(view.busy(), true);
    assert.equal(view.items().length, 0);

    pending[1](page("succeeded"));
    await settle();
    assert.equal(view.busy(), false);
    assert.deepEqual(view.items().map(item => item.id), ["succeeded"]);
});

test("losing read access clears busy and ignores a pending run response", async () => {
    const pending = [];
    const canRead = ko.observable(true);
    const schedules = { runs: () => new Promise(resolve => pending.push(resolve)) };
    const pager = () => ({ page: () => 1, isOutOfRange: () => false, clampIfOutOfRange: () => {}, update: () => {} });
    const dependencies = {
        ko, "models/session": { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => canRead() },
        "models/schedules": schedules, "models/pager": pager,
        "models/i18n": { t: key => key, tOr: (key, fallback) => fallback }, "models/paging": {}
    };
    let Runs;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/scheduleruns.js"), "utf8"), {
        define(names, factory) { Runs = factory(...names.map(name => dependencies[name])); },
        window: { location: { hash: "#/manage/schedules/runs" } },
        Promise, setImmediate
    });
    const view = new Runs();
    assert.equal(view.canDelete(), true);
    view.items([{ id: "existing" }]);

    const first = view.refresh();
    assert.equal(view.busy(), true);
    canRead(false);
    await view.refresh();
    assert.equal(pending.length, 1);
    assert.equal(view.busy(), false);
    assert.equal(view.items().length, 0);

    pending[0]({ ok: true, message: "", data: { items: [{ id: "stale" }] } });
    await first;
    assert.equal(view.busy(), false);
    assert.equal(view.items().length, 0);
});
