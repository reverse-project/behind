const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

test("password sign-in switch follows server state and saves both boolean values", async () => {
    let enabled = false;
    const saved = [];
    const settings = {
        load: async () => ({ ok: true, data: { readOnly: false, files: [{
            name: "backend.json", fields: [{
                path: "Backend:Accounts:Password", type: "boolean", value: String(enabled),
                sensitive: false, editable: true, action: null
            }]
        }] } }),
        save: async (file, values) => {
            saved.push([file, values["Backend:Accounts:Password"]]);
            enabled = values["Backend:Accounts:Password"];
            return { ok: true, message: "saved" };
        }
    };
    const session = {
        isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true
    };
    function Editor() {
        this.canSave = ko.observable(true);
    }
    const dependencies = {
        ko, "models/settings": settings, "models/session": session, "models/i18n": { t: key => key },
        "viewmodels/emailsettings": Editor, "viewmodels/loggingsettings": Editor,
        "viewmodels/opentelemetrysettings": Editor
    };
    let Settings;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/settings.js"), "utf8"), {
        define(names, factory) { Settings = factory(...names.map(name => dependencies[name])); },
        Promise
    });

    const view = new Settings();
    await view.refresh();
    assert.equal(view.canEdit(), true);
    assert.equal(view.selected().fields[0].value(), false);
    for (const value of [true, false]) {
        const field = view.selected().fields[0];
        field.value(value);
        assert.equal(view.selected().isChanged(), true);
        await view.save();
        assert.equal(view.selected().fields[0].value(), value);
        assert.equal(view.selected().isChanged(), false);
    }
    assert.deepEqual(saved, [["backend.json", true], ["backend.json", false]]);
});
