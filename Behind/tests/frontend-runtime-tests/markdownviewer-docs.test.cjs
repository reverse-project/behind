const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const viewer = path.resolve(__dirname, "../../src/MarkdownViewer/Behind.MarkdownViewer.WebApi/Frontend/modules/markdownviewer");
const ko = require(path.join(core, "libraries/knockout.min.js"));

function createView(list, page) {
    const locale = ko.observable("en-US");
    const dependencies = {
        ko,
        "models/i18n": { locale, t: key => key },
        "modules/markdownviewer/models/markdownviewer": {
            list, page, documentRoute: path => "#/docs/" + path
        }
    };
    let Docs;
    vm.runInNewContext(fs.readFileSync(path.join(viewer, "viewmodels/docs.js"), "utf8"), {
        define(names, factory) { Docs = factory(...names.map(name => dependencies[name])); },
        Promise
    });
    return { view: new Docs(), locale };
}

function response(configured, errors, entries = []) {
    return { ok: true, message: "", data: {
        configured, errors, entries, sections: [], locale: "en-US",
        title: "Docs", home: null, defaultDocument: "README.md"
    } };
}

test("the viewer distinguishes missing and unset source directories", async () => {
    let result = response(false, ["source-missing"]);
    const { view, locale } = createView(() => Promise.resolve(result), () => Promise.resolve());
    await view.activate();
    assert.equal(view.configured(), false);
    assert.equal(view.sourceMissing(), true);

    result = response(false, ["source-not-configured"]);
    locale("hu-HU");
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(view.configured(), false);
    assert.equal(view.sourceMissing(), false);

    result = response(true, []);
    locale("en-US");
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(view.configured(), true);
    assert.equal(view.sourceMissing(), false);
});

test("stale locale responses cannot replace the current source status", async () => {
    const pending = [];
    const { view, locale } = createView(() => new Promise(resolve => pending.push(resolve)), () => Promise.resolve());
    const first = view.activate();
    locale("hu-HU");
    pending[1](response(false, ["source-missing"]));
    await new Promise(resolve => setImmediate(resolve));
    pending[0](response(false, ["source-not-configured"]));
    await first;
    assert.equal(view.sourceMissing(), true);
});

test("a stale page cannot reappear after the source becomes unavailable", async () => {
    let finishPage;
    let result = response(true, [], [{ path: "README.md", title: "Home" }]);
    const { view, locale } = createView(
        () => Promise.resolve(result),
        () => new Promise(resolve => { finishPage = resolve; })
    );
    const first = view.activate();
    await new Promise(resolve => setImmediate(resolve));
    result = response(false, ["source-missing"]);
    locale("hu-HU");
    await new Promise(resolve => setImmediate(resolve));
    finishPage({ ok: true, message: "", data: { path: "README.md", html: "stale" } });
    await first;
    assert.equal(view.current(), null);
    assert.equal(view.sourceMissing(), true);
});

test("an older server response without errors still shows the unset warning", async () => {
    const result = response(false, []);
    delete result.data.errors;
    const { view } = createView(() => Promise.resolve(result), () => Promise.resolve());
    await view.activate();
    assert.equal(view.configured(), false);
    assert.equal(view.sourceMissing(), false);
});
