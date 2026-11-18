const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function load(settings) {
    let Editor;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/oidcsettings.js"), "utf8"), {
        define(names, factory) { Editor = factory(ko, settings); },
        Promise, URL
    });
    return Editor;
}

function data(ticketLifetime) {
    return { ticketLifetime, authenticationTimeout: "00:05:00", maxPendingTickets: 10, providers: [] };
}

test("an older settings response cannot overwrite a newer load", async () => {
    const pending = [];
    const Editor = load({ loadOidc: () => new Promise(resolve => pending.push(resolve)) });
    const editor = new Editor(() => true);

    const first = editor.load();
    const second = editor.load();
    pending[1]({ ok: true, data: data("00:02:00") });
    await second;
    pending[0]({ ok: true, data: data("00:01:00") });
    await first;

    assert.equal(editor.ticketLifetime, "00:02:00");
    assert.equal(editor.loaded(), true);
});
