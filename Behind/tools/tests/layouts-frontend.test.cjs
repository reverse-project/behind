const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const { randomUUID } = require("node:crypto");

const root = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(root, "libraries/knockout.min.js"));

function load(file, dependencies, extra = {}) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(root, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => dependencies[name]));
        },
        window: { crypto: { randomUUID }, confirm: () => true },
        console,
        ...extra
    });
    return result;
}

function configuration() {
    return {
        revision: 4, defaultLayout: "default",
        layouts: [{
            id: "default", name: "Default", deleted: false,
            sections: [{ rows: 1, columns: 1, widgets: [{ id: "content", type: "core.content" }] }]
        }],
        assignments: []
    };
}

test("layout selection prefers exact routes then longest prefixes and falls back to default", () => {
    const model = load("models/layouts.js", {});
    const data = configuration();
    data.layouts.push({ id: "news" }, { id: "article" }, { id: "exact" });
    data.assignments = [
        { route: "#/*", layout: "news" },
        { route: "#/news/*", layout: "article" },
        { route: "#/news/item", layout: "exact" }
    ];
    assert.equal(model.select(data, "#/news/item").id, "exact");
    assert.equal(model.select(data, "#/news/other").id, "article");
    assert.equal(model.select(data, "#/articles").id, "news");
    data.assignments = [];
    assert.equal(model.select(data, "#/news").id, "default");
});

test("layout load exposes errors rather than replacing failed configuration with success", async () => {
    const model = load("models/layouts.js", { "models/http": { get: async () => ({ ok: false }) } });
    await assert.rejects(model.load(), /layouts.error.load/);
    assert.equal(model.current(), null);
});

test("saving uses the saved revision and invalidates without an extra fetch", async () => {
    const calls = [];
    const data = configuration();
    const model = load("models/layouts.js", {
        "models/http": {
            post: async () => ({ ok: true, data }),
            get: async () => { calls.push("load"); return { ok: true, data }; }
        },
        "models/invalidation": { invalidate: async topic => calls.push(topic) }
    });
    assert.equal((await model.save(data)).ok, true);
    assert.deepEqual(calls, ["layouts"]);
    assert.equal(model.current().revision, data.revision);
    assert.equal(model.current().layouts[0], data.layouts[0]);
});

test("a stale public read cannot overwrite a newer successful save", async () => {
    let finishRead;
    const data = configuration();
    const model = load("models/layouts.js", {
        "models/http": {
            get: () => new Promise(resolve => { finishRead = resolve; }),
            post: async () => ({ ok: true, data: { ...data, revision: 5 } })
        },
        "models/invalidation": { invalidate: async () => {} }
    });
    const reading = model.load();
    await model.save(data);
    finishRead({ ok: true, data });
    await reading;
    assert.equal(model.current().revision, 5);
});

function editor(data = configuration(), save = async body => ({ ok: true, data: body }), grants = () => true) {
    const Model = load("viewmodels/layouts.js", {
        ko, "models/layouts": { manage: async () => ({ ok: true, data }), save },
        "models/widgets": { catalog: () => [{ key: "core.content", name: "widgets.content" }] },
        "models/session": { hasGrant: grants },
        "models/i18n": { t: key => key },
        "bindings/richeditor": {}
    });
    return new Model();
}

test("new layouts have one content widget and copy assigns fresh ids", async () => {
    const view = editor();
    await view.activate();
    assert.equal(view.widgetName("core.content"), "widgets.content");
    view.addLayout();
    const original = view.selected();
    assert.equal(original.sections()[0].widgets()[0].type(), "core.content");
    view.cloneLayout();
    const copy = view.selected();
    assert.notEqual(copy.id, original.id);
    assert.notEqual(copy.sections()[0].widgets()[0].id, original.sections()[0].widgets()[0].id);
    assert.equal(copy.deleted(), false);
});

test("adding widgets avoids occupied spans and grows rows only when needed", async () => {
    const view = editor();
    await view.activate();
    const section = view.selected().sections()[0];
    section.columns(3);
    section.rows(2);
    section.widgets()[0].columnSpan(2);
    section.widgets()[0].rowSpan(2);
    view.addWidget(section);
    assert.equal(section.widgets()[1].column(), 3);
    assert.equal(section.widgets()[1].row(), 1);
    view.addWidget(section);
    assert.equal(section.widgets()[2].column(), 3);
    assert.equal(section.widgets()[2].row(), 2);
    view.addWidget(section);
    assert.equal(section.rows(), 3);
    assert.equal(section.widgets()[3].row(), 3);
});

test("a full maximum-size section reports an explicit error", async () => {
    const view = editor();
    await view.activate();
    const section = view.selected().sections()[0];
    section.rows(12);
    section.widgets()[0].rowSpan(12);
    view.addWidget(section);
    assert.equal(section.widgets().length, 1);
    assert.equal(view.message(), "layouts.error.full");
});

test("layout save preserves revision and serializes numeric form inputs as numbers", async () => {
    let saved;
    const view = editor(configuration(), async body => {
        saved = body;
        return { ok: true, data: { ...body, revision: 5 } };
    });
    await view.activate();
    const section = view.selected().sections()[0];
    section.rows("2");
    section.columns("3");
    section.widgets()[0].columnSpan("2");
    await view.save();
    assert.equal(saved.revision, 4);
    assert.equal(saved.layouts[0].sections[0].rows, 2);
    assert.equal(saved.layouts[0].sections[0].widgets[0].columnSpan, 2);
    assert.equal(view.message(), "layouts.saved");
    assert.equal(view.busy(), false);
});

test("deleting an assigned layout removes assignments but cannot delete the default", async () => {
    const view = editor();
    await view.activate();
    view.deleteLayout();
    assert.equal(view.selected().deleted(), false);
    assert.equal(view.message(), "layouts.error.default");
    view.addLayout();
    const added = view.selected();
    view.addAssignment();
    view.assignments()[0].layout(added.id);
    view.deleteLayout();
    assert.equal(added.deleted(), true);
    assert.equal(view.assignments().length, 0);
    assert.equal(view.selected().id, "default");
});

test("conflict does not reset unsaved editor state", async () => {
    const view = editor(configuration(), async () => ({ ok: false, status: 409 }));
    await view.activate();
    view.selected().name("Unsaved");
    await view.save();
    assert.equal(view.selected().name(), "Unsaved");
    assert.equal(view.message(), "layouts.error.conflict");
    assert.equal(view.busy(), false);
});

test("read-only grant state prevents writes and shows denied reads explicitly", async () => {
    let writes = 0;
    const view = editor(configuration(), async () => { writes++; }, () => false);
    await view.activate();
    await view.save();
    assert.equal(writes, 0);
    assert.equal(view.message(), "layouts.error.denied");
});

class Element {
    constructor(name) {
        this.name = name;
        this.children = [];
        this.style = {};
        this.classList = { add() {}, remove() {} };
    }
    appendChild(child) {
        child.remove();
        this.children.push(child);
        child.parentNode = this;
    }
    insertBefore(child, reference) {
        child.remove();
        this.children.splice(this.children.indexOf(reference), 0, child);
        child.parentNode = this;
    }
    remove() {
        if (this.parentNode) {
            this.parentNode.children.splice(this.parentNode.children.indexOf(this), 1);
            this.parentNode = null;
        }
    }
}

test("shell moves but never rebinds content and disposes widget regions during invalidation", async () => {
    const body = new Element("body");
    const main = new Element("main");
    main.unsavedInput = "keep me";
    body.appendChild(main);
    const cleaned = [];
    const mounted = [];
    const subscriptions = {};
    const data = configuration();
    data.layouts[0].sections[0].columns = 2;
    data.layouts[0].sections[0].widgets.push({ id: "news", type: "core.news", row: 1, column: 2, rowSpan: 1, columnSpan: 1 });
    const shell = load("models/layoutshell.js", {
        ko: { cleanNode: node => cleaned.push(node), applyBindingsToNode: node => mounted.push(node) },
        "models/layouts": { current: () => data, load: async () => data, select: () => data.layouts[0] },
        "models/invalidation": { subscribe: (topic, handler) => { subscriptions[topic] = handler; }, invalidate: async () => {} },
        "models/session": { accessToken: ko.observable("") },
        "models/restriction": { restricted: () => false },
        "models/i18n": { t: key => key }
    }, { document: { getElementById: () => main, createElement: name => new Element(name) } });
    await shell.start();
    assert.equal(mounted.length, 1);
    assert.notEqual(main.parentNode, body);
    subscriptions.layouts();
    assert.equal(mounted.length, 2);
    assert.equal(cleaned.length, 1);
    assert.ok(!cleaned.includes(main));
    assert.equal(main.unsavedInput, "keep me");
    shell.route("#/manage/layouts");
    assert.equal(main.parentNode, body);
    assert.equal(cleaned.length, 2);
    assert.equal(body.children.length, 1);
});
