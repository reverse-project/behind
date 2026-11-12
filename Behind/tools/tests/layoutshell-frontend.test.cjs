const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const root = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");

class Element {
    constructor(name) {
        this.nodeName = name;
        this.children = [];
        this.parentNode = null;
        this.style = {};
        this.attributes = {};
        this.classes = new Set();
        this.classList = {
            add: (...tokens) => tokens.forEach(token => this.classes.add(token)),
            remove: token => this.classes.delete(token),
            contains: token => this.classes.has(token)
        };
        this.hidden = false;
        this.textContent = "";
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
        return this.insertBefore(child, null);
    }

    insertBefore(child, before) {
        if (child.parentNode) {
            child.parentNode.children.splice(child.parentNode.children.indexOf(child), 1);
        }
        const index = before ? this.children.indexOf(before) : this.children.length;
        this.children.splice(index, 0, child);
        child.parentNode = this;
        return child;
    }

    setAttribute(name, value) {
        this.attributes[name] = value;
    }

    getAttribute(name) {
        return this.attributes[name] ?? null;
    }

    querySelectorAll(selector) {
        const descendants = [];
        const visit = element => element.children.forEach(child => {
            descendants.push(child);
            visit(child);
        });
        visit(this);
        if (selector === ".navbar-collapse[id]") {
            return descendants.filter(element => element.classList.contains("navbar-collapse") && element.id);
        }
        if (selector === "[data-bs-toggle='collapse']") {
            return descendants.filter(element => element.getAttribute("data-bs-toggle") === "collapse");
        }
        return [];
    }

    remove() {
        if (this.parentNode) {
            this.parentNode.children.splice(this.parentNode.children.indexOf(this), 1);
            this.parentNode = null;
        }
    }
}

function load(file, dependencies, extra = {}) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(root, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => dependencies[name]));
        },
        console,
        Promise,
        ...extra
    }, { filename: file });
    return result;
}

function configuration(sections = []) {
    return {
        revision: 1,
        defaultLayout: "default",
        layouts: [{
            id: "default",
            deleted: false,
            sections: sections.length ? sections : [{
                rows: 1,
                columns: 1,
                width: "site",
                widgets: [{ id: "content", type: "core.content", row: 1, column: 1, rowSpan: 1, columnSpan: 1 }]
            }]
        }],
        assignments: []
    };
}

function setup(data, options = {}) {
    const application = new Element("div");
    const header = new Element("header");
    header.id = "header-container";
    header.className = "behind-page-header";
    const main = new Element("main");
    main.id = "main-container";
    main.className = "container my-4";
    main.unsavedInput = "preserved";
    const footer = new Element("footer");
    footer.id = "footer-container";
    footer.className = "behind-page-footer";
    application.appendChild(header);
    application.appendChild(main);
    application.appendChild(footer);
    const elements = {
        "application-shell": application,
        "header-container": header,
        "main-container": main,
        "footer-container": footer
    };
    const document = {
        getElementById: id => elements[id],
        createElement: name => new Element(name),
        createComment: name => new Element("#" + name)
    };
    const bindings = [];
    const cleaned = [];
    const subscriptions = {};
    const component = name => ({
        mount: (element, params) => bindings.push({
            element,
            binding: { component: { name, params } }
        })
    });
    const restriction = { restricted: () => !!options.restricted };
    const current = options.current === undefined ? data : options.current;
    const layouts = {
        current: () => current,
        load: options.load || (async () => data),
        select: configuration => configuration.layouts[0]
    };
    const shell = load("models/layoutshell.js", {
        ko: {
            applyBindingsToNode: (element, binding) => bindings.push({ element, binding }),
            cleanNode: element => cleaned.push(element)
        },
        "models/layouts": layouts,
        "models/invalidation": {
            subscribe: (topic, handler) => { subscriptions[topic] = handler; }
        },
        "models/restriction": restriction,
        "models/i18n": { t: key => key },
        "components/siteheader": component("site-header"),
        "components/sitefooter": component("site-footer")
    }, { document, console: { error() {} } });
    const context = { navigate() {} };
    return {
        shell, context, elements, application, header, main, footer, bindings, cleaned, subscriptions
    };
}

function widget(type, id) {
    return { id, type, row: 1, column: 1, rowSpan: 1, columnSpan: 1 };
}

test("public chrome and route content are rendered only by the layout engine and restored for fixed routes", async () => {
    const data = configuration([
        { rows: 1, columns: 1, width: "full", widgets: [{ ...widget("core.header", "header"), hamburgerPosition: "left" }] },
        { rows: 1, columns: 1, width: "site", cssClass: "content-band compact", widgets: [
            { ...widget("core.content", "content"), cssClass: "content-area featured" }
        ] },
        { rows: 1, columns: 1, width: "full", widgets: [widget("core.footer", "footer")] }
    ]);
    const fixture = setup(data);
    fixture.header.innerHTML = "<nav class=\"fse-menu\">Site navigation</nav>";
    fixture.header.appendChild(new Element("nav"));
    fixture.footer.innerHTML = "<div class=\"fse-contact\">Site contact</div>";
    fixture.footer.appendChild(new Element("div"));
    await fixture.shell.start(fixture.context);

    const host = fixture.application.children.find(element => element.classList.contains("behind-layout"));
    assert.ok(host);
    assert.equal(fixture.header.parentNode.classList.contains("behind-layout-cell"), true);
    assert.equal(fixture.footer.parentNode.classList.contains("behind-layout-cell"), true);
    assert.equal(fixture.main.parentNode.classList.contains("content-area"), true);
    assert.equal(fixture.main.unsavedInput, "preserved");
    assert.ok(!fixture.cleaned.includes(fixture.main));
    assert.equal(fixture.bindings.length, 2);
    assert.equal(fixture.bindings[0].binding.component.name, "site-header");
    assert.equal(fixture.bindings[1].binding.component.name, "site-footer");
    assert.equal(fixture.bindings[0].binding.component.params.template, fixture.header.innerHTML);
    assert.equal(fixture.bindings[0].binding.component.params.hamburgerPosition, "left");
    assert.equal(fixture.bindings[1].binding.component.params.template, fixture.footer.innerHTML);
    const sections = host.children.map(band => band.children[0]);
    assert.equal(host.children[0].style.zIndex, undefined);
    assert.equal(host.children[1].style.zIndex, undefined);
    assert.equal(fixture.header.parentNode.style.zIndex, undefined);
    assert.ok(sections[1].classList.contains("content-band"));
    assert.ok(sections[1].classList.contains("compact"));
    assert.ok(fixture.main.parentNode.classList.contains("featured"));

    fixture.shell.route("#/manage/layouts");
    assert.equal(fixture.header.parentNode, fixture.application);
    assert.equal(fixture.footer.parentNode, fixture.application);
    assert.equal(fixture.main.parentNode, fixture.application);
    assert.equal(fixture.bindings.length, 4);
    assert.equal(fixture.bindings[2].binding.component.params.hamburgerPosition, undefined);
    assert.ok(fixture.cleaned.includes(fixture.header));
    assert.ok(fixture.cleaned.includes(fixture.footer));
    assert.ok(!fixture.cleaned.includes(fixture.main));
    assert.ok(!fixture.application.children.some(element => element.classList.contains("behind-layout")));
});

test("public layouts may omit either chrome widget without leaving fixed or hidden active bindings", async () => {
    const data = configuration([
        { rows: 1, columns: 1, width: "site", widgets: [widget("core.content", "content")] }
    ]);
    const fixture = setup(data);
    await fixture.shell.start(fixture.context);

    assert.equal(fixture.header.parentNode, null);
    assert.equal(fixture.footer.parentNode, null);
    assert.equal(fixture.bindings.length, 0);
    assert.equal(fixture.main.parentNode.classList.contains("behind-layout-cell"), true);
    fixture.shell.route("#/sign-in");
    assert.equal(fixture.header.parentNode, fixture.application);
    assert.equal(fixture.footer.parentNode, fixture.application);
    assert.equal(fixture.bindings.length, 2);
    assert.ok(fixture.shell.isManagementRoute("#/manage/layouts"));
    assert.ok(fixture.shell.isManagementRoute("#/profile"));
    assert.ok(fixture.shell.isManagementRoute("#/audit-log"));
    assert.ok(fixture.shell.isManagementRoute("#/session/renew"));
    assert.ok(fixture.shell.isManagementRoute("#/sign/in/magiclink"));
    assert.ok(!fixture.shell.isManagementRoute("#/news"));
});

test("cell appearance options are independent and do not leak across invalidation", async () => {
    const data = configuration([
        { rows: 1, columns: 4, width: "site", widgets: [
            { ...widget("core.content", "content"), borderless: true },
            { ...widget("core.custom", "custom"), column: 2, noMargin: true },
            { ...widget("core.message", "message"), column: 3, noPadding: true },
            { ...widget("core.footer", "footer"), column: 4 }
        ] }
    ]);
    const fixture = setup(data);
    await fixture.shell.start(fixture.context);
    const cells = () => fixture.application.children.find(element => element.classList.contains("behind-layout"))
        .children[0].children[0].children;
    const options = ["borderless", "no-margin", "no-padding"];
    cells().forEach((cell, index) => {
        options.forEach((option, optionIndex) => {
            assert.equal(cell.classList.contains("behind-layout-cell-" + option), index === optionIndex);
        });
    });
    data.layouts[0].sections[0].widgets.forEach(item => {
        item.borderless = false;
        item.noMargin = false;
        item.noPadding = false;
    });
    await fixture.subscriptions.layouts();
    cells().forEach(cell => options.forEach(option => {
        assert.equal(cell.classList.contains("behind-layout-cell-" + option), false);
    }));
    assert.ok(!fixture.cleaned.includes(fixture.main));
});

test("badges overlay sections with safe text and image links and disappear on management routes", async () => {
    const data = configuration();
    data.layouts[0].badges = [
        { id: "development", text: "<script>Under development</script>", position: "top-right", width: 180 },
        { id: "funding", text: "EU funding", image: "/layouts/images/0a1bc242-6328-4021-959c-96793c556da7", link: "https://example.org/funding", position: "top-right", width: 240 },
        { id: "bottom", text: "Partner", position: "bottom-left", width: 80 }
    ];
    const fixture = setup(data);
    await fixture.shell.start(fixture.context);
    const host = fixture.application.children.find(element => element.classList.contains("behind-layout"));
    const overlay = host.children[host.children.length - 1];
    assert.equal(overlay.className, "behind-layout-badges");
    assert.equal(overlay.children.length, 2);
    const corner = overlay.children[0];
    assert.ok(corner.classList.contains("behind-layout-badge-top-right"));
    assert.equal(corner.children.length, 2);
    assert.equal(corner.children[0].textContent, "<script>Under development</script>");
    assert.equal(corner.children[0].children.length, 0);
    const linked = corner.children[1];
    assert.equal(linked.nodeName, "a");
    assert.equal(linked.getAttribute("href"), "https://example.org/funding");
    assert.equal(linked.style.width, "240px");
    assert.equal(linked.children[0].getAttribute("alt"), "EU funding");
    assert.equal(linked.children[0].getAttribute("src"), data.layouts[0].badges[1].image);
    assert.equal(fixture.main.unsavedInput, "preserved");
    fixture.shell.route("#/manage/layouts");
    assert.ok(!fixture.application.children.includes(host));
    assert.ok(!fixture.cleaned.includes(fixture.main));
    fixture.shell.route("#/news");
    assert.ok(fixture.application.children.some(element => element.classList.contains("behind-layout")));
    data.layouts[0].badges = [];
    await fixture.subscriptions.layouts();
    const refreshed = fixture.application.children.find(element => element.classList.contains("behind-layout"));
    assert.ok(!refreshed.children.some(element => element.className === "behind-layout-badges"));
    const restricted = setup(data, { restricted: true });
    data.layouts[0].badges = [{ id: "restricted", text: "Hidden", position: "top-right", width: 180 }];
    await restricted.shell.start(restricted.context);
    assert.ok(!restricted.application.children.some(element => element.classList.contains("behind-layout")));
});

test("load failures keep a usable fixed shell and stale failures cannot replace a newer route", async () => {
    const fixture = setup(null, {
        current: null,
        load: () => Promise.reject(new Error("offline"))
    });
    await fixture.shell.start(fixture.context);
    assert.equal(fixture.header.parentNode, fixture.application);
    assert.equal(fixture.footer.parentNode, fixture.application);
    assert.equal(fixture.bindings.length, 2);
    assert.ok(fixture.application.children.some(element => element.textContent === "layouts.error.load"));

    const rejectReads = [];
    const stale = setup(null, {
        current: null,
        load: () => new Promise((resolve, reject) => { rejectReads.push(reject); })
    });
    const loading = stale.shell.start(stale.context);
    stale.shell.route("#/manage/layouts");
    rejectReads[0](new Error("stale"));
    await loading;
    assert.equal(stale.header.parentNode, stale.application);
    assert.equal(stale.footer.parentNode, stale.application);
});

test("header component instances get independent collapse targets and footer templates share fixed and public behavior", () => {
    const ko = require(path.join(root, "libraries/knockout.min.js"));
    const main = () => ({ items: [] });
    main.dispose = () => {};
    const session = {
        isSignedIn: ko.observable(false),
        displayName: ko.observable(""),
        grants: ko.observable(null),
        isImpersonating: ko.observable(false)
    };
    const headerViewModel = load("viewmodels/header.js", {
        ko,
        "models/auth": { loadSession() {}, stopImpersonating() {}, signOut() {} },
        "models/session": session,
        "models/menus": { follow: () => main },
        "models/accounts": { signIn: ko.observable(true), signUp: ko.observable(true) },
        "models/branding": { title: ko.observable("Site") },
        "models/i18n": { t: key => key }
    });
    const first = new headerViewModel({ navigate() {} });
    const second = new headerViewModel({ navigate() {} });
    assert.notEqual(first.navigationId, second.navigationId);
    first.dispose();
    second.dispose();

    const headerTemplate = fs.readFileSync(path.join(root, "components/siteheader.html"), "utf8");
    const footerTemplate = fs.readFileSync(path.join(root, "components/sitefooter.html"), "utf8");
    assert.match(headerTemplate, /'data-bs-target': '#' \+ navigationId/);
    assert.match(headerTemplate, /id: navigationId/);
    assert.match(footerTemplate, /menu-nav params="name: 'footer'"/);
    assert.match(fs.readFileSync(path.join(root, "index.html"), "utf8"), /<header id="header-container" class="behind-page-header"><\/header>/);
    assert.match(fs.readFileSync(path.join(root, "index.html"), "utf8"), /<footer id="footer-container" class="behind-page-footer border-top py-3"><\/footer>/);
});

test("chrome mounts preserve site-provided markup and dispose the bound view models", () => {
    const callbacks = [];
    const bindings = [];
    let disposed = 0;
    let sequence = 0;
    function ViewModel() {
        this.navigationId = "navigation-" + (++sequence);
        this.dispose = () => { disposed++; };
        this.activate = () => Promise.resolve();
    }
    const ko = {
        unwrap: value => value,
        applyBindings: (viewModel, element) => bindings.push({ viewModel, element }),
        utils: {
            domNodeDisposal: {
                addDisposeCallback: (element, callback) => callbacks.push({ element, callback })
            }
        }
    };
    const header = load("components/siteheader.js", {
        ko,
        "viewmodels/header": ViewModel,
        "text!components/siteheader.html": "<nav>Template</nav>"
    });
    const footer = load("components/sitefooter.js", {
        ko,
        "viewmodels/footer": ViewModel,
        "text!components/sitefooter.html": "<div>Template</div>"
    });
    const brandedHeader = new Element("div");
    const headerRoot = new Element("header");
    const collapse = new Element("div");
    collapse.id = "navigation";
    collapse.className = "collapse navbar-collapse";
    const toggle = new Element("button");
    toggle.setAttribute("data-bs-toggle", "collapse");
    toggle.setAttribute("data-bs-target", "#navigation");
    toggle.setAttribute("aria-controls", "navigation");
    headerRoot.appendChild(brandedHeader);
    headerRoot.appendChild(toggle);
    headerRoot.appendChild(collapse);
    const footerRoot = new Element("footer");
    header.mount(headerRoot, { context: {}, template: "<nav>Custom site markup</nav>", hamburgerPosition: "left" });
    footer.mount(footerRoot, { template: "<div>Custom footer</div>" });

    assert.equal(headerRoot.children[0], brandedHeader);
    assert.equal(headerRoot.getAttribute("data-hamburger-position"), "left");
    assert.equal(headerRoot.innerHTML, undefined);
    assert.equal(collapse.id, bindings[0].viewModel.navigationId);
    assert.equal(toggle.getAttribute("data-bs-target"), "#" + collapse.id);
    assert.equal(toggle.getAttribute("aria-controls"), collapse.id);
    assert.equal(footerRoot.innerHTML, "<div>Custom footer</div>");
    assert.equal(bindings.length, 2);
    callbacks.forEach(entry => entry.callback());
    assert.equal(disposed, 2);
    header.mount(headerRoot, { context: {}, template: "<nav>Custom site markup</nav>", hamburgerPosition: "right" });
    assert.equal(headerRoot.getAttribute("data-hamburger-position"), "right");
    callbacks.at(-1).callback();
    header.mount(headerRoot, { context: {}, template: "<nav>Custom site markup</nav>" });
    assert.equal(headerRoot.getAttribute("data-hamburger-position"), "default");
    callbacks.at(-1).callback();
});
