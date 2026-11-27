const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function editor(settings, canEdit) {
    let Editor;
    const modules = { visibleGrants: grants => grants };
    const access = {
        create: () => {
            let grants = [], restrictions = [];
            return {
                load(available, selected, restricted) { grants = selected; restrictions = restricted; },
                request() { return { grants, restrictions }; },
                dispose() {}
            };
        },
        calculate: (roles, grants) => grants
    };
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/serviceusersettings.js"), "utf8"), {
        define(names, factory) { Editor = factory(ko, settings, { t: key => key }, access, modules); },
        Promise, window: { confirm: () => true }
    });
    return new Editor(canEdit);
}

function data(overrides = {}) {
    return { users: [], canManage: true, canDelete: true, isDeveloper: true, ...overrides };
}

test("stale loads and invalidated create responses never reveal keys", async () => {
    const pending = [];
    let resolveAction;
    let request;
    const view = editor({
        loadServiceUsers: () => new Promise(resolve => pending.push(resolve)),
        serviceUserAction: (action, id, body) => new Promise(resolve => {
            request = JSON.parse(JSON.stringify(body));
            resolveAction = resolve;
        })
    }, () => true);
    const first = view.load();
    const second = view.load();
    pending[1]({ ok: true, data: data() });
    await second;
    pending[0]({ ok: true, data: data({ canManage: false }) });
    await first;
    assert.equal(view.canManage(), true);
    view.name("Automation");
    const created = view.create();
    assert.deepEqual(request, { name: "Automation" });
    view.invalidate();
    resolveAction({ ok: true, data: { apiKey: "secret" } });
    await created;
    assert.equal(view.key(), "");
    assert.equal(view.busy(), false);
});

test("independent access editor saves roles, grants and restrictions and ignores stale loads", async () => {
    const pending = [];
    const calls = [];
    const view = editor({
        loadServiceUsers: async () => ({ ok: true, data: data() }),
        loadServiceUserAccess: id => new Promise(resolve => pending.push([id, resolve])),
        saveServiceUserAccess: async (id, body) => {
            calls.push([id, JSON.parse(JSON.stringify(body))]);
            return { ok: false, message: "denied" };
        }
    }, () => true);
    await view.load();
    const first = view.openAccess({ id: "first" });
    const second = view.openAccess({ id: "second" });
    const response = { canEdit: true, availableRoles: [{ id: "role" }], roles: [{ id: "role" }],
        availableGrants: ["User.Read"], grants: ["User.Read"], restrictions: ["User.Delete"] };
    pending[1][1]({ ok: true, data: response });
    await second;
    pending[0][1]({ ok: true, data: { ...response, canEdit: false } });
    await first;
    assert.equal(view.accessId(), "second");
    assert.equal(view.canSaveAccess(), true);
    await view.saveAccess();
    assert.deepEqual(calls, [["second", { roleIds: ["role"], grants: ["User.Read"], restrictions: ["User.Delete"] }]]);
    view.invalidate();
    assert.equal(view.accessId(), null);
});

test("buttons honor permissions, send versions and reveal a key only once", async () => {
    const calls = [];
    const user = { id: "id", version: 12, enabled: true };
    const editable = ko.observable(true);
    const view = editor({
        loadServiceUsers: async () => ({ ok: true, data: data({ users: [user], canDelete: false }) }),
        serviceUserAction: async (action, id, body) => {
            calls.push([action, id, JSON.parse(JSON.stringify(body))]);
            return { ok: true, data: action === "rotate" ? { apiKey: "only-once" } : null };
        }
    }, editable);
    await view.load();
    await view.remove(user);
    assert.equal(calls.length, 0);
    await view.rotate(user);
    assert.deepEqual(calls[0], ["rotate", "id", { version: 12 }]);
    assert.equal(view.key(), "only-once");
    await view.showDeleted();
    assert.equal(view.key(), "");
    await view.hardDelete(user);
    assert.deepEqual(calls[1], ["hard/delete", "id", { version: 12 }]);
    editable(false);
    await view.restore(user);
    assert.equal(calls.length, 2);
});
