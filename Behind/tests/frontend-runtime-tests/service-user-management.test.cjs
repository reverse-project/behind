const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function editor(serviceUsers, canEdit) {
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
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/serviceusers.js"), "utf8"), {
        define(names, factory) { Editor = factory(ko, serviceUsers, { t: key => key }, access, modules); },
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
        load: () => new Promise(resolve => pending.push(resolve)),
        action: (action, id, body) => new Promise(resolve => {
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
        load: async () => ({ ok: true, data: data() }),
        loadAccess: id => new Promise(resolve => pending.push([id, resolve])),
        saveAccess: async (id, body) => {
            calls.push([id, JSON.parse(JSON.stringify(body))]);
            return { ok: false, message: "denied" };
        }
    }, () => true);
    await view.load();
    const first = view.openAccess({ id: "first" });
    const second = view.openAccess({ id: "second" });
    const response = { version: 7, canEdit: true, availableRoles: [{ id: "role" }], roles: [{ id: "role" }],
        availableGrants: ["User.Read"], grants: ["User.Read"], restrictions: ["User.Delete"] };
    pending[1][1]({ ok: true, data: response });
    await second;
    pending[0][1]({ ok: true, data: { ...response, canEdit: false } });
    await first;
    assert.equal(view.accessId(), "second");
    assert.equal(view.canSaveAccess(), true);
    await view.saveAccess();
    assert.deepEqual(calls, [["second", {
        version: 7, roleIds: ["role"], grants: ["User.Read"], restrictions: ["User.Delete"]
    }]]);
    view.invalidate();
    assert.equal(view.accessId(), null);
});

test("saving access refreshes the listed version before later service-user actions", async () => {
    let version = 12;
    const accessVersions = [];
    const calls = [];
    const view = editor({
        load: async () => ({ ok: true, data: data({ users: [{ id: "id", version, enabled: true }] }) }),
        loadAccess: async id => {
            accessVersions.push(id);
            return {
                ok: true,
                data: {
                    version,
                    canEdit: true,
                    availableRoles: [{ id: "role" }],
                    roles: [{ id: "role" }],
                    availableGrants: ["User.Read"],
                    grants: ["User.Read"],
                    restrictions: []
                }
            };
        },
        saveAccess: async (id, body) => {
            calls.push(["access", id, JSON.parse(JSON.stringify(body))]);
            version++;
            return { ok: true, message: "saved" };
        },
        action: async (action, id, body) => {
            calls.push([action, id, JSON.parse(JSON.stringify(body))]);
            return { ok: true, data: null };
        }
    }, () => true);

    await view.load();
    await view.openAccess(view.users()[0]);
    await view.saveAccess();

    assert.deepEqual(accessVersions, ["id", "id"]);
    assert.equal(view.users()[0].version, 13);
    assert.deepEqual(calls[0], ["access", "id", {
        version: 12, roleIds: ["role"], grants: ["User.Read"], restrictions: []
    }]);

    await view.rotate(view.users()[0]);
    assert.deepEqual(calls[1], ["rotate", "id", { version: 13 }]);
});

test("buttons honor permissions, send versions and reveal a key only once", async () => {
    const calls = [];
    const user = { id: "id", version: 12, enabled: true };
    const editable = ko.observable(true);
    const view = editor({
        load: async () => ({ ok: true, data: data({ users: [user], canDelete: false }) }),
        action: async (action, id, body) => {
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

test("service-user tabs require configuration read and actions require management grants", async () => {
    function page(configurationManage, configurationRead) {
        let Page;
        const grants = new Set(["User.Manage"]);
        if (configurationManage) {
            grants.add("Configuration.Manage");
        }
        if (configurationRead) {
            grants.add("Configuration.Read");
        }
        const session = {
            isSignedIn: ko.observable(true),
            grants: ko.observable([]),
            hasGrant: grant => grants.has(grant)
        };
        let serviceUsers;
        const ServiceUsersViewModel = function (canEdit) {
            serviceUsers = this;
            this.canManage = ko.observable(false);
            this.loaded = ko.observable(false);
            this.busy = ko.observable(false);
            this.message = ko.observable("");
            this.load = function () {
                this.busy(true);
                this.canManage(canEdit());
                this.loaded(true);
                this.busy(false);
                return Promise.resolve();
            };
            this.invalidate = function () {
                this.canManage(false);
                this.loaded(false);
                this.busy(false);
                this.message("");
            };
            this.dispose = this.invalidate;
        };
        const auth = {
            loadUsers: async () => ({
                ok: true,
                data: { letters: [], users: { items: [] } }
            })
        };
        const createPager = () => ({
            page: ko.observable(1),
            isOutOfRange: () => false,
            update() {},
            clampIfOutOfRange() {}
        });
        const createLetterPager = () => ({
            selected: ko.observable(""),
            update() {}
        });
        vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/users.js"), "utf8"), {
            define(names, factory) {
                Page = factory(
                    ko, auth, session, createPager, createLetterPager,
                    { label: item => item.name, href: id => "#/user/" + id },
                    { t: key => key, banReason: reason => reason },
                    function () {}, ServiceUsersViewModel
                );
            },
            Promise,
            window: {
                addEventListener() {},
                removeEventListener() {},
                confirm: () => true,
                location: { hash: "" }
            }
        });
        return { view: new Page(), serviceUsers: () => serviceUsers };
    }

    const configAdmin = page(true, true);
    await configAdmin.view.activate();
    assert.equal(configAdmin.view.serviceUsers.canManage(), true);
    assert.equal(configAdmin.view.canViewServiceUsers(), true);

    const readOnlyAdmin = page(false, true);
    await readOnlyAdmin.view.activate();
    assert.equal(readOnlyAdmin.view.serviceUsers.canManage(), false);
    assert.equal(readOnlyAdmin.view.canViewServiceUsers(), true);

    const userAdmin = page(false, false);
    await userAdmin.view.activate();
    assert.equal(userAdmin.view.canViewServiceUsers(), false);
    assert.equal(userAdmin.view.serviceUsers.canManage(), false);
    assert.match(fs.readFileSync(path.join(core, "views/users.html"), "utf8"), /users\.tab\.serviceUsers/);
    assert.doesNotMatch(fs.readFileSync(path.join(core, "views/settings.html"), "utf8"), /<section[^>]*with: serviceUsers/);
});

test("user administration discards pending loads when management permission is revoked", async () => {
    let resolveUsers;
    let Page;
    const canManage = ko.observable(true);
    const session = {
        isSignedIn: ko.observable(true),
        grants: ko.observable([]),
        hasGrant: grant => grant === "User.Manage" && canManage()
    };
    const auth = {
        loadUsers: () => new Promise(resolve => {
            resolveUsers = resolve;
        })
    };
    const createPager = () => ({
        page: ko.observable(1),
        isOutOfRange: () => false,
        update() {},
        clampIfOutOfRange() {}
    });
    const createLetterPager = () => ({
        selected: ko.observable(""),
        update() {}
    });
    const ServiceUsersViewModel = function () {
        this.loaded = ko.observable(false);
        this.busy = ko.observable(false);
        this.message = ko.observable("");
        this.load = () => Promise.resolve();
        this.invalidate = () => {};
        this.dispose = () => {};
    };
    vm.runInNewContext(fs.readFileSync(path.join(core, "viewmodels/users.js"), "utf8"), {
        define(names, factory) {
            Page = factory(
                ko, auth, session, createPager, createLetterPager,
                { label: item => item.name, href: id => "#/user/" + id },
                { t: key => key, banReason: reason => reason },
                function () {}, ServiceUsersViewModel
            );
        },
        Promise,
        window: {
            addEventListener() {},
            removeEventListener() {},
            confirm: () => true,
            location: { hash: "" }
        }
    });

    const view = new Page();
    const activating = view.activate();
    canManage(false);
    resolveUsers({
        ok: true,
        message: "",
        data: {
            letters: [],
            users: {
                items: [{
                    id: "id",
                    name: "Automation",
                    emails: [],
                    emailConfirmed: true,
                    permissions: {
                        canConfirmEmail: false,
                        canUnsetPin: false,
                        canModerate: false,
                        canDelete: false,
                        canManageDeleted: false,
                        canImpersonate: false,
                        canChangeAdministrator: false,
                        canChangeDeveloper: false
                    },
                    roles: [],
                    isCurrent: false,
                    ban: null,
                    isDeleted: false
                }]
            }
        }
    });
    await activating;

    assert.equal(view.users().length, 0);
    assert.equal(view.busy(), false);
});
