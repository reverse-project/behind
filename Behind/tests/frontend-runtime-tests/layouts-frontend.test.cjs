const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const { randomUUID } = require("node:crypto");

const root = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(root, "libraries/knockout.min.js"));

function load(file, dependencies, extra = {}) {
    dependencies["models/contentlocale"] ||= {
        current: ko.observable(null),
        loadFailed: ko.observable(false),
        locales: ko.observableArray([]),
        clearMedia() {}
    };
    if (file === "models/layouts.js") {
        dependencies["models/results"] = load("models/results.js", {
            "models/i18n": { tOr: key => key }
        });
    }
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(root, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => dependencies[name]));
        },
        window: { crypto: { randomUUID }, confirm: () => true },
        console,
        FormData,
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

function editor(data = configuration(), save = async body => ({ ok: true, data: body }), grants = () => true,
    manageMessages = async () => ({ ok: true, data: { messages: { page: 1, pageCount: 1, items: [] } } }),
    uploadImage = async () => ({ ok: true, data: { url: "/layouts/images/image" } })) {
    const Model = load("viewmodels/layouts.js", {
        ko, "models/layouts": { manage: async () => ({ ok: true, data }), save, uploadImage },
        "models/messages": { manage: manageMessages },
        "models/widgets": { catalog: () => [
            { key: "core.content", name: "widgets.content" },
            { key: "core.header", name: "widgets.header" },
            { key: "core.footer", name: "widgets.footer" }
        ] },
        "models/session": { hasGrant: grants },
        "models/i18n": { t: key => key },
        "bindings/richeditor": {}
    });
    return new Model();
}

test("custom widget pictures upload as multipart files to the layout image endpoint", async () => {
    const picture = new Blob(["picture"], { type: "image/png" });
    const model = load("models/layouts.js", {
        "models/http": { postForm: async (url, form) => {
            assert.equal(url, "/layouts/images");
            assert.equal(await form.get("file").text(), "picture");
            return { ok: true, data: { url: "/layouts/images/one" } };
        } }
    });
    assert.equal((await model.uploadImage(picture)).data.url, "/layouts/images/one");
});

test("picture upload failures use the shared localized image errors", async () => {
    for (const [status, message] of [
        [400, "content.error.imageRequired"], [401, "common.error.sessionExpired"],
        [403, "content.error.imageForbidden"], [413, "content.error.imageTooLarge"],
        [415, "content.error.imageType"], [500, "common.error.generic"]
    ]) {
        const model = load("models/layouts.js", {
            "models/http": { postForm: async () => ({ ok: false, status }) }
        });
        const result = await model.uploadImage(new Blob(["picture"]));
        assert.equal(result.ok, false);
        assert.equal(result.message, message);
    }
});

test("the layout editor exposes uploaded picture URLs and blocks saves during upload", async () => {
    let finish;
    const view = editor(undefined, undefined, undefined, undefined,
        () => new Promise(resolve => { finish = resolve; }));
    const pending = view.uploadImage(new Blob(["picture"]));
    assert.equal(view.busy(), true);
    assert.equal(view.canWrite(), false);
    finish({ ok: true, data: { url: "/layouts/images/one" } });
    assert.equal(await pending, "/layouts/images/one");
    assert.equal(view.busy(), false);
    assert.equal(view.canWrite(), true);
});

test("the layout editor reports rejected and failed uploads without inserting a picture", async () => {
    for (const upload of [
        async () => ({ ok: false, message: "content.error.imageType" }),
        async () => { throw new Error("network unavailable"); }
    ]) {
        const view = editor(undefined, undefined, undefined, undefined, upload);
        assert.equal(await view.uploadImage(new Blob(["picture"])), null);
        assert.ok(view.message());
        assert.equal(view.busy(), false);
    }
});

test("new layouts have optional full-width chrome around one content widget", async () => {
    const view = editor();
    await view.activate();
    assert.equal(view.widgetName("core.content"), "widgets.content");
    assert.equal(view.widgetName("core.header"), "widgets.header");
    assert.equal(view.widgetName("core.footer"), "widgets.footer");
    view.addLayout();
    const original = view.selected();
    assert.equal(view.schemaVersion(), 1);
    assert.equal(original.sections().length, 3);
    assert.equal(original.sections().map(section => section.width()).join(","), "full,site,full");
    assert.equal(original.sections().map(section => section.widgets()[0].type()).join(","), "core.header,core.content,core.footer");
    original.sections()[0].widgets.remove(original.sections()[0].widgets()[0]);
    original.sections()[2].widgets.remove(original.sections()[2].widgets()[0]);
    assert.equal(original.sections()[1].widgets()[0].type(), "core.content");
    view.cloneLayout();
    const copy = view.selected();
    assert.notEqual(copy.id, original.id);
    assert.equal(copy.deleted(), false);
});

test("css classes and schema version load, clone, and round-trip to save requests", async () => {
    let saved;
    const data = configuration();
    data.schemaVersion = 1;
    data.layouts[0].sections[0].cssClass = " section-shell ";
    data.layouts[0].sections[0].widgets[0].cssClass = " content-card ";
    const view = editor(data, async body => {
        saved = body;
        return { ok: true, data: { ...body, revision: 5 } };
    });
    await view.activate();
    assert.equal(view.schemaVersion(), 1);
    assert.equal(view.selected().sections()[0].cssClass(), " section-shell ");
    assert.equal(view.selected().sections()[0].widgets()[0].cssClass(), " content-card ");
    view.cloneLayout();
    const copy = view.selected();
    assert.equal(view.schemaVersion(), 1);
    assert.equal(copy.sections()[0].cssClass(), " section-shell ");
    assert.equal(copy.sections()[0].widgets()[0].cssClass(), " content-card ");
    copy.sections()[0].cssClass(" section-shell ");
    copy.sections()[0].widgets()[0].cssClass(" content-card ");
    await view.save();
    const request = saved.layouts.find(layout => layout.id === copy.id);
    assert.equal(saved.schemaVersion, 1);
    assert.equal(request.sections[0].cssClass, "section-shell");
    assert.equal(request.sections[0].widgets[0].cssClass, "content-card");
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

test("badges load clone upload save and remove independently of sections", async () => {
    let saved;
    const data = configuration();
    data.layouts[0].badges = [{ id: "badge", text: "Funding", image: "", link: "https://example.org", position: "bottom-right", width: 240 }];
    const view = editor(data, async body => {
        saved = body;
        return { ok: true, data: { ...body, revision: 5 } };
    });
    await view.activate();
    assert.equal(view.selected().badges()[0].text(), "Funding");
    view.cloneLayout();
    const copyId = view.selected().id;
    assert.notEqual(view.selected().badges()[0].id, "badge");
    assert.equal(view.selected().badges()[0].position(), "bottom-right");
    view.addBadge();
    const badge = view.selected().badges()[1];
    assert.equal(badge.position(), "top-right");
    assert.equal(badge.width(), 180);
    badge.text(" Under development ");
    badge.width("200");
    view.uploadImage = async () => "/layouts/images/0a1bc242-6328-4021-959c-96793c556da7";
    const input = { files: [{}], value: "picture.png" };
    await view.uploadBadge(badge, { target: input });
    assert.equal(input.value, "");
    assert.equal(badge.image(), "/layouts/images/0a1bc242-6328-4021-959c-96793c556da7");
    view.uploadImage = async () => null;
    await view.uploadBadge(badge, { target: { files: [{}], value: "failed.png" } });
    assert.equal(badge.image(), "/layouts/images/0a1bc242-6328-4021-959c-96793c556da7");
    await view.save();
    const request = saved.layouts.find(layout => layout.id === copyId);
    assert.equal(request.badges[1].text, "Under development");
    assert.equal(request.badges[1].width, 200);
    assert.equal(request.badges[1].image, badge.image());
    assert.equal(request.sections.length, data.layouts[0].sections.length);
    view.removeBadge(view.selected().badges()[0]);
    assert.equal(view.selected().badges().length, 1);
    view.addLayout();
    assert.equal(view.selected().badges().length, 0);
    for (let index = 0; index < 21; index++) {
        view.addBadge();
    }
    assert.equal(view.selected().badges().length, 20);
    assert.equal(view.message(), "layouts.error.badgeLimit");
});

test("cell appearance options default off and survive load clone save and reset", async () => {
    let saved;
    const data = configuration();
    const original = data.layouts[0].sections[0].widgets[0];
    original.borderless = true;
    original.noMargin = true;
    original.noPadding = true;
    original.hamburgerPosition = "left";
    const view = editor(data, async body => {
        saved = body;
        return { ok: true, data: { ...body, revision: 5 } };
    });
    await view.activate();
    const loaded = view.selected().sections()[0].widgets()[0];
    assert.equal(loaded.borderless(), true);
    assert.equal(loaded.noMargin(), true);
    assert.equal(loaded.noPadding(), true);
    assert.equal(loaded.hamburgerPosition(), "left");
    view.cloneLayout();
    const copy = view.selected();
    const widget = copy.sections()[0].widgets()[0];
    assert.equal(widget.borderless(), true);
    assert.equal(widget.noMargin(), true);
    assert.equal(widget.noPadding(), true);
    assert.equal(widget.hamburgerPosition(), "left");
    await view.save();
    const placement = () => saved.layouts.find(layout => layout.id === copy.id).sections[0].widgets[0];
    assert.equal(placement().borderless, true);
    assert.equal(placement().noMargin, true);
    assert.equal(placement().noPadding, true);
    assert.equal(placement().hamburgerPosition, "left");
    const reloaded = view.selected().sections()[0].widgets()[0];
    reloaded.borderless(false);
    reloaded.noMargin(false);
    reloaded.noPadding(false);
    reloaded.hamburgerPosition("right");
    await view.save();
    assert.equal(placement().borderless, false);
    assert.equal(placement().noMargin, false);
    assert.equal(placement().noPadding, false);
    assert.equal(placement().hamburgerPosition, "right");
    view.addLayout();
    view.selected().sections().forEach(section => {
        const fresh = section.widgets()[0];
        assert.equal(fresh.borderless(), false);
        assert.equal(fresh.noMargin(), false);
        assert.equal(fresh.noPadding(), false);
        assert.equal(fresh.hamburgerPosition(), "default");
    });
});

test("a full grid allows another widget to overlap and enforces the widget limit", async () => {
    const view = editor();
    await view.activate();
    const section = view.selected().sections()[0];
    section.rows(12);
    section.widgets()[0].rowSpan(12);
    view.addWidget(section);
    assert.equal(section.widgets().length, 2);
    assert.equal(section.widgets()[1].row(), 1);
    assert.equal(section.widgets()[1].column(), 1);
    while (section.widgets().length < 100) {
        view.addWidget(section);
    }
    view.addWidget(section);
    assert.equal(section.widgets().length, 100);
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
    section.width("full");
    section.overlap("42");
    section.layer("-2");
    section.widgets()[0].columnSpan("2");
    section.widgets()[0].layer("3");
    await view.save();
    assert.equal(saved.revision, 4);
    assert.equal(saved.layouts[0].sections[0].rows, 2);
    assert.equal(saved.layouts[0].sections[0].width, "full");
    assert.equal(saved.layouts[0].sections[0].overlap, 42);
    assert.equal(saved.layouts[0].sections[0].layer, -2);
    assert.equal(saved.layouts[0].sections[0].widgets[0].columnSpan, 2);
    assert.equal(saved.layouts[0].sections[0].widgets[0].layer, 3);
    assert.equal(view.selected().sections()[0].overlap(), 42);
    assert.equal(view.message(), "layouts.saved");
    assert.equal(view.busy(), false);
});

test("the message picker loads every page and preserves a selected message id", async () => {
    const pages = [];
    const data = configuration();
    data.layouts[0].sections[0].widgets.push({ id: "banner", type: "core.message", message: "second" });
    const view = editor(data, undefined, undefined, async query => {
        pages.push(query.page);
        assert.equal(query.pageSize, 50);
        return { ok: true, data: { messages: {
            page: query.page, pageCount: 2, items: [{ id: query.page === 1 ? "first" : "second", name: "Notice" }]
        } } };
    });
    await view.activate();
    assert.deepEqual(pages, [1, 2]);
    assert.equal(view.messages().length, 2);
    assert.equal(view.selected().sections()[0].widgets()[1].message(), "second");
});

test("the message picker surfaces failures without losing layout selections", async () => {
    const view = editor(configuration(), undefined, undefined, async () => ({ ok: false, message: "Read denied" }));
    await view.activate();
    assert.equal(view.messageLoadError(), "Read denied");
    assert.equal(view.selected().id, "default");
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
        this.parentNode = null;
        this.style = {};
        this.classes = new Set();
        this.attributes = {};
        this.classList = {
            add: (...names) => names.forEach(value => this.classes.add(value)),
            remove: name => this.classes.delete(name),
            contains: name => this.classes.has(name)
        };
    }
    get className() {
        return Array.from(this.classes).join(" ");
    }
    set className(value) {
        this.classes = new Set(value.split(/\s+/).filter(Boolean));
    }
    get nextSibling() {
        if (!this.parentNode) {
            return null;
        }
        return this.parentNode.children[this.parentNode.children.indexOf(this) + 1] || null;
    }
    appendChild(child) {
        child.remove();
        this.children.push(child);
        child.parentNode = this;
    }
    insertBefore(child, reference) {
        child.remove();
        const index = reference ? this.children.indexOf(reference) : this.children.length;
        this.children.splice(index, 0, child);
        child.parentNode = this;
    }
    setAttribute(name, value) {
        this.attributes[name] = value;
    }
    remove() {
        if (this.parentNode) {
            this.parentNode.children.splice(this.parentNode.children.indexOf(this), 1);
            this.parentNode = null;
        }
    }
}

function shellDocument(body, main) {
    const header = new Element("header");
    const footer = new Element("footer");
    body.appendChild(header);
    body.appendChild(main);
    body.appendChild(footer);
    const elements = {
        "application-shell": body,
        "header-container": header,
        "main-container": main,
        "footer-container": footer
    };
    return {
        getElementById: id => elements[id],
        createElement: name => new Element(name),
        createComment: name => new Element("#" + name)
    };
}

const chromeComponents = {
    "components/siteheader": { mount() {} },
    "components/sitefooter": { mount() {} }
};

test("shell renders overlapping widgets and section offsets with independent layers", async () => {
    const body = new Element("body");
    const main = new Element("main");
    const document = shellDocument(body, main);
    const data = configuration();
    data.layouts[0].sections = [
        { rows: 1, columns: 2, width: "site", overlap: 25, layer: -2, widgets: [
            { id: "content", type: "core.content", row: 1, column: 1, rowSpan: 1, columnSpan: 2, layer: 5 },
            { id: "news", type: "core.news", row: 1, column: 2, rowSpan: 1, columnSpan: 1, layer: -1 }
        ] },
        { rows: 1, columns: 1, width: "full", overlap: 40, layer: 3, widgets: [] }
    ];
    const shell = load("models/layoutshell.js", {
        ko: { cleanNode() {}, applyBindingsToNode() {} },
        "models/layouts": { current: () => data, load: async () => data, select: () => data.layouts[0] },
        "models/invalidation": { subscribe() {} },
        "models/restriction": { restricted: () => false },
        "models/i18n": { t: key => key },
        ...chromeComponents
    }, { document });
    await shell.start();
    const host = body.children.find(element => element.classList.contains("behind-layout"));
    const [first, second] = host.children;
    const grid = first.children[0];
    assert.equal(first.style.marginTop, "0px");
    assert.equal(first.style.zIndex, -2);
    assert.equal(second.style.marginTop, "-40px");
    assert.equal(second.style.zIndex, 3);
    assert.ok(grid.classes.has("behind-layout-section-overlapping"));
    assert.equal(grid.children[0].style.zIndex, 5);
    assert.equal(grid.children[1].style.zIndex, -1);
    assert.equal(grid.children[1].style.gridColumn, "2 / span 1");
    assert.ok(!second.children[0].classes.has("behind-layout-section-overlapping"));
});

test("shell moves but never rebinds content and disposes widget regions during invalidation", async () => {
    const body = new Element("body");
    const main = new Element("main");
    main.unsavedInput = "keep me";
    const document = shellDocument(body, main);
    const cleaned = [];
    const mounted = [];
    const subscriptions = {};
    const data = configuration();
    data.layouts[0].sections[0].columns = 2;
    data.layouts[0].sections[0].widgets.push({ id: "news", type: "core.news", row: 1, column: 2, rowSpan: 1, columnSpan: 1 });
    const shell = load("models/layoutshell.js", {
        ko: {
            cleanNode: node => cleaned.push(node),
            applyBindingsToNode: (node, binding) => {
                if (binding.component.name === "site-widget") {
                    mounted.push(node);
                }
            }
        },
        "models/layouts": { current: () => data, load: async () => data, select: () => data.layouts[0] },
        "models/invalidation": { subscribe: (topic, handler) => { subscriptions[topic] = handler; }, invalidate: async () => {} },
        "models/session": { accessToken: ko.observable("") },
        "models/restriction": { restricted: () => false },
        "models/i18n": { t: key => key },
        ...chromeComponents
    }, { document });
    await shell.start();
    assert.equal(mounted.length, 1);
    assert.notEqual(main.parentNode, body);
    await subscriptions.layouts();
    assert.equal(mounted.length, 2);
    assert.equal(cleaned.length, 1);
    assert.ok(!cleaned.includes(main));
    assert.equal(main.unsavedInput, "keep me");
    shell.route("#/manage/layouts");
    assert.equal(main.parentNode, body);
    assert.equal(cleaned.length, 2);
    assert.ok(!body.children.some(element => element.classList.contains("behind-layout")));
});

test("a single-content full-width layout remains rendered by the engine", async () => {
    const body = new Element("body");
    const main = new Element("main");
    const document = shellDocument(body, main);
    const data = configuration();
    data.layouts[0].sections[0].width = "full";
    const shell = load("models/layoutshell.js", {
        ko: {
            cleanNode() {},
            applyBindingsToNode(node, binding) {
                assert.notEqual(binding.component.name, "site-widget", "Content must not be rebound as a widget");
            }
        },
        "models/layouts": { current: () => data, load: async () => data, select: () => data.layouts[0] },
        "models/invalidation": { subscribe() {} },
        "models/restriction": { restricted: () => false },
        "models/i18n": { t: key => key },
        ...chromeComponents
    }, { document });
    await shell.start();
    const host = body.children.find(element => element.classList.contains("behind-layout"));
    assert.equal(host.className, "behind-layout");
    assert.equal(host.children[0].className, "behind-layout-band");
    assert.match(host.children[0].children[0].className, /behind-layout-section-full/);
    assert.notEqual(main.parentNode, body);
    shell.route("#/administration");
    assert.equal(main.parentNode, body);
});

test("message editing allows optional titles, rejects inverted schedules and saves UTC instants", async () => {
    let saved;
    const Model = load("viewmodels/messages.js", {
        ko, "models/session": { isSignedIn: () => true, hasGrant: () => true },
        "models/messages": { create: async fields => { saved = fields; return { ok: true }; } },
        "models/managedlist": () => ({
            busy: ko.observable(false), message: ko.observable(""),
            perform: action => action(), activate: async () => {}
        }),
        "models/i18n": { t: key => key }, "bindings/richeditor": {}
    });
    const view = new Model();
    view.name(" Notice ");
    view.content("<p>Content</p>");
    assert.equal(view.title(), "");
    assert.equal(view.canSave(), true);
    view.startsAt("2026-11-12T12:00");
    view.endsAt("2026-11-12T11:00");
    assert.equal(view.canSave(), false);
    view.endsAt("2026-11-12T13:00");
    await view.save();
    assert.equal(saved.title, "");
    assert.equal(saved.name, "Notice");
    assert.equal(saved.startsAt, new Date("2026-11-12T12:00").toISOString());
    assert.equal(saved.endsAt, new Date("2026-11-12T13:00").toISOString());
    assert.equal(view.name(), "");
});
