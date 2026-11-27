const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

test("caching tab edits provider and key without resubmitting its masked connection", async () => {
    let provider = "Memory";
    let key = "CachingConnection";
    const saved = [];
    const settings = {
        load: async () => ({ ok: true, data: { readOnly: false, files: [{
            name: "caching.json",
            fields: [
                { path: "Caching:Provider", type: "string", value: provider, sensitive: false, editable: true, action: null,
                    options: ["Memory", "RedisCompatible"].map(value => ({ value, label: value })) },
                { path: "Caching:Key", type: "string", value: key, sensitive: false, editable: true, action: null },
                { path: "ConnectionStrings:CachingConnection", type: "string", value: "****", sensitive: true, editable: true, action: null }
            ]
        }] } }),
        save: async (file, values) => {
            saved.push([file, JSON.parse(JSON.stringify(values))]);
            provider = values["Caching:Provider"] ?? provider;
            key = values["Caching:Key"] ?? key;
            return { ok: true, message: "saved" };
        }
    };
    const session = { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true };
    function Editor() {
        this.invalidate = () => {};
        this.key = ko.observable("");
        this.load = async () => ({ ok: true });
        this.canSave = ko.observable(true);
    }
    const dependencies = {
        ko, "models/settings": settings, "models/session": session, "models/i18n": { t: key => key },
        "viewmodels/emailsettings": Editor, "viewmodels/loggingsettings": Editor,
        "viewmodels/opentelemetrysettings": Editor, "viewmodels/forwardedheaderssettings": Editor,"viewmodels/serviceusersettings": Editor, "viewmodels/oidcsettings": Editor
    };
    let Settings;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/settings.js"), "utf8"), {
        define(names, factory) { Settings = factory(...names.map(name => dependencies[name])); },
        Promise
    });
    const view = new Settings();
    await view.refresh();
    assert.equal(view.selected().name, "caching.json");
    const fields = view.selected().fields;
    assert.equal(fields[0].hasOptions, true);
    assert.deepEqual(Array.from(fields[0].options, option => option.value), ["Memory", "RedisCompatible"]);
    assert.equal(fields[2].value(), "");
    fields[0].value("RedisCompatible");
    fields[1].value("");
    await view.save();
    assert.deepEqual(saved, [["caching.json", { "Caching:Provider": "RedisCompatible", "Caching:Key": "" }]]);
    assert.equal(view.selected().isChanged(), false);
    view.readOnly(true);
    assert.equal(view.canEdit(), false);
});
