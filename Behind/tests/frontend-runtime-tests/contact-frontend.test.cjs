const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const contact = path.resolve(__dirname, "../../src/Contact/Behind.Contact.WebApi/Frontend/modules/contact");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key };

test("every Contact frontend locale key exists in each supported language", () => {
    const keys = new Set([
        "contact.widget", "contact.administration", "menu.item.contact",
        ...["text", "textarea", "email"].map(type => "contact.fieldType." + type),
        ...["sent", "failed", "sending", "notRequested"].map(status => "contact.deliveryStatus." + status)
    ]);
    for (const dir of ["models", "viewmodels", "views"]) {
        for (const file of fs.readdirSync(path.join(contact, dir))) {
            const text = fs.readFileSync(path.join(contact, dir, file), "utf8");
            for (const match of text.matchAll(/['"](contact\.[A-Za-z0-9.]+)['"]/g)) {
                if (!match[1].endsWith(".")) {
                    keys.add(match[1]);
                }
            }
        }
    }
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(contact, "locales", locale + ".json"), "utf8")).messages;
        for (const key of keys) {
            assert.ok(messages[key], `${locale}: missing ${key}`);
        }
    }
});

function load(file, dependencies, browserWindow = { location: { hash: "#/contact" }, confirm: () => true }) {
    let result;
    vm.runInNewContext(fs.readFileSync(file, "utf8"), {
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
        window: browserWindow,
        Promise,
        console
    }, { filename: file });
    return result;
}

function contactModel(http) {
    return load(path.join(contact, "models/contact.js"), {
        "models/http": http,
        "models/i18n": i18n,
        "models/paging": load(path.join(core, "models/paging.js"), {})
    });
}

function publicViewModel(api, hash = "#/contact") {
    const ViewModel = load(path.join(contact, "viewmodels/contact.js"), {
        ko,
        "models/i18n": i18n,
        "modules/contact/models/contact": api
    }, { location: { hash }, confirm: () => true });
    return new ViewModel();
}

function managerViewModel(api, grants = ["Contact.Read", "Contact.Write", "Contact.Delete", "RawDatabase.Manage"], translations = i18n) {
    const ViewModel = load(path.join(contact, "viewmodels/contactmanager.js"), {
        ko,
        "models/session": { hasGrant: grant => grants.includes(grant) },
        "models/i18n": translations,
        "modules/contact/models/contact": api,
        "models/pager": load(path.join(core, "models/pager.js"), { ko, "models/i18n": i18n })
    });
    return new ViewModel();
}

const form = (id, fields = []) => ({
    id,
    version: 3,
    name: "General",
    description: "Send us a message",
    subjectMode: "choices",
    subjects: ["Question", "Feedback"],
    fields
});

const requestPage = (items, page = 1, total = items.length, pageSize = 10) => ({
    items, page, total, pageSize, pageCount: Math.ceil(total / pageSize)
});

test("contact API calls public and management routes with expected payloads", async () => {
    const calls = [];
    const model = contactModel({
        get: async path => {
            calls.push({ method: "GET", path });
            return { ok: true, status: 200, data: [] };
        },
        post: async (path, body) => {
            calls.push({ method: "POST", path, body });
            return { ok: true, status: 204, data: null };
        },
        delete: async path => {
            calls.push({ method: "DELETE", path });
            return { ok: true, status: 204, data: null };
        }
    });

    await model.forms();
    await model.form("form / one");
    const payload = { name: "Ada", email: "ada@example.test", subject: "Question", message: "Hello", fields: { phone: "123" } };
    await model.submit("form / one", payload);
    await model.manageForms(true);
    await model.manageRequests();
    await model.manageForm("form-1", true);
    await model.request("request-1", true);
    await model.setRequestRead("request-1", true);
    await model.retryRequest("request-1");
    await model.hardDeleteForm("form-1");

    assert.equal(JSON.stringify(calls), JSON.stringify([
        { method: "GET", path: "/contact/forms" },
        { method: "GET", path: "/contact/forms/form%20%2F%20one" },
        { method: "POST", path: "/contact/forms/form%20%2F%20one/requests", body: payload },
        { method: "GET", path: "/contact/manage/forms?deleted=true" },
        { method: "GET", path: "/contact/manage/requests?page=1&pageSize=10&search=" },
        { method: "GET", path: "/contact/manage/forms/form-1?deleted=true" },
        { method: "GET", path: "/contact/manage/requests/request-1?deleted=true" },
        { method: "POST", path: "/contact/manage/requests/request-1/read", body: { read: true } },
        { method: "POST", path: "/contact/manage/requests/request-1/retry", body: undefined },
        { method: "DELETE", path: "/contact/manage/forms/form-1/hard" }
    ]));
});

test("failed delivery retry surfaces provider failure and management can recover with retry", async () => {
    const model = contactModel({
        post: async () => ({ ok: false, status: 503, data: null })
    });
    const result = await model.retryRequest("request-1");
    assert.equal(result.ok, false);
    assert.equal(result.message, "contact.error.retryFailed");
});

test("public submission rate limits are translated as retry-later feedback", async () => {
    const model = contactModel({ post: async () => ({ ok: false, status: 429 }) });
    const result = await model.submit("form-1", {});
    assert.equal(result.ok, false);
    assert.equal(result.message, "contact.error.rateLimited");
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(contact, "locales", locale + ".json"), "utf8")).messages;
        assert.ok(messages["contact.error.rateLimited"]);
    }
});

test("public form selects a schema, sends all submitted values and resets safely", async () => {
    const calls = [];
    const forms = [form("one"), { ...form("two", [{ key: "department", label: "Department", type: "text", required: true, maxLength: 80 }]), name: "Other" }];
    const api = {
        forms: async () => ({ ok: true, data: forms }),
        form: async id => ({ ok: true, data: forms.find(item => item.id === id) }),
        submit: async (id, body) => {
            calls.push({ id, body });
            return { ok: true, data: { deliveryStatus: "sent" } };
        }
    };
    const view = publicViewModel(api, "#/contact?formId=two");
    await view.activate();
    assert.equal(view.form().id, "two");
    assert.equal(view.fields()[0].key, "department");
    view.name("Ada Lovelace");
    view.email("ada@example.test");
    view.subject("Feedback");
    view.message("Please help");
    view.fields()[0].value("Research");
    await view.submit();

    assert.equal(calls[0].id, "two");
    assert.equal(calls[0].body.name, "Ada Lovelace");
    assert.equal(calls[0].body.email, "ada@example.test");
    assert.equal(calls[0].body.subject, "Feedback");
    assert.equal(calls[0].body.message, "Please help");
    assert.equal(calls[0].body.fields.department, "Research");
    assert.equal(view.name(), "");
    assert.equal(view.fields()[0].value(), "");
    assert.equal(view.notice(), "contact.done.submitted");
});

test("extra field values use own keys even for inherited object property names", async () => {
    const schema = form("one", [{ key: "constructor", label: "Constructor", type: "text", required: true, maxLength: 80 }]);
    let body;
    const view = publicViewModel({
        forms: async () => ({ ok: true, data: [schema] }),
        form: async () => ({ ok: true, data: schema }),
        submit: async (id, fields) => { body = fields; return { ok: true, data: { deliveryStatus: "sent" } }; }
    });
    await view.activate();
    view.fields()[0].value("data");
    await view.submit();
    assert.equal(Object.getPrototypeOf(body.fields), null);
    assert.equal(Object.hasOwn(body.fields, "constructor"), true);
    assert.equal(body.fields.constructor, "data");
});

test("saved submissions distinguish sent, failed and pending delivery without suggesting resubmission", async () => {
    let status = "sent";
    const schema = form("one");
    const view = publicViewModel({
        forms: async () => ({ ok: true, data: [schema] }),
        form: async () => ({ ok: true, data: schema }),
        submit: async () => ({ ok: true, data: { deliveryStatus: status } })
    });
    await view.activate();
    for (const [deliveryStatus, notice] of [
        ["sent", "contact.done.submitted"],
        ["failed", "contact.done.deliveryFailed"],
        ["sending", "contact.done.sending"],
        ["notRequested", "contact.done.savedToInbox"]
    ]) {
        status = deliveryStatus;
        view.message("Message");
        await view.submit();
        assert.equal(view.notice(), notice);
        assert.equal(view.error(), "");
        assert.equal(view.message(), "");
    }
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(contact, "locales", locale + ".json"), "utf8")).messages;
        assert.ok(messages["contact.done.deliveryFailed"]);
        assert.ok(messages["contact.done.sending"]);
        assert.ok(messages["contact.done.savedToInbox"]);
        assert.ok(messages["contact.deliveryStatus.notRequested"]);
        assert.ok(messages["contact.manager.inboxOnly"]);
        assert.ok(messages["contact.manager.form.recipientHint"]);
    }
});

test("contact widget disposal unsubscribes and ignores late activation, schema and submit results", async () => {
    const deferred = () => {
        let resolve;
        let reject;
        const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
        return { promise, resolve, reject };
    };
    const schema = form("one");
    const pendingForms = deferred();
    const activating = publicViewModel({ forms: () => pendingForms.promise });
    const loading = activating.activate();
    const subscriptions = activating.selectedFormId.getSubscriptionsCount();
    assert.ok(subscriptions > 0);
    activating.dispose();
    assert.equal(activating.selectedFormId.getSubscriptionsCount(), subscriptions - 1);
    pendingForms.resolve({ ok: true, data: [schema] });
    await loading;
    assert.equal(activating.forms().length, 0);
    assert.equal(activating.error(), "");

    const pendingSchema = deferred();
    const selecting = publicViewModel({
        forms: async () => ({ ok: true, data: [schema] }),
        form: () => pendingSchema.promise
    });
    const selectingPromise = selecting.activate();
    await new Promise(resolve => setImmediate(resolve));
    selecting.dispose();
    pendingSchema.resolve({ ok: true, data: schema });
    await selectingPromise;
    assert.equal(selecting.form(), null);
    assert.equal(selecting.fields().length, 0);

    const pendingSubmit = deferred();
    const submitting = publicViewModel({
        forms: async () => ({ ok: true, data: [schema] }),
        form: async () => ({ ok: true, data: schema }),
        submit: () => pendingSubmit.promise
    });
    await submitting.activate();
    submitting.message("Original");
    const submitPromise = submitting.submit();
    submitting.dispose();
    pendingSubmit.resolve({ ok: true, data: { deliveryStatus: "sent" } });
    await submitPromise;
    assert.equal(submitting.message(), "Original");
    assert.equal(submitting.notice(), "");
    assert.equal(submitting.error(), "");

    const pendingFailure = deferred();
    const failing = publicViewModel({ forms: () => pendingFailure.promise });
    const failedLoad = failing.activate();
    failing.dispose();
    pendingFailure.reject(new Error("network"));
    await failedLoad;
    assert.equal(failing.error(), "");

    const failedSchema = deferred();
    const selectingFailure = publicViewModel({ form: () => failedSchema.promise });
    const selection = selectingFailure.selectForm(schema);
    selectingFailure.dispose();
    failedSchema.reject(new Error("network"));
    await selection;
    assert.equal(selectingFailure.error(), "");

    const failedSubmit = deferred();
    const submittingFailure = publicViewModel({ submit: () => failedSubmit.promise });
    submittingFailure.form(schema);
    const submission = submittingFailure.submit();
    submittingFailure.dispose();
    failedSubmit.reject(new Error("network"));
    await submission;
    assert.equal(submittingFailure.error(), "");
});

test("public form selection uses the first subject choice and switching schemas reloads custom fields", async () => {
    const forms = [form("one", [{ key: "phone", label: "Phone", type: "text", required: false, maxLength: 40 }]),
        { ...form("two"), subjectMode: "freeText", subjects: [], fields: [] }];
    const api = {
        forms: async () => ({ ok: true, data: forms }),
        form: async id => ({ ok: true, data: forms.find(item => item.id === id) })
    };
    const view = publicViewModel(api);
    await view.activate();
    assert.equal(view.subject(), "Question");
    assert.equal(view.fields().length, 1);
    view.selectedFormId("two");
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(view.form().id, "two");
    assert.equal(view.subject(), "");
    assert.equal(view.fields().length, 0);
});

test("manager saves customizable forms and supports deleted restore and hard delete", async () => {
    const calls = [];
    const savedForm = form("new-form", [{ key: "phone", label: "Phone", type: "text", required: true, maxLength: 50 }]);
    const api = {
        manageForms: async deleted => ({ ok: true, data: deleted ? [{ ...savedForm, isDeleted: true }] : [savedForm] }),
        manageForm: async id => ({ ok: true, data: savedForm }),
        saveForm: async (id, fields) => {
            calls.push({ action: "save", id, fields });
            return { ok: true, data: savedForm };
        },
        removeForm: async id => { calls.push({ action: "delete", id }); return { ok: true }; },
        restoreForm: async id => { calls.push({ action: "restore", id }); return { ok: true }; },
        hardDeleteForm: async id => { calls.push({ action: "hard", id }); return { ok: true }; }
    };
    const view = managerViewModel(api);
    await view.activate();
    view.formName("General");
    view.description("Description");
    view.recipient("team@example.test");
    view.subjectMode("choices");
    view.subjectsText("Help\nFeedback");
    view.formFields.push({
        key: ko.observable("phone"),
        label: ko.observable("Phone"),
        type: ko.observable("text"),
        required: ko.observable(true),
        maxLength: ko.observable(50)
    });
    await view.saveForm();
    assert.equal(calls[0].action, "save");
    assert.equal(calls[0].id, null);
    assert.equal(calls[0].fields.subjects.join(","), "Help,Feedback");
    assert.equal(calls[0].fields.fields[0].key, "phone");
    assert.equal(view.formName(), "");

    await view.editForm(savedForm);
    assert.equal(view.editingFormId(), "new-form");
    await view.removeForm(savedForm);
    view.showDeleted(true);
    await view.loadForms();
    assert.equal(view.forms()[0].isDeleted, true);
    await view.restoreForm(view.forms()[0]);
    await view.hardDeleteForm({ id: "new-form" });
    assert.ok(calls.some(call => call.action === "restore"));
    assert.ok(calls.some(call => call.action === "hard"));
});

test("inbox actions record shared read state, retry failures and render submitted values as text", async () => {
    const calls = [];
    let detail = {
        id: "request-1", version: 1, formId: "form-1", formName: "General",
        recipient: "team@example.test", name: "Ada", email: "ada@example.test",
        subject: "Question", message: "<script>alert(1)</script>",
        fields: [{ key: "note", label: "Note", value: "<img src=x onerror=alert(1)>" }],
        deliveryStatus: "failed", readBy: null, readByName: null, isDeleted: false
    };
    const api = {
        manageForms: async () => ({ ok: true, data: [] }),
        manageRequests: async () => ({ ok: true, data: requestPage(detail ? [detail] : []) }),
        request: async () => ({ ok: true, data: detail }),
        setRequestRead: async (id, read) => {
            calls.push({ action: "read", id, read });
            detail.readBy = read ? "admin-id" : null;
            detail.readByName = read ? "Ada Administrator#1234" : null;
            return { ok: true };
        },
        retryRequest: async id => { calls.push({ action: "retry", id }); detail.deliveryStatus = "sent"; return { ok: true }; },
        removeRequest: async id => { calls.push({ action: "delete", id }); detail.isDeleted = true; return { ok: true }; },
        restoreRequest: async id => { calls.push({ action: "restore", id }); detail.isDeleted = false; return { ok: true }; },
        hardDeleteRequest: async id => { calls.push({ action: "hard", id }); detail = null; return { ok: true }; }
    };
    const view = managerViewModel(api);
    view.activeSection("requests");
    await view.loadRequests();
    await view.selectRequest(detail);
    await view.setRead(true);
    assert.equal(view.requestDetails().readBy, "admin-id");
    assert.equal(view.requestDetails().readByName, "Ada Administrator#1234");
    assert.equal(view.message(), "contact.manager.markedRead");
    await view.setRead(false);
    assert.equal(view.requestDetails().readBy, null);
    assert.equal(view.requestDetails().readByName, null);
    assert.equal(view.message(), "contact.manager.markedUnread");
    await view.retryRequest();
    assert.equal(view.requestDetails().deliveryStatus, "sent");
    await view.removeRequest();
    view.showDeleted(true);
    await view.loadRequests();
    await view.selectRequest(detail);
    await view.restoreRequest();
    view.showDeleted(false);
    await view.loadRequests();
    await view.selectRequest(detail);
    await view.removeRequest();
    view.showDeleted(true);
    await view.loadRequests();
    await view.selectRequest(detail);
    await view.hardDeleteRequest();
    assert.equal(view.requestDetails(), null);
    assert.equal(JSON.stringify(calls), JSON.stringify([
        { action: "read", id: "request-1", read: true },
        { action: "read", id: "request-1", read: false },
        { action: "retry", id: "request-1" },
        { action: "delete", id: "request-1" },
        { action: "restore", id: "request-1" },
        { action: "delete", id: "request-1" },
        { action: "hard", id: "request-1" }
    ]));

    const template = fs.readFileSync(path.join(contact, "views/contactmanager.html"), "utf8");
    assert.match(template, /data-bind="text: message"/);
    assert.match(template, /data-bind="text: value"/);
    assert.doesNotMatch(template, /html:\s*(?:message|value|fields)/);
});

test("inbox read attribution binds safe display-name links and preserves the unread UI", () => {
    const template = fs.readFileSync(path.join(contact, "views/contactmanager.html"), "utf8");
    const attribution = template.match(/<p[^>]+data-bind="(visible: readBy)">\s*<span[^>]+data-bind="t: 'contact\.manager\.readBy'">[^<]+<\/span>:\s*<a data-bind="([^"]+)"><\/a>\s*<\/p>/);
    const unread = template.match(/<p[^>]+data-bind="(visible: !readBy), t: 'contact\.manager\.unread'">Unread<\/p>/);
    assert.ok(attribution, "Read attribution must contain a well-formed bound link");
    assert.ok(unread, "Keep the existing localized unread label");

    const readBy = "11111111-2222-3333-4444-555555555555";
    for (const data of [
        { readBy, readByName: "Ada Administrator#1234" },
        { readBy, readByName: '<img src=x onerror=alert(1)> & "Administrator"' },
        { readBy, readByName: "user#1234" },
        { readBy: null, readByName: null }
    ]) {
        const context = { $data: data };
        const textNode = { nodeType: 3, data: "", nextSibling: null };
        const attributes = {};
        const link = {
            nodeType: 1,
            tagName: "A",
            firstChild: textNode,
            setAttribute: (name, value) => { attributes[name] = value; },
            removeAttribute: name => { delete attributes[name]; }
        };
        const bindings = ko.bindingProvider.instance.parseBindingsString(attribution[2], context, link, { valueAccessors: true });
        assert.deepEqual(Object.keys(bindings).sort(), ["attr", "text"]);
        ko.bindingHandlers.attr.update(link, bindings.attr);
        ko.bindingHandlers.text.update(link, bindings.text);
        assert.equal(attributes.href, data.readBy ? "#/user/" + readBy : undefined);
        assert.equal(link.firstChild, textNode);
        assert.equal(textNode.data, data.readByName || "");

        for (const [binding, visible] of [[attribution[1], !!data.readBy], [unread[1], !data.readBy]]) {
            const paragraph = { style: { display: "" } };
            const parsed = ko.bindingProvider.instance.parseBindingsString(binding, context, paragraph, { valueAccessors: true });
            ko.bindingHandlers.visible.update(paragraph, parsed.visible);
            assert.equal(paragraph.style.display !== "none", visible);
        }
    }
});

test("deleted inbox details use the deleted endpoint and restore clears the removed selection", async () => {
    let deleted = true;
    const calls = [];
    const request = { id: "request-1", isDeleted: true, fields: [], deliveryStatus: "failed" };
    const view = managerViewModel({
        manageRequests: async query => ({ ok: true, data: requestPage(query.deleted === deleted ? [request] : []) }),
        request: async (id, showDeleted) => {
            calls.push({ id, showDeleted });
            return showDeleted === deleted ? { ok: true, data: request } : { ok: false, message: "not found" };
        },
        restoreRequest: async () => { deleted = false; return { ok: true }; }
    });
    view.activeSection("requests");
    view.showDeleted(true);
    await view.loadRequests();
    await view.selectRequest(request);
    assert.equal(view.requestDetails().id, "request-1");
    assert.equal(calls[0].showDeleted, true);
    await view.restoreRequest();
    assert.equal(view.requestDetails(), null);
    assert.equal(view.requests().length, 0);
    assert.equal(calls.length, 1);
    assert.equal(view.message(), "contact.manager.request.restored");
});

test("inbox queries encode search text without injecting paging or filter parameters", async () => {
    const calls = [];
    const model = contactModel({
        get: async url => {
            calls.push(url);
            return { ok: true, data: requestPage([]) };
        }
    });
    const search = "Árvíz Ada+tag@example.test &deleted=false?#/%";
    await model.manageRequests({ page: 3, pageSize: 100, search, unreadOnly: true, deleted: true });
    assert.equal(calls[0], "/contact/manage/requests?page=3&pageSize=100&search="
        + encodeURIComponent(search) + "&unreadOnly=true&deleted=true");
    const query = new URL(calls[0], "https://example.test").searchParams;
    assert.equal(query.get("search"), search);
    assert.deepEqual(query.getAll("deleted"), ["true"]);
    assert.equal(query.get("page"), "3");
    assert.equal(query.get("pageSize"), "100");
    await model.manageRequests({ page: 2, pageSize: 10, unreadOnly: false, deleted: false });
    assert.equal(calls[1], "/contact/manage/requests?page=2&pageSize=10&search=");
    const denied = await contactModel({ get: async () => ({ ok: false, status: 403 }) }).manageRequests();
    assert.equal(denied.message, "contact.error.readDenied");
});

function inboxFixture(records, grants) {
    const calls = [];
    const details = [];
    const api = {
        manageRequests: async query => {
            calls.push({ ...query });
            const matches = records.filter(item =>
                !!item.isDeleted === query.deleted
                && (!query.unreadOnly || !item.readBy)
                && ["name", "email", "subject", "message", "formName"].some(key =>
                    (item[key] || "").toLowerCase().includes(query.search.toLowerCase())));
            return { ok: true, data: requestPage(matches.slice((query.page - 1) * 10, query.page * 10), query.page, matches.length) };
        },
        request: async (id, deleted) => {
            details.push({ id, deleted });
            const item = records.find(item => item.id === id && !!item.isDeleted === deleted);
            return item ? { ok: true, data: { ...item } } : { ok: false, message: "not found" };
        },
        setRequestRead: async (id, read) => {
            records.find(item => item.id === id).readBy = read ? "admin" : null;
            return { ok: true };
        },
        removeRequest: async id => {
            records.find(item => item.id === id).isDeleted = true;
            return { ok: true };
        },
        restoreRequest: async id => {
            records.find(item => item.id === id).isDeleted = false;
            return { ok: true };
        },
        hardDeleteRequest: async id => {
            records.splice(records.findIndex(item => item.id === id), 1);
            return { ok: true };
        },
        retryRequest: async id => {
            records.find(item => item.id === id).deliveryStatus = "sent";
            return { ok: true };
        }
    };
    const view = managerViewModel(api, grants);
    view.activeSection("requests");
    return { view, api, calls, details };
}

function inboxRecords(count) {
    return Array.from({ length: count }, (_, index) => ({
        id: "request-" + (index + 1), name: "Ada", email: "ada@example.test",
        subject: "Question", message: "Help", formName: "General",
        readBy: null, isDeleted: false, deliveryStatus: "failed", fields: []
    }));
}

test("inbox shows read and unread requests by default and uses the shared bounded pager", async () => {
    const records = inboxRecords(23);
    records[0].readBy = "admin";
    const { view, calls } = inboxFixture(records);
    await view.loadRequests();
    assert.equal(view.hideRead(), false);
    assert.deepEqual(calls[0], { page: 1, pageSize: 10, search: "", unreadOnly: false, deleted: false });
    assert.equal(view.requests().length, 10);
    assert.equal(view.requests()[0].readBy, "admin");
    assert.equal(view.pager.pageCount(), 3);
    assert.equal(view.pager.total(), 23);
    assert.equal(view.pager.hasPrevious(), false);
    assert.equal(view.pager.hasNext(), true);
    await view.pager.previous();
    assert.equal(calls.length, 1);
    await view.pager.next();
    assert.equal(view.requests()[0].id, "request-11");
    await view.pager.last();
    assert.equal(view.requests().length, 3);
    assert.equal(view.pager.hasNext(), false);
    await view.pager.next();
    assert.equal(calls.length, 3);
    await view.pager.previous();
    assert.equal(view.pager.page(), 2);
    await view.pager.first();
    assert.equal(view.pager.page(), 1);
    await view.pager.goTo({ number: 500 });
    assert.equal(view.pager.page(), 3);
    await view.pager.goTo({ number: -1 });
    assert.equal(view.pager.page(), 1);
});

test("search submission and hide-read changes reset paging while refresh retains applied filters", async () => {
    const records = inboxRecords(23);
    records[0].readBy = "admin";
    const { view, calls } = inboxFixture(records);
    await view.loadRequests();
    await view.pager.next();
    view.search("  General  ");
    await view.searchRequests();
    assert.equal(view.pager.page(), 1);
    assert.equal(calls.at(-1).search, "General");
    await view.pager.next();
    view.search("unsubmitted draft");
    await view.refresh();
    assert.equal(view.pager.page(), 2);
    assert.equal(calls.at(-1).search, "General");
    view.search("General");
    view.hideRead(true);
    await view.searchRequests();
    assert.equal(view.pager.page(), 1);
    assert.equal(view.pager.total(), 22);
    assert.equal(view.requests()[0].id, "request-2");
    assert.equal(calls.at(-1).unreadOnly, true);
    await view.pager.next();
    view.hideRead(false);
    await view.searchRequests();
    assert.equal(view.pager.page(), 1);
    assert.equal(view.pager.total(), 23);
    view.search("");
    await view.searchRequests();
    assert.equal(calls.at(-1).search, "");
});

test("deleted toggle resets inbox paging from either section without losing search or hide-read", async () => {
    const records = inboxRecords(23);
    records.push({ ...records[0], id: "deleted", isDeleted: true });
    const { view, api, calls, details } = inboxFixture(records);
    const forms = [];
    api.manageForms = async deleted => { forms.push(deleted); return { ok: true, data: [] }; };
    view.search("General");
    view.hideRead(true);
    await view.searchRequests();
    await view.pager.next();
    await view.selectRequest(view.requests()[0]);
    view.showDeleted(true);
    await view.deletedChanged();
    assert.equal(view.pager.page(), 1);
    assert.equal(view.requestDetails(), null);
    assert.equal(view.requests()[0].id, "deleted");
    assert.deepEqual(calls.at(-1), { page: 1, pageSize: 10, search: "General", unreadOnly: true, deleted: true });
    await view.selectRequest(view.requests()[0]);
    assert.equal(details.at(-1).deleted, true);
    await view.switchSection("forms");
    view.pager.page(3);
    view.showDeleted(false);
    await view.deletedChanged();
    assert.equal(view.pager.page(), 1);
    assert.equal(view.requestDetails(), null);
    assert.deepEqual(forms, [true, false]);
    await view.switchSection("requests");
    assert.deepEqual(calls.at(-1), { page: 1, pageSize: 10, search: "General", unreadOnly: true, deleted: false });
});

for (const action of ["removeRequest", "restoreRequest", "hardDeleteRequest", "setRead"]) {
    test(`${action} recovers when the final request disappears from the last page`, async () => {
        const records = inboxRecords(11);
        const deleted = action === "restoreRequest" || action === "hardDeleteRequest";
        records.forEach(item => { item.isDeleted = deleted; });
        const { view, calls, details } = inboxFixture(records);
        view.showDeleted(deleted);
        view.hideRead(action === "setRead");
        await view.loadRequests();
        await view.pager.last();
        await view.selectRequest(view.requests()[0]);
        const detailCount = details.length;
        await view[action](true);
        assert.equal(view.pager.page(), 1);
        assert.equal(view.pager.pageCount(), 1);
        assert.equal(view.pager.total(), 10);
        assert.equal(view.requests().length, 10);
        assert.equal(view.requestDetails(), null);
        assert.equal(details.length, detailCount);
        assert.equal(calls.slice(-2).map(query => query.page).join(","), "2,1");
        assert.equal(view.busy(), false);
    });
}

test("paging recovery reaches a consistent empty first page when all requests disappear", async () => {
    const records = inboxRecords(11);
    const { view, calls } = inboxFixture(records);
    await view.loadRequests();
    await view.pager.last();
    records.splice(0);
    await view.refresh();
    assert.equal(calls.slice(-2).map(query => query.page).join(","), "2,1");
    assert.equal(view.pager.page(), 1);
    assert.equal(view.pager.pageCount(), 0);
    assert.equal(view.pager.total(), 0);
    assert.equal(view.pager.hasItems(), false);
    assert.equal(view.pager.hasNext(), false);
    assert.equal(view.pager.hasPrevious(), false);
    assert.equal(view.requests().length, 0);
});

test("refresh updates selected details once and clears selection when paging or filtering removes it", async () => {
    const records = inboxRecords(11);
    const { view, details } = inboxFixture(records);
    await view.loadRequests();
    await view.selectRequest(view.requests()[0]);
    records[0].message = "Changed by another administrator";
    await view.refresh();
    assert.equal(view.requestDetails().message, records[0].message);
    assert.equal(details.length, 2);
    await view.setRead(true);
    assert.equal(view.requestDetails().readBy, "admin");
    assert.equal(details.length, 3);
    await view.retryRequest();
    assert.equal(view.requestDetails().deliveryStatus, "sent");
    assert.equal(details.length, 4);
    await view.pager.next();
    assert.equal(view.requestDetails(), null);
    assert.equal(details.length, 4);
    await view.selectRequest(view.requests()[0]);
    view.search("no match");
    await view.searchRequests();
    assert.equal(view.requestDetails(), null);
    assert.equal(view.requests().length, 0);
});

test("stale detail responses cannot replace a newer selection or restore filtered-out details", async () => {
    const records = inboxRecords(2);
    const { view, api } = inboxFixture(records);
    await view.loadRequests();
    let resolveOld;
    api.request = id => id === records[0].id
        ? new Promise(resolve => { resolveOld = resolve; })
        : Promise.resolve({ ok: true, data: records[1] });
    const old = view.openRequest(records[0].id);
    await view.openRequest(records[1].id);
    resolveOld({ ok: false, message: "stale failure" });
    await old;
    assert.equal(view.requestDetails().id, records[1].id);
    assert.equal(view.message(), "");
    const removed = view.openRequest(records[0].id);
    view.search("no match");
    await view.searchRequests();
    resolveOld({ ok: true, data: records[0] });
    await removed;
    assert.equal(view.requestDetails(), null);
});

test("detail failures and list refresh failures clear selection and release busy state", async () => {
    const records = inboxRecords(1);
    const { view, api } = inboxFixture(records);
    await view.loadRequests();
    await view.selectRequest(records[0]);
    api.request = async () => ({ ok: false, message: "detail missing" });
    await view.refresh();
    assert.equal(view.requestDetails(), null);
    assert.equal(view.message(), "detail missing");
    api.request = async () => ({ ok: true, data: records[0] });
    await view.selectRequest(records[0]);
    api.manageRequests = async () => ({ ok: false, message: "read denied" });
    await view.refresh();
    assert.equal(view.requestDetails(), null);
    assert.equal(view.message(), "read denied");
    await view.selectRequest(records[0]);
    api.manageRequests = async () => { throw new Error("offline"); };
    await view.refresh();
    assert.equal(view.requestDetails(), null);
    assert.equal(view.message(), "offline");
    assert.equal(view.busy(), false);
});

test("busy inbox ignores paging and duplicate refreshes without changing requested page", async () => {
    const { view, api, calls } = inboxFixture(inboxRecords(21));
    await view.loadRequests();
    let resolve;
    api.manageRequests = () => new Promise(done => { resolve = done; });
    const loading = view.refresh();
    await view.pager.next();
    await view.pager.last();
    await view.pager.goTo({ number: 2 });
    await view.refresh();
    assert.equal(view.pager.page(), 1);
    assert.equal(calls.length, 1);
    resolve({ ok: true, data: requestPage(inboxRecords(10), 1, 21) });
    await loading;
    assert.equal(view.busy(), false);
    assert.equal(view.pager.page(), 1);
});

test("without read permission the inbox clears state and never calls its API", async () => {
    const { view, calls } = inboxFixture(inboxRecords(1), []);
    view.requests(inboxRecords(1));
    view.requestDetails({ id: "old" });
    view.pager.update({ page: 3, pageCount: 3, total: 21 });
    await view.loadRequests();
    assert.equal(calls.length, 0);
    assert.equal(view.requests().length, 0);
    assert.equal(view.requestDetails(), null);
    assert.equal(view.pager.page(), 1);
    assert.equal(view.pager.total(), 0);
});

test("inbox template exposes localized search, opt-in hide-read and the shared pager", () => {
    const template = fs.readFileSync(path.join(contact, "views/contactmanager.html"), "utf8");
    assert.match(template, /submit: searchRequests/);
    assert.match(template, /checked: hideRead/);
    assert.match(template, /change: searchRequests/);
    assert.match(template, /change: deletedChanged/);
    assert.match(template, /'aria-label': 'contact\.manager\.searchLabel'/);
    assert.match(template, /<pager[^>]+params="pager: pager"/);
    assert.match(template, /<fieldset[^>]+data-bind="disable: busy"/);
    assert.match(template, /t: 'common.search'/);
});

test("inbox list and request detail preserve creation timestamps and use localized dateTime formatting", async () => {
    const createdAt = "2026-11-13T10:39:07.810Z";
    const records = inboxRecords(1);
    records[0].createdAt = createdAt;
    const { view } = inboxFixture(records);
    await view.loadRequests();
    await view.selectRequest(view.requests()[0]);
    assert.equal(view.requests()[0].createdAt, createdAt);
    assert.equal(view.requestDetails().createdAt, createdAt);

    for (const locale of ["en-US", "hu-HU"]) {
        const calls = [];
        const formatted = new Date(createdAt).toLocaleString(locale);
        const localizedView = managerViewModel({}, undefined, {
            t: key => key,
            dateTime: value => {
                calls.push(value);
                return new Date(value).toLocaleString(locale);
            }
        });
        assert.equal(localizedView.createdAtLabel(view.requests()[0].createdAt), formatted);
        assert.equal(localizedView.createdAtLabel(view.requestDetails().createdAt), formatted);
        assert.deepEqual(calls, [createdAt, createdAt]);
    }
});

test("missing and empty creation timestamps show an explicit localized missing-audit fallback", () => {
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(fs.readFileSync(path.join(contact, "locales", locale + ".json"), "utf8")).messages;
        const view = managerViewModel({}, undefined, {
            t: key => messages[key],
            dateTime: () => assert.fail("Missing timestamps must not be formatted as dates")
        });
        assert.ok(messages["contact.manager.createdAt"]);
        assert.ok(messages["contact.manager.creationAuditMissing"]);
        for (const value of [null, undefined, ""]) {
            assert.equal(view.createdAtLabel(value), messages["contact.manager.creationAuditMissing"]);
        }
    }
});

test("inbox list and detail render labeled semantic times only when a creation timestamp exists", () => {
    const template = fs.readFileSync(path.join(contact, "views/contactmanager.html"), "utf8");
    const list = template.slice(template.indexOf('data-bind="foreach: requests"'), template.indexOf('<fieldset class="mt-3"'));
    const detail = template.slice(template.indexOf('data-bind="with: requestDetails'));
    for (const section of [list, detail]) {
        assert.match(section, /t: 'contact\.manager\.createdAt'/);
        assert.match(section, /<!-- ko if: \$data\.createdAt -->\s*<time data-bind="attr: \{ datetime: \$data\.createdAt \}, text: \$root\.createdAtLabel\(\$data\.createdAt\)"><\/time>\s*<!-- \/ko -->/);
        assert.match(section, /<!-- ko ifnot: \$data\.createdAt -->\s*<span data-bind="text: \$root\.createdAtLabel\(\$data\.createdAt\)"><\/span>\s*<!-- \/ko -->/);
        assert.doesNotMatch(section, /html:.*createdAt/);
    }
});

test("widget descriptor model registers a component and returns the generic mount payload", () => {
    const registrations = [];
    const components = {
        isRegistered: () => false,
        register: (name, definition) => registrations.push({ name, definition })
    };
    const model = load(path.join(contact, "models/contact-widget.js"), {
        ko: { components },
        "text!modules/contact/views/contactwidget.html": "<form></form>",
        "modules/contact/viewmodels/contact": function () {}
    });
    assert.equal(model.load("contact.form", 5, {}).component.name, "contact-form");
    assert.equal(registrations[0].name, "contact-form");
});

test("page and nested widget fields bind to their contact view model", () => {
    for (const file of ["contact.html", "contactwidget.html"]) {
        const template = fs.readFileSync(path.join(contact, "views", file), "utf8");
        assert.match(template, /foreach: fields/);
        assert.match(template, /for: \$parent\.idPrefix \+ '-field-' \+ key/);
        assert.equal((template.match(/id: \$parent\.idPrefix \+ '-field-' \+ key/g) || []).length, 2);
        assert.equal((template.match(/enable: !\$parent\.busy\(\)/g) || []).length, 2);
        assert.doesNotMatch(template, /\$root\.(?:idPrefix|fields|busy)/);
    }
});

test("registered widget activates and disposes its actual public view model", async () => {
    let registration;
    let resolveForms;
    const forms = new Promise(resolve => { resolveForms = resolve; });
    const ViewModel = load(path.join(contact, "viewmodels/contact.js"), {
        ko, "models/i18n": i18n,
        "modules/contact/models/contact": { forms: () => forms }
    });
    load(path.join(contact, "models/contact-widget.js"), {
        ko: { components: {
            isRegistered: () => false,
            register: (name, definition) => { registration = definition; }
        } },
        "text!modules/contact/views/contactwidget.html": "<form></form>",
        "modules/contact/viewmodels/contact": ViewModel
    });
    const widget = new registration.viewModel();
    assert.equal(widget.busy(), true);
    widget.dispose();
    resolveForms({ ok: true, data: [form("one")] });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(widget.forms().length, 0);
    assert.equal(widget.form(), null);
});

test("inbox-only forms save an empty recipient and never offer or perform retry", async () => {
    const calls = [];
    const api = {
        manageForms: async () => ({ ok: true, data: [] }),
        saveForm: async (id, fields) => { calls.push({ action: "save", fields }); return { ok: true, data: form("inbox") }; },
        retryRequest: async id => { calls.push({ action: "retry", id }); return { ok: true }; }
    };
    const view = managerViewModel(api);
    await view.activate();
    view.formName("Inbox");
    view.recipient("  ");
    view.subjectMode("freeText");
    await view.saveForm();
    assert.equal(calls[0].fields.recipient, "");
    assert.equal(calls[0].fields.subjectMode, "freeText");
    assert.equal(calls[0].fields.subjects.length, 0);
    assert.equal(view.recipientLabel(""), "contact.manager.inboxOnly");
    assert.equal(view.recipientLabel("team@example.test"), "team@example.test");
    assert.equal(view.statusLabel("notRequested"), "contact.deliveryStatus.notRequested");
    assert.equal(view.statusClass("notRequested"), "text-bg-secondary");
    for (const [deliveryStatus, isDeleted, expected] of [["failed", false, true], ["notRequested", false, false], ["sent", false, false], ["sending", false, false], ["failed", true, false]]) {
        assert.equal(view.canRetry({ deliveryStatus, isDeleted }), expected);
    }
    view.requestDetails({ id: "request-2", deliveryStatus: "notRequested", isDeleted: false, recipient: "" });
    await view.retryRequest();
    assert.equal(calls.some(call => call.action === "retry"), false);
});
