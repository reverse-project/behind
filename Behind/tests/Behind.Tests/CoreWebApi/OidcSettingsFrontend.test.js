const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const root = process.argv[2];
const ko = require(path.join(root, "libraries/knockout.min.js"));

function load(file, dependencies) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(root, file), "utf8"), {
        define: (names, factory) => { result = factory(...names.map(name => dependencies[name])); },
        Promise, URL, window: { confirm: () => true }
    }, { filename: file });
    return result;
}

async function main() {
    const calls = [];
    const config = {
        ticketLifetime: "00:02:00", authenticationTimeout: "00:10:00", maxPendingTickets: 1000,
        providers: [{
            id: "corp", displayName: "Corporate", enabled: true, authority: "https://id.example",
            clientId: "client", clientSecret: "", hasClientSecret: true, scopes: ["openid", "email"],
            autoCreateUsers: true, linkByVerifiedEmail: false, roleClaimType: "roles",
            roleMappings: { admin: ["Administrator", "Operator"] }, autoCreateRoles: false,
            autoCreateRolePrefix: null
        }]
    };
    let oidcResponse = { ok: true, data: config };
    const files = { readOnly: false, files: [{ name: "security.json", editor: null, fields: [
        { path: "Security:Something", type: "string", value: "old", editable: true, sensitive: false, action: null }
    ] }, { name: "other.json", editor: null, fields: [] }] };
    const http = {
        get: url => {
            calls.push(["GET", url]);
            return Promise.resolve(url === "/settings" ? { ok: true, data: files } : oidcResponse);
        },
        put: (url, body) => {
            calls.push(["PUT", url, body]);
            return Promise.resolve({ ok: true });
        }
    };
    const i18n = { t: key => key };
    const settings = load("models/settings.js", { "models/http": http, "models/i18n": i18n });
    const Oidc = load("viewmodels/oidcsettings.js", { ko, "models/settings": settings });
    const session = {
        isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true
    };
    const stub = function () {
        this.canSave = () => true;
        this.load = () => Promise.resolve({ ok: true });
    };
    const Settings = load("viewmodels/settings.js", {
        ko, "models/settings": settings, "models/session": session, "models/i18n": i18n,
        "viewmodels/emailsettings": stub, "viewmodels/loggingsettings": stub,
        "viewmodels/opentelemetrysettings": stub, "viewmodels/oidcsettings": Oidc
    });
    const page = new Settings();
    await page.refresh();
    assert.equal(page.isOidcSelected(), true);
    assert.equal(page.isFieldTabSelected(), true);
    assert.equal(page.selected().fields.length, 1);
    assert.deepEqual(calls.slice(0, 2).map(call => call.slice(0, 2)), [
        ["GET", "/settings"], ["GET", "/settings/security.json/oidc"]
    ]);
    const editor = page.oidc;
    const p = editor.providers()[0];
    assert.equal(p.clientSecret(), "");
    assert.equal(p.hasClientSecret(), true);
    assert.equal(editor.canSave(), true);
    let request = editor.request();
    assert.equal(request.providers[0].clientSecret, "");
    assert.equal(request.providers[0].clearClientSecret, false);
    p.id("renamed");
    assert.equal(p.hasClientSecret(), false);
    assert.equal(editor.request().providers[0].id, "renamed");
    assert.equal(editor.request().providers[0].clientSecret, "");
    p.id("corp");
    assert.equal(p.hasClientSecret(), true);
    assert.equal(request.ticketLifetime, config.ticketLifetime);
    assert.equal(request.authenticationTimeout, config.authenticationTimeout);
    assert.equal(request.maxPendingTickets, config.maxPendingTickets);
    assert.deepEqual(Array.from(request.providers[0].roleMappings.admin), ["Administrator", "Operator"]);
    p.clientSecret("new secret");
    p.clearClientSecret(true);
    assert.equal(editor.request().providers[0].clientSecret, "");
    assert.equal(editor.request().providers[0].clearClientSecret, true);
    p.clearClientSecret(false);
    assert.equal(editor.request().providers[0].clientSecret, "new secret");
    p.addScope();
    assert.equal(editor.canSave(), false);
    p.scopes()[2].value("groups");
    p.addMapping();
    p.roleMappings()[1].external("staff");
    assert.equal(editor.canSave(), false);
    p.roleMappings()[1].addRole();
    p.roleMappings()[1].roles()[0].value("Staff");
    assert.equal(editor.canSave(), true);
    editor.add();
    const added = editor.providers()[1];
    added.id("CORP");
    assert.equal(editor.canSave(), false);
    added.id("new-provider");
    added.displayName("New");
    added.authority("https://new.example");
    added.clientId("new-client");
    assert.equal(editor.canSave(), true);
    request = editor.request();
    assert.equal(Object.hasOwn(request, "enabled"), false);
    assert.equal(request.providers.length, 2);
    assert.deepEqual(Array.from(request.providers[0].roleMappings.staff), ["Staff"]);
    page.selected().fields[0].value("unsaved generic value");
    await page.saveOidc();
    const saved = calls.find(call => call[0] === "PUT");
    assert.equal(saved[1], "/settings/security.json/oidc");
    assert.equal(saved[2].providers[0].clientSecret, "new secret");
    assert.equal(editor.providers()[0].clientSecret(), "");
    assert.equal(page.selected().fields[0].value(), "unsaved generic value");
    editor.providers()[0].displayName("Unsaved provider");
    await page.save();
    assert.equal(editor.providers()[0].displayName(), "Unsaved provider");
    assert.equal(calls.filter(call => call[0] === "PUT")[1][1], "/settings/security.json");
    editor.remove(editor.providers()[0]);
    assert.equal(editor.providers().length, 0);
    editor.discard();
    assert.equal(editor.providers().length, 1);
    page.readOnly(true);
    assert.equal(editor.canSave(), false);
    await page.saveOidc();
    assert.equal(calls.filter(call => call[0] === "PUT").length, 2);
    page.readOnly(false);
    page.busy(true);
    assert.equal(editor.canSave(), false);
    page.busy(false);
    await page.choose(page.files()[1]);
    assert.equal(page.isOidcSelected(), false);
    await page.saveOidc();
    assert.equal(calls.filter(call => call[0] === "PUT").length, 2);
    oidcResponse = { ok: false, status: 403, data: { code: "configuration_read_only" } };
    await page.choose(page.files()[0]);
    assert.equal(editor.loaded(), false);
    assert.equal(editor.canSave(), false);
    assert.equal(page.message(), "settings.error.readOnly");
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(root, "locales", locale + ".json"), "utf8")).messages;
        const view = fs.readFileSync(path.join(root, "views/settings.html"), "utf8");
        const keys = view.match(/settings\.oidc\.[A-Za-z]+/g) || [];
        for (const key of keys.concat(["settings.oidc.saved"])) {
            assert.ok(messages[key], `${locale}: ${key}`);
        }
    }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
