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
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise, Date, encodeURIComponent,
        window: { localStorage: { getItem: () => null }, confirm: () => true },
        document: { documentElement: { setAttribute() {} } }
    }, { filename: file });
    return result;
}

function localization() {
    return load("models/i18n.js", {
        ko,
        "models/modules": { localeUrls: () => [] },
        "models/http": { get: async url => ({
            ok: true, data: JSON.parse(fs.readFileSync(path.join(core, url), "utf8"))
        }) }
    });
}

test("built-in ban reasons translate reactively while custom reasons are preserved verbatim", async () => {
    const i18n = localization();
    await i18n.apply();
    const reason = ko.pureComputed(() => i18n.banReason("Misconduct"));
    assert.equal(reason(), "Misconduct");
    i18n.useAccount("hu-HU");
    await i18n.apply();
    assert.equal(reason(), "Szabályszegés");
    for (const value of ["Custom reason", "<b>Custom</b>", "Misconduct: custom details", "misconduct", "", null]) {
        assert.equal(i18n.banReason(value), value);
    }
    i18n.useAccount("en-US");
    await i18n.apply();
    assert.equal(reason(), "Misconduct");
    reason.dispose();
});

test("wall and management rows translate built-in reasons without reloading or changing custom bans", async () => {
    const i18n = localization();
    await i18n.apply();
    const createPager = load("models/pager.js", { ko, "models/i18n": i18n });
    const data = { page: 1, pageCount: 1, total: 2, items: [
        { id: "one", displayName: "One", reason: "Misconduct", expiresAt: null },
        { id: "two", displayName: "Two", reason: "Custom reason", expiresAt: null }
    ] };
    const Wall = load("viewmodels/wallofshame.js", {
        ko, "models/i18n": i18n, "models/pager": createPager,
        "models/user": { href: id => "#/user/" + id },
        "models/auth": { loadWallOfShame: async () => ({ ok: true, message: "", data }) }
    });
    const wall = new Wall();
    await wall.activate();
    const Users = load("viewmodels/users.js", {
        ko, "models/i18n": i18n, "models/pager": createPager,
        "models/letterpager": () => ({ selected: ko.observable(""), update() {} }),
        "models/user": { href: id => "#/user/" + id, label: item => item.displayName },
        "models/session": { isSignedIn: ko.observable(true), hasGrant: () => true, grants: ko.observable([]) },
        "viewmodels/useraccess": function () {},
        "models/auth": { loadUsers: async () => ({ ok: true, message: "", data: {
            letters: [], users: { ...data, items: data.items.map(item => ({
                ...item, emails: [], roles: [], permissions: {}, ban: { reason: item.reason }
            })) }
        } }) }
    });
    const users = new Users();
    await users.refresh();
    assert.equal(wall.entries()[0].reason(), "Misconduct");
    assert.equal(users.users()[0].banReason(), "Misconduct");
    i18n.useAccount("hu-HU");
    await i18n.apply();
    assert.equal(wall.entries()[0].reason(), "Szabályszegés");
    assert.equal(users.users()[0].banReason(), "Szabályszegés");
    assert.equal(wall.entries()[1].reason(), "Custom reason");
    assert.equal(users.users()[1].banReason(), "Custom reason");
    assert.match(fs.readFileSync(path.join(core, "views/wallofshame.html"), "utf8"), /text: reason/);
    assert.match(fs.readFileSync(path.join(core, "views/users.html"), "utf8"), /isBanned && banReason\(\) !== ''/);
});

test("profile history and banned sign-in errors use the same localized reason", async () => {
    const i18n = localization();
    i18n.useAccount("hu-HU");
    await i18n.apply();
    const Profile = load("viewmodels/publicprofile.js", {
        ko, "models/i18n": i18n, "models/auth": {}, "models/profiles": {}
    });
    const profile = new Profile({ params: { id: "person" } });
    assert.equal(profile.banReason("Misconduct"), "Szabályszegés");
    assert.match(fs.readFileSync(path.join(core, "views/publicprofile.html"), "utf8"), /text: \$root\.banReason\(reason\)/);
    let reason = "Misconduct";
    const auth = load("models/auth.js", {
        "models/i18n": i18n, "models/session": {}, "models/user": {}, "models/webauthn": {},
        "models/http": { post: async () => ({ ok: false, status: 403, data: { reason } }) }
    });
    assert.equal((await auth.signIn("user@example.test", "password")).message,
        i18n.t("auth.bannedReason", ["Szabályszegés"]));
    reason = "Custom reason";
    assert.equal((await auth.signIn("user@example.test", "password")).message,
        i18n.t("auth.bannedReason", ["Custom reason"]));
});
