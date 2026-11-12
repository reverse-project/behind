const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const frontend = process.argv[2];

function observable(initial) {
    let value = initial;
    const listeners = [];
    const result = function (next) {
        if (arguments.length) {
            if (value !== next) {
                value = next;
                listeners.slice().forEach(listener => listener(value));
            }
            return result;
        }
        return value;
    };
    result.subscribe = function (listener) {
        listeners.push(listener);
        return { dispose() { listeners.splice(listeners.indexOf(listener), 1); } };
    };
    result.dispose = function () {};
    return result;
}

const ko = {
    observable,
    observableArray: observable,
    pureComputed: fn => {
        const computed = typeof fn === "function" ? fn : fn.read;
        computed.dispose = function () {};
        return computed;
    },
    unwrap: value => typeof value === "function" ? value() : value,
    components: { register() {} },
    cleanNode() {},
    applyBindings() {}
};

function load(file, dependencies, globals = {}) {
    let exported;
    const define = function (names, factory) {
        if (typeof names === "function") {
            factory = names;
            names = [];
        }
        exported = factory(...names.map(name => dependencies[name]));
    };
    define.amd = {};
    vm.runInNewContext(fs.readFileSync(path.join(frontend, file), "utf8"), {
        define, Promise, Map, WeakMap, FormData, console, ...globals
    }, { filename: file });
    return exported;
}

async function main() {
    const switcherTemplate = fs.readFileSync(path.join(frontend, "components/localeswitch.html"), "utf8");
    assert.match(switcherTemplate, /<!-- ko if: view === 'view' -->\s*<select[^>]+data-bind="options:/);
    assert.match(switcherTemplate, /<!-- ko if: view !== 'view' -->\s*<div[^>]+data-bind="foreach: options"/);
    assert.doesNotMatch(switcherTemplate, /<(?:select|div)[^>]+data-bind="[^"]*\bif:[^"]*(?:options|foreach):/);

    const window = {
        location: { hash: "#/", pathname: "/", search: "", href: "https://example.test/", origin: "https://example.test", replace(hash) { this.hash = hash; } },
        navigator: { languages: ["hu-HU"], language: "hu-HU" },
        history: { replaceState() {} },
        setTimeout() {},
        setInterval() { return 1; },
        matchMedia: () => ({ matches: false, addEventListener() {} }),
        scrollTo() {}
    };
    const contentLocale = load("models/contentlocale.js", { ko }, { window, URL });
    contentLocale.locales([{ code: "en-US", name: "English", flag: "🇺🇸" }, { code: "hu-HU", name: "Magyar", flag: "🇭🇺" }]);
    assert.equal(contentLocale.defaultLocale([]), "en-US");
    assert.equal(contentLocale.defaultLocale(["en"]), "en-US");
    assert.equal(contentLocale.defaultLocale(["hu-HU"], "en-US"), "hu-HU");
    contentLocale.current("hu-HU");
    assert.equal(contentLocale.current(), "hu-HU");
    const layoutModel = load("models/layouts.js", {
        "models/http": {},
        "models/invalidation": {},
        "models/results": { create: () => ({}) },
        "models/contentlocale": contentLocale
    });
    const sharedAndLocalizedLayouts = {
        defaultLayout: "default",
        layouts: [{ id: "default", locale: null }, { id: "hungarian", locale: "hu-HU" }],
        assignments: [{ route: "#/news", layout: "default" }, { route: "#/news", layout: "hungarian" }]
    };
    assert.equal(layoutModel.select(sharedAndLocalizedLayouts, "#/news").id, "hungarian");
    contentLocale.current("en-US");
    assert.equal(layoutModel.select(sharedAndLocalizedLayouts, "#/news").id, "default");
    contentLocale.current("hu-HU");
    assert.equal(contentLocale.media("/news/images/abc?size=1#view", "hu-HU"), "/news/images/abc?size=1&contentLocale=hu-HU#view");
    assert.equal(contentLocale.media("/news/images/abc?contentLocale=en-US", "hu-HU"), "/news/images/abc?contentLocale=en-US");
    assert.equal(contentLocale.media("https://external.test/news/images/abc", "hu-HU"), "https://external.test/news/images/abc");
    assert.equal(contentLocale.media("/profile/images/abc", "hu-HU"), "/profile/images/abc");
    assert.equal(contentLocale.media("/downloads/files/abc", null), "/downloads/files/abc");
    contentLocale.trackMedia({ imageUrl: "/news/images/new" });
    assert.equal(contentLocale.media("/news/images/new", "hu-HU"), "/news/images/new");
    contentLocale.clearMedia();
    assert.equal(contentLocale.media("/news/images/new", "hu-HU"), "/news/images/new?contentLocale=hu-HU");

    const requests = [];
    const $ = function () {
        return { 0: {}, html() {} };
    };
    $.extend = Object.assign;
    $.each = function (items, callback) {
        Object.keys(items).forEach(key => callback(key, items[key]));
    };
    $.isEmptyObject = object => Object.keys(object).length === 0;
    $.ajax = function (options) {
        requests.push(options);
        const locale = options.headers["X-Content-Locale"] || "neutral";
        const xhr = { status: 200, getResponseHeader: () => '"' + locale + '"' };
        return { then(resolve) { resolve(JSON.stringify({ locale }), "success", xhr); } };
    };
    const session = {
        accessToken: () => "",
        isSignedIn: observable(false),
        isImpersonating: observable(false),
        refreshHeaders: () => ({})
    };
    const http = load("models/http.js", { jquery: $, "models/session": session, "models/contentlocale": contentLocale });
    contentLocale.current(null);
    assert.equal((await http.get("/news")).data.locale, "neutral");
    contentLocale.current("hu-HU");
    assert.equal((await http.get("/news")).data.locale, "hu-HU");
    assert.equal(requests[1].headers["If-None-Match"], undefined);
    await http.get("/news");
    assert.equal(requests[2].headers["If-None-Match"], '"hu-HU"');
    contentLocale.current(null);
    await http.get("/news");
    assert.equal(requests[3].headers["If-None-Match"], '"neutral"');
    contentLocale.current("hu-HU");
    await http.get("/news", { "X-Content-Locale": null });
    assert.equal(requests[4].headers["X-Content-Locale"], undefined);

    const calls = [];
    let router;
    const anchor = function (href) {
        return {
            href,
            hasAttribute() { return false; },
            getAttribute() { return this.href; },
            setAttribute(name, value) { this.href = value; }
        };
    };
    const links = [anchor("#/news?search=a"), anchor("#/articles"), anchor("#/manage/news")];
    const image = {
        tagName: "IMG",
        src: "/news/images/abc",
        getAttribute() { return this.src; },
        setAttribute(name, value) { this.src = value; },
        closest() { return null; }
    };
    const document = { documentElement: { setAttribute() {} }, querySelectorAll: selector => selector === "a[href^='#/']" ? links : [image] };
    const realSammy = load("libraries/sammy.min.js", { jquery: $ }, { window, document, history: window.history, location: window.location });
    function Sammy(configure) {
        router = realSammy(configure);
        router.trigger = function () {};
        router.run = function () {
            this.runRoute("get", window.location.hash || "#/");
        };
        return router;
    }
    const requireController = function (names, callback) {
        const name = names[0];
        callback(function (context) {
            this.exec = function () {
                calls.push({ name, params: context.params, locale: contentLocale.current(), context });
            };
        });
    };
    const i18n = { useDefault() {}, useAccount() {}, useContent: () => Promise.resolve(), remembered: () => "", apply: () => Promise.resolve() };
    const restriction = { restricted: observable(false), load: () => Promise.resolve() };
    const layout = { start: () => Promise.resolve(), route() {} };
    const startupLocales = [];
    const modules = { load: () => { startupLocales.push(contentLocale.current()); return Promise.resolve(); }, routes: () => [{ path: "#/sport/team/:id", controller: "module/team" }] };
    contentLocale.current(null);
    window.location.hash = "#/news/123";
    const app = load("app.js", {
        jquery: $, "jquery-migrate": {}, ko, sammy: Sammy,
        "models/accounts": { load() {} },
        "models/branding": { title: observable(""), locale: () => "en-US", load: () => { startupLocales.push(contentLocale.current()); return Promise.resolve(); } },
        "models/session": session,
        "models/usersettings": { locale: observable(""), load: () => Promise.resolve({ ok: true }), clear: () => Promise.resolve({ ok: true }) },
        "models/restriction": restriction, "models/i18n": i18n,
        "models/modules": modules, "models/layoutshell": layout, "models/contentlocale": contentLocale,
        "models/about": { loadLocales: () => Promise.resolve({ ok: true, items: contentLocale.locales() }) }
    }, { window, document, require: requireController, MutationObserver: function () { this.observe = function () {}; } });
    await app.start();
    assert.deepEqual(startupLocales, [null, null]);
    assert.equal(window.location.hash, "#/news/123");
    assert.equal(contentLocale.current(), "en-US");
    assert.equal(calls[calls.length - 1].name, "controllers/newsarticle");
    assert.equal(calls[calls.length - 1].locale, "en-US");
    function dispatch(hash) {
        window.location.hash = hash;
        assert.ok(router.lookupRoute("get", hash));
        router.runRoute("get", hash);
        return calls[calls.length - 1];
    }
    assert.equal(dispatch("#/news/123?x=1").name, "controllers/newsarticle");
    assert.equal(calls[calls.length - 1].locale, "en-US");
    assert.equal(links[0].href, "#/news?search=a");
    assert.equal(links[1].href, "#/articles");
    assert.equal(links[2].href, "#/manage/news");
    assert.equal(image.src, "/news/images/abc?contentLocale=en-US");
    calls[calls.length - 1].context.navigate("#/news");
    assert.equal(window.location.hash, "#/news");
    assert.equal(dispatch("#/sport/team/123").name, "module/team");
    assert.equal(calls[calls.length - 1].locale, "en-US");
    assert.equal(image.src, "/news/images/abc?contentLocale=en-US");
    assert.equal(links[0].href, "#/news?search=a");
    assert.equal(links[1].href, "#/articles");
    const downloadsRoute = dispatch("#/downloads/first/second");
    assert.equal(downloadsRoute.name, "controllers/downloads");
    assert.equal(downloadsRoute.params.splat[0], "first/second");
    assert.equal(downloadsRoute.locale, "en-US");
    assert.equal(dispatch("#/example").name, "controllers/routealias");
    assert.equal(dispatch("#/news/123").locale, "en-US");
    assert.equal(image.src, "/news/images/abc?contentLocale=en-US");
    restriction.restricted(true);
    assert.equal(dispatch("#/news/123").name, "controllers/restriction");
    assert.equal(dispatch("#/sign-in").name, "controllers/signin");

    const savedLocales = [];
    const switcherI18n = {
        t: key => key,
        remembered: () => "",
        choose(code) {
            savedLocales.push(code);
            return Promise.resolve();
        }
    };
    const switcherSettings = {
        locale: observable(""),
        saveLocale(code) {
            savedLocales.push(code);
            this.locale(code);
            return Promise.resolve({ ok: true });
        }
    };
    const switcher = load("components/localeswitch.js", {
        ko, "models/contentlocale": contentLocale, "models/i18n": switcherI18n,
        "models/session": session,
        "models/usersettings": switcherSettings,
        "text!components/localeswitch.html": ""
    }, { window });
    window.location.hash = "#/news/123?search=x";
    const chooser = new switcher.viewModel();
    assert.equal(chooser.view, "view");
    assert.equal(chooser.value(), "en-US");
    assert.equal(chooser.options().length, 2);
    assert.equal(chooser.optionLabel(chooser.options()[0]), "English");
    const flags = new switcher.viewModel({ view: "flag-only" });
    assert.equal(flags.optionLabel(flags.options()[0]), "🇺🇸");
    contentLocale.locales([{ code: "en-GB", name: "English", flag: "🏴" }]);
    assert.equal(flags.optionLabel(flags.options()[0]), "🏴");
    contentLocale.locales([{ code: "en-US", name: "English", flag: "🇺🇸" }, { code: "hu-HU", name: "Magyar", flag: "🇭🇺" }]);
    const flagsAndText = new switcher.viewModel({ view: "flag-and-text" });
    assert.equal(flagsAndText.optionLabel(flagsAndText.options()[1]), "🇭🇺 Magyar");
    const text = new switcher.viewModel({ view: "text-only" });
    assert.equal(text.optionLabel(text.options()[1]), "Magyar");
    flags.dispose();
    flagsAndText.dispose();
    text.dispose();
    chooser.value("hu-HU");
    await chooser.change();
    assert.equal(contentLocale.current(), "hu-HU");
    assert.equal(window.location.hash, "#/news/123?search=x");
    assert.deepEqual(savedLocales, ["hu-HU"]);
    chooser.dispose();

    session.isSignedIn(true);
    switcherSettings.locale("hu-HU");
    const signedInChooser = new switcher.viewModel();
    assert.equal(signedInChooser.value(), "hu-HU");
    signedInChooser.value("en-US");
    await signedInChooser.change();
    assert.deepEqual(savedLocales, ["hu-HU", "en-US"]);
    assert.equal(switcherSettings.locale(), "en-US");
    assert.equal(contentLocale.current(), "en-US");
    assert.equal(window.location.hash, "#/news/123?search=x");
    signedInChooser.dispose();
    session.isSignedIn(false);

    const operations = [];
    const editor = load("components/contentlocale.js", {
        ko, "models/contentlocale": contentLocale, "models/session": session,
        "models/i18n": { t: key => key },
        "models/http": {
            get(url) { operations.push(url); return Promise.resolve({ ok: true, data: { locale: "hu-HU" } }); },
            post(url, body) { operations.push({ url, body }); return Promise.resolve({ ok: true }); }
        },
        "text!components/contentlocale.html": ""
    });
    const assignment = new editor.viewModel({ entityType: "News", id: "123" });
    await assignment.toggle();
    assert.equal(assignment.value(), "hu-HU");
    assignment.value("");
    await assignment.save();
    assert.equal(operations[0], "/content-locales/News/123");
    assert.equal(operations[1].body.locale, null);
    assert.equal(operations[1].body.entityType, "News");
    assignment.dispose();

    const sections = load("models/sitesections.js", { ko });
    const section = sections.editable({ id: "section-id", title: "Title" });
    assert.equal(sections.toRequest(section).id, "section-id");
    const writes = [];
    const writer = { post(url, body) { writes.push({ url, body }); return Promise.resolve({ ok: true, data: {} }); } };
    const results = {
        create() { return { loaded: response => response, saved: response => response, failure: response => response }; }
    };
    const galleries = load("models/galleries.js", {
        ko, "models/http": writer, "models/results": results,
        "models/paging": {}, "models/i18n": { t: key => key }
    });
    await galleries.saveVersion("gallery-id", {
        title: "Gallery", items: [{ id: "item-id", imageId: "image", thumbnailId: "thumbnail", description: "" }]
    });
    assert.equal(writes[0].body.items[0].id, "item-id");
    const carousels = load("models/carousels.js", { "models/http": writer, "models/results": results });
    await carousels.save("site-id", { intervalSeconds: 5, slides: [{ id: "slide-id", imageId: "image", caption: "", linkUrl: "" }] });
    assert.equal(writes[1].body.slides[0].id, "slide-id");

    const sourceImage = {
        hasAttribute: name => name === "data-content-locale-source",
        getAttribute: name => name === "src" ? "/news/images/abc?contentLocale=hu-HU" : "/news/images/abc"
    };
    const outputImage = {
        src: "/news/images/abc?contentLocale=hu-HU",
        getAttribute() { return this.src; },
        setAttribute(name, value) { this.src = value; }
    };
    const editorElement = {
        set innerHTML(value) {},
        get innerHTML() { return '<img src="' + outputImage.src + '">'; },
        querySelectorAll() { return [outputImage]; }
    };
    let replaced = false;
    const quill = {
        getText: () => "",
        root: { querySelector: () => sourceImage, querySelectorAll: () => [sourceImage] },
        getSemanticHTML: () => '<img src="/news/images/abc?contentLocale=hu-HU">',
        enable() {},
        clipboard: { convert() {} },
        setContents() { replaced = true; }
    };
    ko.bindingHandlers = {};
    ko.utils = { domData: { get: () => ({ quill, isWriting: () => false }) } };
    load("bindings/richeditor.js", {
        ko, quill: function () {}, "models/i18n": {},
        "bindings/richeditorimages": { register() {} }, "bindings/richeditorlinks": { register() {} }
    }, { document: { createElement: () => editorElement } });
    ko.bindingHandlers.richEditor.update({}, () => ({ value: observable('<img src="/news/images/abc">') }));
    assert.equal(replaced, false);
    assert.equal(outputImage.src, "/news/images/abc");

    const batchRequests = [];
    const scopeComputeds = [];
    const scopeKo = {
        ...ko,
        bindingHandlers: {},
        computed(fn) { scopeComputeds.push(fn); fn(); return { dispose() {} }; },
        utils: { domNodeDisposal: { addDisposeCallback() {} } }
    };
    const scopedElement = () => ({
        tagName: "DIV",
        attributes: {},
        setAttribute(name, value) { this.attributes[name] = value; },
        removeAttribute(name) { delete this.attributes[name]; },
        insertBefore(child) { this.warning = child; }
    });
    const warning = () => ({ setAttribute() {} });
    let scopeToken = "first-user";
    load("bindings/contentlocalescope.js", {
        ko: scopeKo, "models/i18n": { t: key => key }, "models/contentlocale": contentLocale,
        "models/session": { accessToken: () => scopeToken },
        "models/http": {
            post(url, body) {
                batchRequests.push({ url, body });
                return Promise.resolve({ ok: true, data: { items: body.items.map(item => ({
                    ...item, status: item.entityId === "3" ? 403 : 200, effectiveLocale: item.entityId === "1" ? "hu-HU" : null
                })) } });
            }
        }
    }, { document: { createElement: warning } });
    const first = scopedElement();
    const second = scopedElement();
    const denied = scopedElement();
    scopeKo.bindingHandlers.contentLocaleScope.init(first, () => ({ entityType: "News", id: "1" }));
    scopeKo.bindingHandlers.contentLocaleScope.init(second, () => ({ entityType: "News", id: "2" }));
    scopeKo.bindingHandlers.contentLocaleScope.init(denied, () => ({ entityType: "News", id: "3" }));
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(batchRequests.length, 1);
    assert.equal(batchRequests[0].body.items.length, 3);
    assert.equal(first.attributes["data-content-locale"], "hu-HU");
    assert.equal(second.attributes["data-content-locale"], "");
    assert.equal(denied.warning.hidden, false);
    scopeToken = "second-user";
    scopeComputeds[0]();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(batchRequests.length, 2);
}

main().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
