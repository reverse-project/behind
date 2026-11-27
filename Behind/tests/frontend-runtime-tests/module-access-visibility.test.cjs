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
            result = factory(...names.map(name => dependencies[name]));
        },
        Promise,
        window: { confirm: () => true }
    });
    return result;
}

const plain = value => JSON.parse(JSON.stringify(value));

test("disabled module access is hidden but role and user edits retain grants and restrictions", async () => {
    let active = false;
    const modules = load("models/modules.js", {
        ko, "models/http": {
            get: async () => ({ ok: true, data: {
                modules: [], availableGrants: active ? ["User.Manage", "Notes.Read", "Notes.Write"] : ["User.Manage"]
            } })
        }
    });
    await modules.load();
    const access = load("models/access.js", { ko, "models/modules": modules });
    const role = { id: "role", name: "Role", grants: ["User.Manage", "Notes.Read"], restrictions: ["Notes.Write"], canEdit: true };
    const saved = [];
    const auth = {
        saveRoleAccess: async (id, body) => { saved.push({ id, body: plain(body) }); return { ok: true }; },
        loadUserAccess: async () => ({ ok: true, data: {
            canEdit: true, roles: [role], availableRoles: [role],
            availableGrants: ["User.Manage", "Notes.Read", "Notes.Write"],
            grants: ["Notes.Write"], restrictions: ["Notes.Read"]
        } })
    };
    const session = { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true };
    const managedList = options => ({
        busy: ko.observable(false), message: ko.observable(""), hasMessage: ko.observable(false),
        items: ko.observableArray([options.present(role, { availableGrants: ["User.Manage", "Notes.Read", "Notes.Write"] })]),
        hasItems: ko.observable(true), refresh: () => Promise.resolve(), activate: () => Promise.resolve(),
        perform: action => action()
    });
    const Roles = load("viewmodels/roles.js", {
        ko, "models/auth": auth, "models/session": session, "models/access": access,
        "models/managedlist": managedList, "models/i18n": { t: key => key }, "models/modules": modules
    });
    const roles = new Roles();
    const row = roles.roles()[0];
    assert.deepEqual(plain(row.grants), ["User.Manage"]);
    assert.deepEqual(plain(row.restrictions), []);
    row.edit();
    assert.deepEqual(plain(row.editor.rows().map(item => item.name)), ["User.Manage"]);
    row.editor.rows()[0].state("none");
    await row.save();
    assert.deepEqual(saved[0].body, { grants: ["Notes.Read"], restrictions: ["Notes.Write"] });

    const UserAccess = load("viewmodels/useraccess.js", {
        ko, "models/auth": auth, "models/access": access, "models/modules": modules
    });
    let userSaved;
    const user = new UserAccess("user", body => { userSaved = plain(body); return Promise.resolve(); });
    await user.load();
    assert.deepEqual(plain(user.calculatedGrants()), ["User.Manage"]);
    assert.deepEqual(plain(user.editor.rows().map(item => item.name)), ["User.Manage"]);
    user.editor.rows()[0].state("grant");
    await user.save();
    assert.deepEqual(userSaved, { roleIds: ["role"], grants: ["User.Manage", "Notes.Write"], restrictions: ["Notes.Read"] });

    role.grants = saved[0].body.grants;
    role.restrictions = saved[0].body.restrictions;
    active = true;
    await modules.load();
    const restoredRole = new Roles().roles()[0];
    assert.deepEqual(plain(restoredRole.grants), ["Notes.Read"]);
    assert.deepEqual(plain(restoredRole.restrictions), ["Notes.Write"]);
    const reactivated = new UserAccess("user", () => Promise.resolve());
    await reactivated.load();
    assert.deepEqual(plain(reactivated.editor.rows().map(item => [item.name, item.state()])), [
        ["User.Manage", "none"], ["Notes.Read", "restrict"], ["Notes.Write", "grant"]
    ]);
});

test("session and public profile omit disabled grants and restrictions", async () => {
    const modules = { visibleGrants: names => names.filter(name => !name.startsWith("Notes.")) };
    const sessionData = {
        user: { id: "user", roles: [{ name: "Role", grants: ["User.Manage", "Notes.Read"] }], emails: [] },
        session: {}, sessionId: "session"
    };
    const auth = { loadSession: async () => ({ ok: true, data: sessionData }) };
    const i18n = { t: key => key, dateTime: () => "", banReason: value => value };
    const Session = load("viewmodels/session.js", {
        ko, "models/auth": auth, "models/session": { isSignedIn: ko.observable(true) },
        "models/user": { label: () => "User", href: () => "" }, "models/i18n": i18n, "models/modules": modules
    });
    const session = new Session();
    await session.refresh();
    assert.equal(session.details().roles[0].grants, "User.Manage");

    const moderation = { effectiveGrants: ["Notes.Read", "User.Manage"], directGrants: ["Notes.Write"], directRestrictions: ["Notes.Read"] };
    const Profile = load("viewmodels/publicprofile.js", {
        ko, "models/profiles": { get: async () => ({ ok: true, data: { moderation } }) },
        "models/auth": auth, "models/i18n": i18n, "models/modules": modules
    });
    const profile = new Profile({ params: { id: "user" } });
    await profile.refresh();
    assert.deepEqual(plain(profile.profile().moderation), {
        effectiveGrants: ["User.Manage"], directGrants: [], directRestrictions: []
    });
});
