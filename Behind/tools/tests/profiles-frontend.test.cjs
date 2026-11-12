const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function load(file, dependencies = {}, browserWindow = { confirm: () => true }) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) {
            if (typeof names === "function") {
                result = names();
                return;
            }
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise,
        window: browserWindow
    }, { filename: file });
    return result;
}

test("user labels never use email and always include the stable full-account tag", () => {
    const user = load("models/user.js");
    const id = "019a7821-1234-7000-8000-012345678901";
    const blank = user.label({ id, displayName: " ", emails: ["private@example.test"] });
    assert.match(blank, /^user#[0-9a-z]{8}$/);
    assert.equal(blank, "user#uvg8s6o9");
    assert.equal(user.label({ id: "00000000-0000-0000-0000-000000000000" }), "user#aajyzuzp");
    assert.equal(user.label({ id: "ffffffff-ffff-ffff-ffff-ffffffffffff" }), "user#93trmt2d");
    assert.equal(user.label({ id, displayName: " Alex " }), "Alex" + blank.slice(4));
    assert.equal(user.label({ id, displayName: "Alex" + blank.slice(4) }), "Alex" + blank.slice(4) + blank.slice(4));
    assert.notEqual(user.label({ id: "019a7821-1234-7000-8000-012345678902" }), blank);
    assert.equal(user.label({ id: id.toUpperCase() }), blank);
    assert.equal(user.href(id), "#/user/" + id);
    assert.equal(user.href(null), null);
});

test("public profile refresh shows the server response and clears stale private details on errors", async () => {
    let fail = false;
    const profile = { id: "person", displayName: "Alex#12345678", email: null,
        pictureId: null, ban: null, permissions: { canModerate: false, canImpersonate: false } };
    const Model = load("viewmodels/publicprofile.js", {
        ko, "models/i18n": { t: key => key }, "models/auth": {},
        "models/profiles": { get: async id => {
            assert.equal(id, "person");
            return fail ? { ok: false, message: "Not found" } : { ok: true, data: profile, message: "" };
        } }
    });
    const model = new Model({ params: { id: "person" } });
    await model.activate();
    assert.equal(model.profile(), profile);
    fail = true;
    await model.refresh();
    assert.equal(model.profile(), null);
    assert.equal(model.message(), "Not found");
    assert.equal(model.busy(), false);
});

test("public profile bans and unbans the looked-up user with visible failure feedback", async () => {
    const calls = [];
    const Model = load("viewmodels/publicprofile.js", {
        ko, "models/i18n": { t: key => key },
        "models/profiles": { get: async () => ({ ok: true, data: {}, message: "" }) },
        "models/auth": {
            banUser: async (id, reason) => { calls.push([id, reason]); return { ok: true, message: "Banned" }; },
            unbanUser: async id => { calls.push([id]); return { ok: false, message: "Forbidden" }; }
        }
    });
    const model = new Model({ params: { id: "person" } });
    model.startBan();
    model.banMessage(" Reason ");
    await model.ban();
    assert.deepEqual(calls[0], ["person", "Reason"]);
    assert.equal(model.banning(), false);
    assert.equal(model.banMessage(), "");
    await model.unban();
    assert.equal(model.message(), "Forbidden");
    assert.equal(model.busy(), false);
});

test("impersonation requires confirmation and only navigates after success", async () => {
    let allowed = false;
    let calls = 0;
    let destination;
    const Model = load("viewmodels/publicprofile.js", {
        ko, "models/i18n": { t: key => key },
        "models/profiles": { get: async () => ({ ok: true, data: { displayName: "Alex" }, message: "" }) },
        "models/auth": { impersonateUser: async () => { calls++; return { ok: true, message: "" }; } }
    }, { confirm: () => allowed });
    const model = new Model({ params: { id: "person" }, navigate: route => { destination = route; } });
    await model.activate();
    await model.impersonate();
    assert.equal(calls, 0);
    allowed = true;
    await model.impersonate();
    assert.equal(calls, 1);
    assert.equal(destination, "#/");
});

test("moderation never overwrites a failed profile refresh with success feedback", async () => {
    const Model = load("viewmodels/publicprofile.js", {
        ko, "models/i18n": { t: key => key },
        "models/profiles": { get: async () => ({ ok: false, message: "Refresh failed" }) },
        "models/auth": { banUser: async () => ({ ok: true, message: "Banned" }) }
    });
    const model = new Model({ params: { id: "person" } });
    await model.ban();
    assert.equal(model.message(), "Refresh failed");
    assert.equal(model.busy(), false);
});

test("profile API sends privacy preferences without inventing success on validation failure", async () => {
    const calls = [];
    const profiles = load("models/profiles.js", {
        "models/i18n": { t: key => key },
        "models/http": {
            put: async (url, body) => { calls.push({ url, body }); return { ok: false, status: 400 }; }
        }
    });

    const result = await profiles.save("", true);
    assert.equal(calls[0].url, "/profile/details");
    assert.equal(calls[0].body.birthday, null);
    assert.equal(calls[0].body.hideEmail, true);
    assert.equal(result.ok, false);
    assert.equal(result.message, "profile.invalidDetails");
});

test("own profile loads and saves optional private details without putting email in the label", async () => {
    let saved;
    const user = load("models/user.js");
    const Model = load("viewmodels/profile.js", {
        ko, "models/user": user, "models/i18n": { t: key => key },
        "models/auth": { loadSession: async () => ({ ok: true, message: "", data: {
            user: { id: "019a7821-1234-7000-8000-012345678901", displayName: "", emails: ["private@example.test"] }
        } }) },
        "models/session": { isSignedIn: () => true },
        "models/about": { loadLocales: async () => ({ items: [] }) },
        "models/usersettings": { locale: () => "" },
        "models/profilepicture": { get: async () => ({ url: null }) },
        "models/profiles": {
            details: async () => ({ ok: true, data: { birthday: "2000-02-29", hideEmail: true } }),
            save: async (birthday, hideEmail) => { saved = { birthday, hideEmail }; return { ok: true }; }
        }
    });
    const model = new Model();
    assert.equal(model.detailsLoaded(), false);
    await model.activate();
    assert.equal(model.shownAs(), "user#uvg8s6o9");
    assert.equal(model.birthday(), "2000-02-29");
    assert.equal(model.hideEmail(), true);
    model.birthday("");
    model.hideEmail(false);
    await model.submitDetails();
    assert.deepEqual(saved, { birthday: "", hideEmail: false });
    assert.equal(model.message(), "profile.detailsSaved");
});

test("failed own-profile loading cannot overwrite saved privacy preferences", async () => {
    let saves = 0;
    const Model = load("viewmodels/profile.js", {
        ko, "models/user": { label: () => "", href: () => null }, "models/i18n": { t: key => key },
        "models/auth": { loadSession: async () => ({ ok: true, message: "", data: { user: {} } }) },
        "models/session": { isSignedIn: () => true },
        "models/about": { loadLocales: async () => ({ items: [] }) },
        "models/usersettings": { locale: () => "" },
        "models/profilepicture": { get: async () => ({ url: null }) },
        "models/profiles": {
            details: async () => ({ ok: false, message: "Failed to load" }),
            save: async () => { saves++; return { ok: true }; }
        }
    });
    const model = new Model();
    await model.activate();
    assert.equal(model.message(), "Failed to load");
    await model.submitDetails();
    assert.equal(saves, 0);
    assert.equal(model.busy(), false);
});
