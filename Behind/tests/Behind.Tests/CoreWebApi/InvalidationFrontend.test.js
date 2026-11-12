const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const core = process.argv[2];
const forumRoot = process.argv[3];

function load(root, file, dependencies, globals = {}) {
    let exported;
    function define(names, factory) {
        exported = factory(...names.map(name => dependencies[name]));
    }
    vm.runInNewContext(fs.readFileSync(path.join(root, file), "utf8"), {
        define, Promise, Map, Set, AggregateError, FormData, console, ...globals
    }, { filename: file });
    return exported;
}

function observable(initial) {
    let value = initial;
    const listeners = new Set();
    function result(next) {
        if (arguments.length) {
            value = next;
            listeners.forEach(listener => listener(next));
            return result;
        }
        return value;
    }
    result.subscribe = listener => {
        listeners.add(listener);
        return { dispose: () => listeners.delete(listener) };
    };
    result.dispose = () => {};
    return result;
}

function deferred() {
    let resolve;
    const promise = new Promise(done => { resolve = done; });
    return { promise, resolve };
}

async function main() {
    const invalidation = load(core, "models/invalidation.js", {});
    const results = load(core, "models/results.js", {
        "models/i18n": { tOr: value => value }
    });
    const paging = { paging: () => "", managing: value => value };
    let response = { ok: true, data: {} };
    const http = {
        post: () => Promise.resolve(response),
        postForm: () => Promise.resolve(response),
        get: () => Promise.resolve(response)
    };
    const shared = {
        "models/http": http,
        "models/results": results,
        "models/paging": paging,
        "models/invalidation": invalidation,
        "models/i18n": { t: value => value }
    };
    const news = load(core, "models/news.js", shared);
    const articles = load(core, "models/articles.js", shared);
    const messages = load(core, "models/messages.js", shared);
    const downloads = load(core, "models/downloads.js", shared);
    const forum = load(forumRoot, "modules/forum/models/forum.js", shared);
    const counts = {};
    const subscriptions = ["core.news", "core.articles", "core.message", "core.downloads", "forum.posts", "forum.stats"]
        .map(type => invalidation.subscribe("widget:" + type, () => {
            counts[type] = (counts[type] || 0) + 1;
        }));

    await news.create({});
    await news.saveVersion("id", {});
    await news.activate("id", 1);
    await news.remove("id");
    await news.restore("id");
    await news.hardDelete("id");
    assert.equal(counts["core.news"], 6);
    await articles.create({});
    await articles.saveVersion("id", {});
    await articles.activate("id", 1);
    await articles.remove("id");
    await articles.restore("id");
    await articles.hardDelete("id");
    assert.equal(counts["core.articles"], 6);
    await messages.create({});
    await messages.save("id", {});
    await messages.remove("id");
    await messages.restore("id");
    await messages.hardDelete("id");
    assert.equal(counts["core.message"], 5);
    await downloads.rename("id", "name");
    await downloads.remove("id");
    await downloads.restore("id");
    await downloads.hardDelete("id");
    await downloads.removeFolder("id");
    response = { ok: true, data: { files: [{}] } };
    await downloads.upload("id", new Blob(["file"]), "file");
    assert.equal(counts["core.downloads"], 6);
    response = { ok: true, data: { files: [{ error: "empty" }] } };
    assert.equal((await downloads.upload("id", new Blob(["file"]), "file")).ok, false);
    assert.equal(counts["core.downloads"], 6);
    response = { ok: true, data: { id: "post" } };
    assert.equal((await forum.save("posts", {})).id, "post");
    assert.equal(counts["forum.posts"], 1);
    assert.equal(counts["forum.stats"], 1);

    response = { ok: false, status: 403, data: {} };
    assert.equal((await news.create({})).ok, false);
    assert.equal((await articles.create({})).ok, false);
    assert.equal((await messages.create({})).ok, false);
    assert.equal((await downloads.rename("id", "name")).ok, false);
    await assert.rejects(forum.save("posts", {}));
    assert.equal(counts["core.news"], 6);
    assert.equal(counts["core.articles"], 6);
    assert.equal(counts["core.message"], 5);
    assert.equal(counts["core.downloads"], 6);
    assert.equal(counts["forum.posts"], 1);
    assert.equal(counts["forum.stats"], 1);
    subscriptions.forEach(subscription => subscription.dispose());

    let widgetLoads = 0;
    const session = {
        accessToken: observable("token"),
        isSignedIn: observable(true),
        hasGrant: () => true
    };
    const assignmentRevision = observable(0);
    const ko = {
        observable,
        observableArray: observable,
        unwrap: value => typeof value === "function" ? value() : value,
        pureComputed: fn => {
            const result = () => fn();
            result.subscribe = () => ({ dispose() {} });
            result.dispose = () => {};
            return result;
        }
    };
    const component = load(core, "components/sitewidget.js", {
        require: () => {},
        ko,
        "models/widgets": {},
        "models/articles": {},
        "models/menus": {},
        "models/http": { get: () => {
            widgetLoads++;
            return Promise.resolve({ ok: true, data: [] });
        } },
        "models/session": session,
        "models/i18n": { t: value => value },
        "models/invalidation": invalidation,
        "models/user": {},
        "models/contentlocale": { assignmentRevision },
        "text!components/sitewidget.html": ""
    }, { window: {} });
    const widget = component.viewModel.createViewModel({ widget: { type: "core.news" } });
    await invalidation.invalidate("widget:core.news");
    assert.equal(widgetLoads, 2);
    await invalidation.invalidate("widget:core.articles");
    assert.equal(widgetLoads, 2);
    assignmentRevision(1);
    assert.equal(widgetLoads, 3);
    widget.dispose();
    await invalidation.invalidate("widget:core.news");
    assignmentRevision(2);
    assert.equal(widgetLoads, 3);

    let chatLoads = 0;
    let timersCleared = 0;
    const chatComponent = load(core, "components/sitewidget.js", {
        require: () => {},
        ko,
        "models/widgets": {},
        "models/articles": {},
        "models/menus": {},
        "models/http": {
            get: () => {
                chatLoads++;
                return Promise.resolve({ ok: true, data: { items: [], nextCursor: null } });
            },
            post: () => Promise.resolve({ ok: true })
        },
        "models/session": session,
        "models/i18n": { t: value => value },
        "models/invalidation": invalidation,
        "models/user": {},
        "models/contentlocale": { assignmentRevision },
        "text!components/sitewidget.html": ""
    }, { window: {
        setInterval: () => 1,
        clearInterval: () => { timersCleared++; }
    } });
    const firstChat = chatComponent.viewModel.createViewModel({ widget: { type: "core.chat" } });
    const secondChat = chatComponent.viewModel.createViewModel({ widget: { type: "core.chat" } });
    firstChat.text("hello");
    await firstChat.send();
    assert.equal(chatLoads, 4);
    firstChat.dispose();
    secondChat.dispose();
    assert.equal(timersCleared, 2);
    await invalidation.invalidate("widget:core.chat");
    assert.equal(chatLoads, 4);

    const failing = invalidation.subscribe("failure", () => { throw new Error("refresh failed"); });
    await assert.rejects(invalidation.invalidate("failure"), error =>
        error instanceof AggregateError && error.errors[0].message === "refresh failed");
    failing.dispose();

    const requests = [];
    let saveResponse = { ok: true, data: { layouts: [] } };
    const layoutHttp = {
        get: () => {
            const request = deferred();
            requests.push(request);
            return request.promise;
        },
        post: () => Promise.resolve(saveResponse)
    };
    const locale = { current: () => null };
    const layouts = load(core, "models/layouts.js", {
        "models/http": layoutHttp,
        "models/invalidation": invalidation,
        "models/results": results,
        "models/contentlocale": locale
    });
    const layout = revision => ({ ok: true, data: {
        schemaVersion: 1, revision, defaultLayout: "main", layouts: [{ id: "main" }], assignments: []
    } });
    const first = layouts.load();
    const follower = invalidation.subscribe("layouts", () => layouts.load());
    const saved = layouts.save({});
    await Promise.resolve();
    await Promise.resolve();
    assert.equal(requests.length, 2);
    requests[0].resolve(layout(1));
    await first;
    const coalesced = layouts.load();
    assert.equal(requests.length, 2);
    requests[1].resolve(layout(2));
    await Promise.all([saved, coalesced]);
    assert.equal(layouts.current().revision, 2);
    const old = layouts.load();
    const secondSave = layouts.save({});
    await Promise.resolve();
    await Promise.resolve();
    assert.equal(requests.length, 4);
    requests[3].resolve(layout(4));
    await secondSave;
    requests[2].resolve(layout(3));
    await old;
    assert.equal(layouts.current().revision, 4);
    saveResponse = { ok: false };
    const unchanged = layouts.load();
    await layouts.save({});
    assert.equal(layouts.load(), unchanged);
    assert.equal(requests.length, 5);
    requests[4].resolve(layout(4));
    await unchanged;
    follower.dispose();

    class Element {
        constructor(tag) {
            this.tag = tag;
            this.children = [];
            this.parentNode = null;
            this.style = {};
            this.className = "";
            this.attributes = {};
            const classes = new Set();
            this.classList = {
                add: name => classes.add(name),
                remove: name => classes.delete(name),
                contains: name => classes.has(name)
            };
        }
        appendChild(element) {
            if (element.parentNode) {
                element.remove();
            }
            this.children.push(element);
            element.parentNode = this;
            return element;
        }
        insertBefore(element, next) {
            if (element.parentNode) {
                element.remove();
            }
            const index = this.children.indexOf(next);
            this.children.splice(index < 0 ? this.children.length : index, 0, element);
            element.parentNode = this;
            return element;
        }
        remove() {
            if (this.parentNode) {
                const siblings = this.parentNode.children;
                siblings.splice(siblings.indexOf(this), 1);
                this.parentNode = null;
            }
        }
        setAttribute(name, value) {
            this.attributes[name] = value;
        }
    }

    const root = new Element("root");
    const header = root.appendChild(new Element("header"));
    const main = root.appendChild(new Element("main"));
    const footer = root.appendChild(new Element("footer"));
    const editorState = main.appendChild(new Element("editor"));
    const document = {
        getElementById: id => ({
            "application-shell": root,
            "main-container": main,
            "header-container": header,
            "footer-container": footer
        })[id] || null,
        createElement: tag => new Element(tag),
        createComment: () => new Element("comment")
    };
    let cleanedMain = false;
    const shellKo = {
        cleanNode: element => { cleanedMain = cleanedMain || element === main; },
        applyBindingsToNode() {}
    };
    const publicLayout = {
        id: "public", sections: [{ columns: 1, rows: 1, widgets: [
            { type: "core.content", row: 1, column: 1, rowSpan: 1, columnSpan: 1 }
        ] }]
    };
    const selectedLayout = {
        id: "selected", locale: "hu-HU", sections: [{ columns: 1, rows: 3, cssClass: "saved-preview", widgets: [
            { type: "core.header", hamburgerPosition: "left", row: 1, column: 1, rowSpan: 1, columnSpan: 1 },
            { type: "core.content", row: 2, column: 1, rowSpan: 1, columnSpan: 1 },
            { type: "core.footer", row: 3, column: 1, rowSpan: 1, columnSpan: 1 }
        ] }]
    };
    let serverResponse = { ok: true, data: { layouts: [publicLayout, selectedLayout] } };
    const shellLayouts = load(core, "models/layouts.js", {
        "models/http": {
            get: () => Promise.resolve({ ok: true, data: {
                schemaVersion: 1, revision: 1, defaultLayout: "public", layouts: [publicLayout], assignments: []
            } }),
            post: () => Promise.resolve(serverResponse)
        },
        "models/invalidation": invalidation,
        "models/results": results,
        "models/contentlocale": locale
    });
    let headerOptions;
    let footerMounts = 0;
    const shell = load(core, "models/layoutshell.js", {
        ko: shellKo,
        "models/layouts": shellLayouts,
        "models/invalidation": invalidation,
        "models/restriction": { restricted: () => false },
        "models/i18n": { t: value => value },
        "components/siteheader": { mount: (element, options) => { headerOptions = options; } },
        "components/sitefooter": { mount: () => { footerMounts++; } },
        "models/contentlocale": locale
    }, { document });
    await shell.start({});
    shell.route("#/manage/layouts");
    assert.equal(main.parentNode, root);
    await shellLayouts.save({}, "selected");
    assert.notEqual(main.parentNode, root);
    assert.equal(main.parentNode.parentNode.classList.contains("saved-preview"), true);
    assert.equal(headerOptions.hamburgerPosition, "left");
    assert.equal(footerMounts, 2);
    assert.equal(main.children[0], editorState);
    assert.equal(cleanedMain, false);
    serverResponse = { ok: false, status: 409 };
    await shellLayouts.save({}, "public");
    assert.equal(main.parentNode.parentNode.classList.contains("saved-preview"), true);
    shell.route("#/manage/users");
    assert.equal(main.parentNode, root);
    shell.route("#/");
    assert.notEqual(main.parentNode, root);
    assert.equal(main.parentNode.parentNode.classList.contains("saved-preview"), false);
    assert.equal(main.children[0], editorState);
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
