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
            result = typeof names === "function" ? names() : factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        window: { setTimeout: callback => { callback(); return 1; }, clearTimeout: () => {}, confirm: () => true },
        Promise
    });
    return result;
}

const settle = () => new Promise(resolve => setImmediate(resolve));

function editor(options = {}) {
    const saved = [];
    const grants = options.grants || ["Schedule.Read", "Schedule.Write"];
    const schedules = {
        cron: async () => ({ ok: true, data: { valid: true, next: [] } }),
        create: async fields => {
            saved.push({ id: null, fields });
            return { ok: true, message: "saved", data: { id: "s1", version: 1 } };
        },
        save: async (id, fields) => {
            saved.push({ id, fields });
            return { ok: true, message: "updated", data: { id, version: fields.version + 1 } };
        },
        actions: async () => ({ ok: true, data: [] }),
        participants: async () => ({ ok: true, data: { roles: [], users: [] } })
    };
    const list = {
        busy: ko.observable(false), message: ko.observable(""),
        perform: action => action(), activate: async () => {}
    };
    const Schedules = load("viewmodels/schedules.js", {
        ko,
        "models/session": { isSignedIn: ko.observable(true), grants: ko.observable(grants), hasGrant: grant => grants.includes(grant) },
        "models/schedules": schedules,
        "models/managedlist": () => list,
        "models/i18n": { t: key => key, tOr: (key, fallback) => fallback },
        "models/cronfields": load("models/cronfields.js", {})
    });
    return { view: new Schedules(), saved };
}

test("a successful save keeps the schedule open and the next save uses the returned version", async () => {
    const { view, saved } = editor();
    view.name("Nightly");
    view.action("core.session-cleanup");
    view.cron("0 2 * * *");
    await settle();

    await view.save();
    assert.equal(view.isEditing(), true);
    assert.equal(view.editingId(), "s1");
    assert.equal(view.name(), "Nightly");
    assert.equal(view.saveMessage(), "saved");
    assert.equal(view.saveSucceeded(), true);

    await view.save();
    await view.save();
    assert.deepEqual(saved.map(entry => [entry.id, entry.fields.version]), [[null, undefined], ["s1", 1], ["s1", 2]]);
    assert.equal(view.saveMessage(), "updated");
});

test("schedules cannot be saved without the write grant", async () => {
    const { view, saved } = editor({ grants: ["Schedule.Read"] });
    view.name("Nightly");
    view.action("core.session-cleanup");
    await settle();

    assert.equal(view.canSave(), false);
    await view.save();
    assert.equal(saved.length, 0);
});

test("an incomplete calculator draft stays editable while typed custom expressions stay custom", () => {
    const { view } = editor();
    const minute = view.fields[0];

    minute.mode("interval");
    assert.equal(view.customCron(), false);
    assert.equal(view.cron(), "*/ * * * *");

    minute.value("15");
    assert.equal(view.customCron(), false);
    assert.equal(view.cron(), "*/15 * * * *");

    view.cron("0 0 L * *");
    assert.equal(view.customCron(), true);
    view.cron("0 2 * * 1-5");
    assert.equal(view.customCron(), false);
    assert.equal(view.fields[4].mode(), "range");
});

test("placeholders are inserted at the template input selection, not into the button", () => {
    const { view } = editor();
    const input = { selectionStart: 2, selectionEnd: 3, focus() {}, setSelectionRange() {} };
    const button = {
        value: "",
        closest: selector => selector === ".behind-schedule-parameter"
            ? { querySelector: query => query === ".behind-schedule-template" ? input : null }
            : null
    };
    const field = { value: ko.observable("abXcd") };

    view.insertPlaceholder(field, "name", { currentTarget: button });

    assert.equal(field.value(), "ab{{name}}cd");
});
