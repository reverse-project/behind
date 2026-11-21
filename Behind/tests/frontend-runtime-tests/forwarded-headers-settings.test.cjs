const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function editor(loadForwardedHeaders, canEdit = () => true) {
    let Editor;
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/forwardedheaderssettings.js"), "utf8"), {
        define(names, factory) { Editor = factory(ko, { loadForwardedHeaders }); }, URL
    });
    return new Editor(canEdit);
}

test("trust edits require a loaded writable policy, narrow addresses, and a positive hop count", async () => {
    const writable = ko.observable(true);
    const view = editor(async () => ({ ok: true, data: { enabled: false, knownForwarders: [], forwardLimit: 1 } }), writable);
    assert.equal(view.canEdit(), false);
    assert.equal(view.canSave(), false);
    await view.load();
    assert.equal(view.canEdit(), true);
    assert.equal(view.canSave(), false);
    view.enabled(true);
    assert.equal(view.canSave(), false);
    for (const invalid of ["0.0.0.0/0", "::/0", "127.1", "192.0.2.1/24", "1:2:3:4:5::/64", "10.0.0.0/8", "::ffff:0:0/96", "proxy.example"]) {
        view.addresses(invalid);
        assert.equal(view.canSave(), false, invalid);
    }
    view.addresses("  2001:db8::/64\n::ffff:192.0.2.1\n192.0.2.0/24\n");
    view.limit(0);
    assert.equal(view.canSave(), false);
    view.limit(2);
    assert.equal(view.canSave(), true);
    assert.deepEqual(Array.from(view.request().knownForwarders), ["2001:db8::/64", "::ffff:192.0.2.1", "192.0.2.0/24"]);
    view.enabled(false);
    assert.equal(view.canSave(), true);
    assert.equal(view.request().knownForwarders.length, 3);
    writable(false);
    assert.equal(view.canEdit(), false);
    assert.equal(view.canSave(), false);
});

test("the forwarded headers fieldset waits for the current settings load", async () => {
    const markup = fs.readFileSync(path.join(core, "views/settings.html"), "utf8");
    assert.match(markup, /<fieldset data-bind="with: forwarded, enable: forwarded\.canEdit">/);

    const pending = [];
    const view = editor(() => new Promise(resolve => pending.push(resolve)));
    const first = view.load();
    const second = view.load();
    assert.equal(view.canEdit(), false);
    pending[0]({ ok: true, data: { enabled: true, knownForwarders: ["192.0.2.1"], forwardLimit: 1 } });
    await first;
    assert.equal(view.canEdit(), false);
    assert.equal(view.addresses(), "");
    pending[1]({ ok: true, data: { enabled: true, knownForwarders: ["192.0.2.2"], forwardLimit: 2 } });
    await second;
    assert.equal(view.canEdit(), true);
    assert.equal(view.addresses(), "192.0.2.2");

    const retry = view.load();
    assert.equal(view.canEdit(), false);
    pending[2]({ ok: false });
    await retry;
    assert.equal(view.canEdit(), false);
    assert.equal(view.canSave(), false);
});

test("leaving a tab invalidates outstanding policy reads", async () => {
    let resolve;
    const view = editor(() => new Promise(done => { resolve = done; }));
    const pending = view.load();
    view.invalidate();
    resolve({ ok: true, data: { enabled: false, knownForwarders: ["192.0.2.2"], forwardLimit: 2 } });
    await pending;
    assert.equal(view.addresses(), "");
    assert.equal(view.canEdit(), false);
    assert.equal(view.canSave(), false);
});
