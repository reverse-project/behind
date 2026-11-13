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
        Promise
    });
    return result;
}

test("recovery grant becomes usable after completing a two-factor challenge", async () => {
    const accounts = { twoFactor: ko.observable(true), passkey: ko.observable(true) };
    const result = { ok: true, passwordReset: { token: "reset" }, recoveryCodes: [] };
    const auth = { twoFactor: { verifyTotp: async () => result } };
    const TwoFactor = load("viewmodels/twofactorchallenge.js", {
        ko, "models/auth": auth, "models/webauthn": { supported: () => true }, "models/accounts": accounts,
        "models/i18n": { t: value => value }
    });
    let done;
    const challenge = new TwoFactor({ done: value => { done = value; }, back: () => {} });
    challenge.begin({ ticket: "ticket", methods: ["Totp"], function: "LostPassword" });
    challenge.code("123456");
    await challenge.verifyTotp();
    assert.equal(challenge.active(), false);
    assert.equal(done.passwordReset.token, "reset");
    assert.equal(done.lostPassword, true);
});

test("recovery codes remain visible until acknowledged and then allow password reset", () => {
    const TwoFactor = load("viewmodels/twofactorchallenge.js", {
        ko, "models/auth": {}, "models/webauthn": { supported: () => true },
        "models/accounts": { twoFactor: ko.observable(true), passkey: ko.observable(true) },
        "models/i18n": { t: value => value }
    });
    let done;
    const challenge = new TwoFactor({ done: value => { done = value; }, back: () => {} });
    challenge.begin({ ticket: "ticket", methods: ["Recovery"], function: "LostPassword" });
    challenge.complete({ ok: true, passwordReset: { token: "reset" }, recoveryCodes: ["one"] });
    assert.equal(challenge.showCodes(), true);
    assert.equal(done, undefined);
    challenge.proceed();
    assert.equal(challenge.active(), false);
    assert.equal(done.passwordReset.token, "reset");
    assert.equal(done.lostPassword, true);
});

test("magic-link recovery displays a password form without starting a session", async () => {
    let resetRequest;
    const accounts = { twoFactor: ko.observable(true), passkey: ko.observable(true) };
    const auth = {
        signInWithMagicLink: async () => ({ ok: true, twoFactor: {
            ticket: "ticket", methods: ["Totp"], function: "LostPassword"
        } }),
        resetPassword: async (token, password) => {
            resetRequest = [token, password];
            return { ok: true, message: "done" };
        },
        twoFactor: { verifyTotp: async () => ({ ok: true, passwordReset: { token: "reset" } }) }
    };
    const TwoFactor = load("viewmodels/twofactorchallenge.js", {
        ko, "models/auth": auth, "models/webauthn": { supported: () => true }, "models/accounts": accounts,
        "models/i18n": { t: value => value }
    });
    const MagicLink = load("viewmodels/magiclink.js", {
        ko, "viewmodels/twofactorchallenge": TwoFactor, "models/auth": auth, "models/i18n": { t: value => value }
    });
    const view = new MagicLink({ params: { token: "link" }, scrub: () => {}, replace: () => {
        throw new Error("recovery must not navigate to a signed-in session");
    } });
    await view.activate();
    view.twoFactor.code("123456");
    await view.twoFactor.verifyTotp();
    assert.equal(view.resettingPassword(), true);
    assert.equal(view.twoFactor.active(), false);
    view.newPassword("new password");
    await view.submitPassword();
    assert.deepEqual(resetRequest, ["reset", "new password"]);
    assert.equal(view.passwordChanged(), true);
});
