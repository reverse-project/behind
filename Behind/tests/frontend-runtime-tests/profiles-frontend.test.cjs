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
        "models/auth": { changeDisplayName: async () => ({ ok: true }), loadSession: async () => ({ ok: true, message: "", data: {
            user: { id: "019a7821-1234-7000-8000-012345678901", displayName: "", emails: ["private@example.test"] }
        } }) },
        "models/session": { isSignedIn: () => true, isImpersonating: () => false },
        "models/profileemails": { load: async () => ({ ok: true, data: { items: [], confirmationRequired: true } }) },
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
    await model.submit();
    assert.deepEqual(saved, { birthday: "", hideEmail: false });
    assert.equal(model.message(), "profile.saved");
});

test("failed own-profile loading cannot overwrite saved privacy preferences", async () => {
    let saves = 0;
    const Model = load("viewmodels/profile.js", {
        ko, "models/user": { label: () => "", href: () => null }, "models/i18n": { t: key => key },
        "models/auth": { loadSession: async () => ({ ok: true, message: "", data: { user: {} } }) },
        "models/session": { isSignedIn: () => true, isImpersonating: () => false },
        "models/profileemails": { load: async () => ({ ok: true, data: { items: [], confirmationRequired: true } }) },
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
    await model.submit();
    assert.equal(saves, 0);
    assert.equal(model.busy(), false);
});

function emailModel(api = {}, impersonating = false, confirm = true) {
    const Model = load("viewmodels/profile.js", {
        ko, "models/user": { label: () => "", href: () => null }, "models/i18n": { t: key => key },
        "models/auth": {}, "models/session": { isSignedIn: () => true, isImpersonating: () => impersonating },
        "models/about": {}, "models/usersettings": {}, "models/profilepicture": {}, "models/profiles": {},
        "models/profileemails": {
            load: async () => ({ ok: true, data: { items: [
                { id: "first", value: "first@test.com", confirmedAt: "2026-11-12T09:00:00Z" },
                { id: "second", value: "second@test.com", confirmedAt: null }
            ], confirmationRequired: true } }),
            ...api
        }
    }, { confirm: () => confirm });
    return new Model();
}

test("profile lists separate email drafts and confirmation status without enabling unloaded actions", async () => {
    const model = emailModel();
    assert.equal(model.canManageEmails(), false);
    await model.refreshEmails();
    assert.equal(model.canManageEmails(), true);
    assert.equal(model.emails().length, 2);
    assert.equal(model.emails()[0].confirmed, true);
    assert.equal(model.emails()[1].confirmed, false);
    model.emails()[1].draft("replacement@test.com");
    assert.equal(model.emails()[1].value, "second@test.com");
    assert.equal(model.emailConfirmationRequired(), true);
});

test("adding email clears input only after persistence and reports required confirmation", async () => {
    const calls = [];
    const model = emailModel({ save: async (id, value) => {
        calls.push([id, value]);
        return { ok: true, data: { confirmationRequired: true, emailSent: true } };
    } });
    await model.refreshEmails();
    model.newEmail("new@test.com");
    await model.addEmail();
    assert.deepEqual(calls, [[null, "new@test.com"]]);
    assert.equal(model.newEmail(), "");
    assert.equal(model.message(), "profile.emailConfirmationSent");
    assert.equal(model.busy(), false);
});

test("editing targets the address id and explicitly reports a persisted address whose mail failed", async () => {
    const calls = [];
    const model = emailModel({ save: async (id, value) => {
        calls.push([id, value]);
        return { ok: true, data: { confirmationRequired: true, emailSent: false } };
    } });
    await model.refreshEmails();
    const item = model.emails()[1];
    item.draft("new@test.com");
    await model.saveEmail(item);
    assert.deepEqual(calls, [["second", "new@test.com"]]);
    assert.equal(model.message(), "profile.emailSavedSendFailed");
});

test("duplicate email failures preserve the draft and remain visible", async () => {
    const model = emailModel({ save: async () => ({ ok: false, message: "Already in use" }) });
    await model.refreshEmails();
    model.newEmail("duplicate@test.com");
    await model.addEmail();
    assert.equal(model.newEmail(), "duplicate@test.com");
    assert.equal(model.message(), "Already in use");
    assert.equal(model.busy(), false);
});

test("email operations stay blocked when impersonating, busy or not loaded", async () => {
    let calls = 0;
    const api = { save: async () => { calls++; return { ok: true }; } };
    for (const impersonating of [false, true]) {
        const model = emailModel(api, impersonating);
        await model.addEmail();
        await model.refreshEmails();
        if (!impersonating) {
            model.busy(true);
        }
        await model.addEmail();
    }
    assert.equal(calls, 0);
});

test("email resend and removal target the saved address, with cancellable removal", async () => {
    const calls = [];
    const api = {
        resend: async id => { calls.push(["resend", id]); return { ok: true }; },
        remove: async id => { calls.push(["remove", id]); return { ok: true }; }
    };
    const cancelled = emailModel(api, false, false);
    await cancelled.refreshEmails();
    await cancelled.removeEmail(cancelled.emails()[0]);
    assert.equal(calls.length, 0);
    const model = emailModel(api);
    await model.refreshEmails();
    await model.resendEmail(model.emails()[1]);
    assert.equal(model.message(), "profile.emailConfirmationSent");
    await model.removeEmail(model.emails()[1]);
    assert.equal(model.message(), "profile.emailRemoved");
    assert.deepEqual(calls, [["resend", "second"], ["remove", "second"]]);
});

test("email refresh errors never become success feedback or unhandled rejections", async () => {
    let loads = 0;
    const model = emailModel({
        load: async () => {
            if (++loads > 1) {
                throw new Error("Refresh failed");
            }
            return { ok: true, data: { items: [], confirmationRequired: true } };
        },
        save: async () => ({ ok: true, data: { confirmationRequired: false } })
    });
    await model.refreshEmails();
    await model.addEmail();
    assert.equal(model.message(), "Refresh failed");
    assert.equal(model.busy(), false);
});

test("email API sends exact resource requests and translates last-address and mail failures", async () => {
    const calls = [];
    const api = load("models/profileemails.js", {
        "models/i18n": { t: key => key },
        "models/http": {
            get: async url => { calls.push(["get", url]); return { ok: true, data: { items: [] } }; },
            post: async (url, body) => { calls.push(["post", url, body]); return { ok: false, status: 503 }; },
            put: async (url, body) => { calls.push(["put", url, body]); return { ok: false, status: 409, data: { code: "emailInUse" } }; },
            delete: async url => { calls.push(["delete", url]); return { ok: false, status: 409, data: { code: "lastEmail" } }; }
        }
    });
    assert.equal((await api.load()).ok, true);
    assert.equal((await api.save(null, "new@test.com")).message, "profile.emailSendFailed");
    assert.equal((await api.save("second", "edit@test.com")).message, "profile.emailInUse");
    assert.equal((await api.remove("second")).message, "profile.emailLast");
    assert.equal((await api.resend("second")).message, "profile.emailSendFailed");
    assert.equal(calls[0][1], "/profile/emails");
    assert.equal(calls[1][1], "/profile/emails");
    assert.equal(calls[1][2].email, "new@test.com");
    assert.equal(calls[2][1], "/profile/emails/second");
    assert.equal(calls[2][2].email, "edit@test.com");
    assert.equal(calls[3][1], "/profile/emails/second");
    assert.equal(calls[4][1], "/profile/emails/second/confirmation");
});
