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
        Promise, Date, encodeURIComponent
    }, { filename: file });
    return result;
}

test("audit filters, sorting, paging and reset are sent from the view model", async () => {
    const calls = [];
    const i18n = { t: key => key, tOr: (key, fallback) => key === "audit.entities.ModuleEntry" ? fallback : key, dateTime: value => value };
    const createPager = load("models/pager.js", { ko, "models/i18n": i18n });
    const AuditLog = load("viewmodels/auditlog.js", {
        ko, "models/i18n": i18n, "models/pager": createPager,
        "models/user": { href: id => id },
        "models/session": { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true },
        "models/auth": {
            loadAuditLog: async query => {
                calls.push(query);
                return { ok: true, message: "", data: { page: query.page, pageCount: 1, total: 0, items: [] } };
            },
            loadAuditEntityTypes: async () => ({ ok: true, data: ["Role", "User", "ModuleEntry"] })
        }
    });
    const page = new AuditLog();
    page.search(" editors ");
    page.entity(" Role ");
    page.action("Updated");
    page.actor(" ada ");
    page.behalfOf(" bob ");
    page.sortBy("actor");
    page.oldestFirst(true);
    page.pager.page(3);
    await page.find();
    assert.equal(calls[0].page, 1);
    assert.equal(calls[0].search, "editors");
    assert.equal(calls[0].entity, "Role");
    assert.equal(calls[0].action, "Updated");
    assert.equal(calls[0].actor, "ada");
    assert.equal(calls[0].behalfOf, "bob");
    assert.equal(calls[0].sortBy, "actor");
    assert.equal(calls[0].descending, false);
    assert.deepEqual(Array.from(page.entityTypes(), item => item.value), ["", "Role", "User", "ModuleEntry"]);
    assert.equal(page.entityTypes()[1].label(), "audit.entities.Role");
    assert.equal(page.entityTypes()[3].label(), "ModuleEntry");
    await page.reset();
    assert.equal(calls[1].search, "");
    assert.equal(calls[1].entity, "");
    assert.equal(calls[1].action, "");
    assert.equal(calls[1].actor, "");
    assert.equal(calls[1].behalfOf, "");
    assert.equal(calls[1].sortBy, "timestamp");
    assert.equal(calls[1].descending, true);
});

test("audit query parameters are URL-encoded", async () => {
    let url;
    const auth = load("models/auth.js", {
        "models/i18n": { t: key => key }, "models/session": {}, "models/user": {}, "models/webauthn": {},
        "models/http": { get: async value => {
            url = value;
            return { ok: true, data: {} };
        } }
    });
    await auth.loadAuditLog({
        fromDate: "2026-01-01", toDate: "2026-01-02", search: "a&b", entity: "Role",
        action: "Updated", actor: "a+b@example.test", behalfOf: "Bob Smith",
        sortBy: "actor", descending: true, oldestFirst: false, page: 1, pageSize: 20
    });
    const query = new URL(url, "https://example.test").searchParams;
    assert.equal(query.get("search"), "a&b");
    assert.equal(query.get("actor"), "a+b@example.test");
    assert.equal(query.get("behalfOf"), "Bob Smith");
    assert.equal(query.get("sortBy"), "actor");
    assert.equal(query.get("descending"), "true");
    assert.equal(query.get("entityExact"), "true");
});

test("entity types load once access is granted and failures remain visible until retry", async () => {
    let allowed = false;
    let failed = true;
    const grant = ko.observable(false);
    const i18n = { t: key => key, tOr: (key, fallback) => key || fallback };
    const createPager = load("models/pager.js", { ko, "models/i18n": i18n });
    const AuditLog = load("viewmodels/auditlog.js", {
        ko, "models/i18n": i18n, "models/pager": createPager,
        "models/user": {},
        "models/session": { isSignedIn: ko.observable(true), grants: grant, hasGrant: () => { grant(); return allowed; } },
        "models/auth": {
            loadAuditLog: async query => ({ ok: true, message: "", data: { page: query.page, pageCount: 0, total: 0, items: [] } }),
            loadAuditEntityTypes: async () => failed
                ? { ok: false, message: "Failed to load types" }
                : { ok: true, data: ["Role"] }
        }
    });
    const page = new AuditLog();
    await page.activate();
    assert.equal(page.entityTypes().length, 1);
    allowed = true;
    grant(true);
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(page.message(), "Failed to load types");
    failed = false;
    await page.find();
    assert.equal(page.message(), "");
    assert.equal(page.entityTypes()[1].value, "Role");
    page.entity("Role");
    allowed = false;
    grant(false);
    assert.equal(page.entity(), "");
    assert.equal(page.entityTypes().length, 1);
});

test("module entity labels translate in both the selector and rows and react to language changes", async () => {
    const language = ko.observable("en-US");
    const translations = {
        "en-US": JSON.parse(fs.readFileSync(path.join(core, "locales/en-US.json"), "utf8")).messages,
        "hu-HU": JSON.parse(fs.readFileSync(path.join(core, "locales/hu-HU.json"), "utf8")).messages
    };
    const forum = path.resolve(core, "../../../Forum/Behind.Forum.WebApi/Frontend/modules/forum/locales");
    for (const locale of Object.keys(translations)) {
        Object.assign(translations[locale], JSON.parse(fs.readFileSync(path.join(forum, locale + ".json"), "utf8")).messages);
    }
    const i18n = {
        t: key => translations[language()][key] || key,
        tOr: (key, fallback) => translations[language()][key] || fallback,
        dateTime: value => value
    };
    const createPager = load("models/pager.js", { ko, "models/i18n": i18n });
    const AuditLog = load("viewmodels/auditlog.js", {
        ko, "models/i18n": i18n, "models/pager": createPager,
        "models/user": { href: id => id },
        "models/session": { isSignedIn: ko.observable(true), grants: ko.observable([]), hasGrant: () => true },
        "models/auth": {
            loadAuditLog: async () => ({ ok: true, message: "", data: { page: 1, pageCount: 1, total: 1, items: [{
                timestamp: "2026-01-01", actor: { label: "Guest", id: null, emails: [] }, onBehalfOf: null,
                action: "Created", entityName: "ForumBoard", entityKey: "1", entityLabel: "Example", changes: []
            }] } }),
            loadAuditEntityTypes: async () => ({ ok: true, data: ["ForumBoard", "UnknownType"] })
        }
    });
    const page = new AuditLog();
    await page.activate();
    assert.equal(page.entityTypes()[1].label(), "Forum board");
    assert.equal(page.entries()[0].entityName(), "Forum board");
    assert.equal(page.entityTypes()[2].label(), "UnknownType");
    language("hu-HU");
    assert.equal(page.entityTypes()[1].label(), "Fórumtábla");
    assert.equal(page.entries()[0].entityName(), "Fórumtábla");
});
