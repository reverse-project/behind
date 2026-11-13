const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key };
const plain = value => JSON.parse(JSON.stringify(value));

function load(file, dependencies, globals = {}) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise,
        ...globals
    }, { filename: file });
    return result;
}

function authentication(responses, browser = { get: async () => ({ ok: true, credential: { id: "credential" } }) }) {
    const requests = [];
    const sessions = [];
    const auth = load("models/auth.js", {
        "models/http": {
            post: async (url, body) => {
                requests.push({ url, body: plain(body) });
                assert.ok(responses.length, url);
                return responses.shift();
            }
        },
        "models/session": { start: data => sessions.push(data) },
        "models/user": {},
        "models/i18n": i18n,
        "models/webauthn": browser
    });
    return { auth, requests, sessions };
}

function challenge(auth, settings = {}) {
    const TwoFactor = load("viewmodels/twofactorchallenge.js", {
        ko,
        "models/auth": auth,
        "models/i18n": i18n,
        "models/webauthn": { supported: () => settings.supported !== false },
        "models/accounts": { twoFactor: ko.observable(true), passkey: ko.observable(true) }
    });
    const done = [];
    const back = [];
    const view = new TwoFactor({
        done: result => done.push(result),
        back: message => back.push(message),
        allowEmail: settings.allowEmail
    });
    return { view, done, back, TwoFactor };
}

const ok = data => ({ ok: true, status: 200, data });
const denied = (status, code) => ({ ok: false, status, data: code ? { code } : {} });
const addresses = [{ id: "first", value: "a***@example.org" }, { id: "second", value: "b***@example.org" }];

test("primary passkey uses email and captcha, browser assertion, then normal auth completion", async () => {
    let requestedOptions;
    const options = { challenge: "challenge" };
    const { auth, requests, sessions } = authentication([
        ok({ ticket: "primary", options }),
        ok({ accessToken: "access", refreshToken: "refresh" })
    ], { get: async value => {
        requestedOptions = value;
        return { ok: true, credential: { id: "credential" } };
    } });
    const result = await auth.signInWithPasskey("user@example.org", "captcha");
    assert.equal(requestedOptions, options);
    assert.deepEqual(requests, [
        { url: "/sign/in/passkey/options", body: { email: "user@example.org", captcha: "captcha" } },
        { url: "/sign/in/passkey", body: { ticket: "primary", credential: { id: "credential" } } }
    ]);
    assert.equal(result.ok, true);
    assert.equal(sessions.length, 1);
    assert.equal(sessions[0].accessToken, "access");
});

test("primary passkey returns an Email challenge without starting a session", async () => {
    const { auth, sessions } = authentication([
        ok({ ticket: "primary", options: {} }),
        ok({ ticket: "factor", methods: ["Email", "Recovery"], setupRequired: false })
    ]);
    const result = await auth.signInWithPasskey("user@example.org", "captcha");
    assert.deepEqual(plain(result.twoFactor), {
        ticket: "factor", methods: ["Email", "Recovery"], setupRequired: false, function: null
    });
    assert.equal(sessions.length, 0);
});

test("passkey cancellation and unsupported browsers never submit a credential", async () => {
    for (const supported of [true, false]) {
        const webauthn = load("models/webauthn.js", { "models/i18n": i18n }, {
            window: {
                PublicKeyCredential: supported ? function () {} : undefined,
                atob: value => Buffer.from(value, "base64").toString("binary")
            },
            navigator: { credentials: { get: async () => {
                const error = new Error();
                error.name = "NotAllowedError";
                throw error;
            } } }
        });
        const { auth, requests, sessions } = authentication([ok({ ticket: "primary", options: { challenge: "AA" } })], webauthn);
        const result = await auth.signInWithPasskey("user@example.org", "captcha");
        assert.equal(result.ok, false);
        assert.equal(result.message, supported ? "auth.twoFactor.passkeyCancelled" : "auth.twoFactor.passkeyUnsupported");
        assert.equal(requests.length, 1);
        assert.equal(sessions.length, 0);
    }
});

test("primary passkey rejects missing email and handles captcha, expired, unavailable, banned and throttled requests", async () => {
    const empty = authentication([]);
    assert.equal((await empty.auth.signInWithPasskey(" ", "captcha")).message, "auth.passkey.invalidEmail");
    assert.equal(empty.requests.length, 0);
    for (const [response, expected] of [
        [denied(400, "captchaInvalid"), "auth.captcha.invalid"],
        [denied(401), "auth.passkey.invalid"],
        [denied(403, "emailNotConfirmed"), "auth.emailNotConfirmed"],
        [denied(404), "auth.passkey.unavailable"],
        [denied(429), "auth.tooManyAttempts"]
    ]) {
        const { auth, sessions } = authentication([response]);
        assert.equal((await auth.signInWithPasskey("user@example.org", "captcha")).message, expected);
        assert.equal(sessions.length, 0);
    }
    const expired = authentication([ok({ ticket: "primary", options: {} }), denied(401)]);
    assert.equal((await expired.auth.signInWithPasskey("user@example.org", "captcha")).message, "auth.passkey.invalid");
    assert.equal(expired.sessions.length, 0);
});

test("sign-in passkey works with passwords, magic links and two-factor disabled and refreshes captcha", async () => {
    const result = { ok: true, twoFactor: { ticket: "factor", methods: ["Recovery"] } };
    const requests = [];
    const accounts = {
        password: ko.observable(false), magicLink: ko.observable(false),
        twoFactor: ko.observable(false), passkey: ko.observable(true)
    };
    const { TwoFactor } = challenge({});
    const Form = load("viewmodels/form.js", { ko });
    let refreshed = 0;
    function Captcha() {
        this.response = () => "captcha";
        this.refresh = async () => { refreshed++; };
    }
    const CaptchaForm = load("viewmodels/captchaform.js", { "viewmodels/form": Form, "viewmodels/captcha": Captcha });
    const SignIn = load("viewmodels/signin.js", {
        "viewmodels/captchaform": CaptchaForm, "viewmodels/twofactorchallenge": TwoFactor,
        "models/accounts": accounts,
        "models/auth": { signInWithPasskey: async (email, captcha) => {
            requests.push([email, captcha]);
            return result;
        } }
    });
    const view = new SignIn({ navigate: () => {} });
    view.email("user@example.org");
    assert.equal(view.passkeySignInEnabled(), true);
    await view.submit();
    await view.usePasskey(null, { currentTarget: { form: { reportValidity: () => false } } });
    assert.equal(requests.length, 0);
    await view.usePasskey(null, { currentTarget: { form: { reportValidity: () => true } } });
    assert.deepEqual(requests, [["user@example.org", "captcha"]]);
    assert.equal(refreshed, 1);
    assert.equal(view.twoFactor.ticket(), "factor");
    assert.equal(view.busy(), false);
    accounts.passkey(false);
    await view.usePasskey();
    assert.equal(requests.length, 1);
});

test("sign-in passkey shares normal completion and recovery-code acknowledgement", async () => {
    const { TwoFactor } = challenge({});
    const Form = load("viewmodels/form.js", { ko });
    const SignIn = load("viewmodels/signin.js", {
        "viewmodels/captchaform": Form,
        "viewmodels/twofactorchallenge": TwoFactor,
        "models/accounts": { password: ko.observable(false), magicLink: ko.observable(false), passkey: ko.observable(true) },
        "models/auth": { signInWithPasskey: async () => ({ ok: true, recoveryCodes: ["one"] }) }
    });
    const navigations = [];
    const view = new SignIn({ navigate: value => navigations.push(value) });
    await view.usePasskey();
    assert.equal(view.twoFactor.showCodes(), true);
    assert.deepEqual(navigations, []);
    view.twoFactor.proceed();
    assert.deepEqual(navigations, ["#/"]);
});

test("Email addresses and send responses are intermediate, verify completes authentication", async () => {
    const { auth, requests, sessions } = authentication([
        ok({ addresses }),
        ok({ ticket: "delivery" }),
        ok({ accessToken: "access" })
    ]);
    const choices = await auth.twoFactor.emailAddresses("factor");
    assert.deepEqual(plain(choices.data.addresses), addresses);
    const sent = await auth.twoFactor.sendEmail("factor", "second");
    assert.equal(sent.ticket, "delivery");
    assert.equal(sent.message, "auth.twoFactor.emailSent");
    assert.equal(sessions.length, 0);
    const verified = await auth.twoFactor.verifyEmail("delivery", "123456");
    assert.equal(verified.ok, true);
    assert.equal(sessions.length, 1);
    assert.deepEqual(requests, [
        { url: "/sign/in/two-factor/email/addresses", body: { ticket: "factor" } },
        { url: "/sign/in/two-factor/email", body: { ticket: "factor", addressId: "second" } },
        { url: "/sign/in/two-factor/email/verify", body: { ticket: "delivery", code: "123456" } }
    ]);
});

test("Email-only challenge loads confirmed choices, requires selection, sends/resends and verifies latest ticket", async () => {
    const { auth, requests } = authentication([
        ok({ addresses }),
        ok({ ticket: "delivery-1" }),
        ok({ ticket: "delivery-2" }),
        ok({ accessToken: "access", recoveryCodes: ["recovery"] })
    ]);
    const { view, done } = challenge(auth);
    await view.begin({ ticket: "factor", methods: ["Email", "Recovery"] });
    assert.equal(view.showEmailForm(), true);
    assert.equal(view.showRecoveryForm(), false);
    assert.equal(view.emailId(), "");
    await view.sendEmail();
    assert.equal(requests.length, 1);
    view.emailId("second");
    await view.sendEmail();
    assert.equal(view.emailSent(), true);
    assert.equal(view.ticket(), "delivery-1");
    view.emailCode("old");
    await view.sendEmail();
    assert.equal(view.ticket(), "delivery-2");
    assert.equal(view.emailCode(), "");
    view.emailCode("123 456");
    await view.verifyEmail();
    assert.equal(requests[3].body.ticket, "delivery-2");
    assert.equal(requests[3].body.code, "123456");
    assert.equal(view.showCodes(), true);
    assert.equal(done.length, 0);
    view.proceed();
    assert.equal(done.length, 1);
    assert.equal(view.active(), false);
    assert.equal(view.emailAddresses().length, 0);
    assert.equal(view.emailId(), "");
});

test("Email selection, Totp, Recovery and Passkey coexist without overriding available methods", async () => {
    const { auth, requests } = authentication([ok({ addresses }), ok({ ticket: "delivery" })]);
    const { view } = challenge(auth);
    view.begin({ ticket: "factor", methods: ["Totp", "Email", "Recovery", "Passkey"] });
    assert.equal(view.showTotpForm(), true);
    assert.equal(requests.length, 0);
    await view.useEmail();
    assert.equal(view.showEmailForm(), true);
    assert.equal(view.showTotpForm(), false);
    view.emailId("first");
    await view.sendEmail();
    view.toggleRecovery();
    assert.equal(view.showRecoveryForm(), true);
    assert.equal(view.showEmailForm(), false);
    await view.useEmail();
    assert.equal(view.emailSent(), true);
    view.emailCode("old");
    view.emailId("second");
    assert.equal(view.emailSent(), false);
    assert.equal(view.emailCode(), "");
    view.useTotp();
    assert.equal(view.showTotpForm(), true);
    assert.equal(view.showEmailForm(), false);
    assert.equal(view.passkeyAvailable(), true);
    assert.equal(requests.length, 2);
});

test("challenge does not call unavailable methods or unsupported passkeys", async () => {
    const { auth, requests } = authentication([]);
    for (const current of [
        { ticket: "factor", methods: ["Passkey"] },
        { ticket: "factor", methods: [] }
    ]) {
        const { view } = challenge(auth, { supported: false });
        view.begin(current);
        await view.useEmail();
        await view.sendEmail();
        await view.verifyEmail();
        await view.verifyTotp();
        await view.verifyRecovery();
        await view.usePasskey();
    }
    assert.equal(requests.length, 0);
});

test("required setup can use a confirmed email code", async () => {
    const { auth, requests } = authentication([ok({ addresses }), ok({ ticket: "delivery" }), ok({ accessToken: "access" })]);
    const { view, done } = challenge(auth);
    view.begin({ ticket: "factor", methods: ["Totp", "Email"], setupRequired: true });
    await view.useEmail();
    assert.equal(view.showEmailForm(), true);
    view.emailId("first");
    await view.sendEmail();
    view.emailCode("123456");
    await view.verifyEmail();
    assert.equal(requests[2].body.ticket, "delivery");
    assert.equal(done.length, 1);
    assert.equal(view.active(), false);
});

test("magic-link challenge never loads or offers Email even if listed in server methods", async () => {
    const { auth, requests } = authentication([]);
    auth.signInWithMagicLink = async () => ({ ok: true, twoFactor: {
        ticket: "factor", methods: ["Email", "Recovery"], function: "LostPassword"
    } });
    const { TwoFactor } = challenge(auth);
    const MagicLink = load("viewmodels/magiclink.js", {
        ko, "viewmodels/twofactorchallenge": TwoFactor, "models/auth": auth, "models/i18n": i18n
    });
    const view = new MagicLink({ params: { token: "link" }, scrub: () => {}, replace: () => {}, navigate: () => {} });
    await view.activate();
    assert.equal(view.twoFactor.hasEmail(), false);
    assert.equal(view.twoFactor.showEmailForm(), false);
    assert.equal(view.twoFactor.showRecoveryForm(), true);
    await view.twoFactor.useEmail();
    assert.equal(requests.length, 0);
});

test("Email errors clear codes, preserve retry state and restart expired challenges", async () => {
    const { auth } = authentication([
        ok({ addresses }), ok({ ticket: "delivery" }),
        denied(403, "invalidCode"), denied(429), denied(401)
    ]);
    const { view, back, done } = challenge(auth);
    await view.begin({ ticket: "factor", methods: ["Email"] });
    view.emailId("first");
    await view.sendEmail();
    view.emailCode("bad");
    await view.verifyEmail();
    assert.equal(view.message(), "auth.twoFactor.invalidCode");
    assert.equal(view.emailCode(), "");
    assert.equal(view.emailSent(), true);
    assert.equal(view.ticket(), "delivery");
    await view.sendEmail();
    assert.equal(view.message(), "auth.tooManyAttempts");
    assert.equal(view.emailSent(), true);
    await view.verifyEmail();
    assert.equal(view.active(), false);
    assert.deepEqual(back, ["auth.twoFactor.ticketInvalid"]);
    assert.equal(done.length, 0);
    assert.equal(view.busy(), false);
});

test("Email address loading and sending handle unavailable, throttled and expired tickets without sessions", async () => {
    for (const operation of ["emailAddresses", "sendEmail"]) {
        for (const [response, expected, restart] of [
            [denied(401), "auth.twoFactor.ticketInvalid", true],
            [denied(403, "twoFactorUnavailable"), "auth.twoFactor.unavailable", false],
            [denied(404), "auth.twoFactor.unavailable", false],
            [denied(429), "auth.tooManyAttempts", false]
        ]) {
            const { auth, sessions } = authentication([response]);
            const result = await auth.twoFactor[operation]("factor", "first");
            assert.equal(result.ok, false);
            assert.equal(result.message, expected);
            assert.equal(result.restart, restart);
            assert.equal(sessions.length, 0);
        }
    }
});

test("email delivery failures are reported without creating a session", async () => {
    const { auth, sessions } = authentication([denied(503, "emailDeliveryFailed")]);
    const result = await auth.twoFactor.sendEmail("factor", "first");
    assert.equal(result.ok, false);
    assert.equal(result.message, "auth.twoFactor.emailDeliveryFailed");
    assert.equal(sessions.length, 0);
});

test("Email choices cannot switch or submit again during an in-flight send", async () => {
    let finish;
    let sends = 0;
    const { view } = challenge({ twoFactor: {
        emailAddresses: async () => ({ ok: true, data: { addresses } }),
        sendEmail: () => {
            sends++;
            return new Promise(resolve => { finish = resolve; });
        }
    } });
    await view.begin({ ticket: "factor", methods: ["Email", "Recovery", "Totp"] });
    await view.useEmail();
    view.emailId("first");
    const sending = view.sendEmail();
    assert.equal(view.busy(), true);
    await view.sendEmail();
    view.toggleRecovery();
    view.useTotp();
    view.back();
    assert.equal(view.showEmailForm(), true);
    assert.equal(sends, 1);
    finish({ ok: true, ticket: "delivery", message: "sent" });
    await sending;
    assert.equal(view.busy(), false);
});

test("frontend texts are localized in English and Hungarian and addresses remain challenge-only", () => {
    const files = ["views/signin.html", "components/twofactorchallenge.html", "viewmodels/twofactorchallenge.js"];
    const keys = new Set(files.flatMap(file => Array.from(
        fs.readFileSync(path.join(core, file), "utf8").matchAll(/["']((?:signin|twoFactor)\.[A-Za-z.]+)["']/g),
        match => match[1]
    )));
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(core, "locales", locale + ".json"), "utf8")).messages;
        for (const key of [...keys, "auth.passkey.invalidEmail", "auth.passkey.invalid", "auth.passkey.unavailable", "auth.twoFactor.emailSent"]) {
            assert.equal(typeof messages[key], "string", locale + ": " + key);
        }
    }
    const primary = fs.readFileSync(path.join(core, "views/signin.html"), "utf8");
    assert.match(primary, /type="email"[^>]*required/);
    assert.match(primary, /visible: passkeySignInEnabled/);
    assert.doesNotMatch(primary, /emailAddresses|addressId/);
    const component = fs.readFileSync(path.join(core, "components/twofactorchallenge.html"), "utf8");
    assert.match(component, /optionsText: 'value'/);
    assert.doesNotMatch(component, /html:/);
});
