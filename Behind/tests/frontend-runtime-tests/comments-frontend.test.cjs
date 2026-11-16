const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key };
const user = { href: id => id ? "#/user/" + encodeURIComponent(id) : null };

function load(file, dependencies) {
    let result;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        Promise,
        window: { confirm: () => true }
    });
    return result;
}

test("news comment create and edit preserve anonymity and use only visible author links", async () => {
    const calls = [];
    const signedIn = ko.observable(true);
    let item = {
        id: "comment", authorId: null, author: "", content: "Hidden", isAnonymous: true,
        canEdit: true, canDelete: true, createdAt: "now", updatedAt: null
    };
    const NewsArticle = load("viewmodels/newsarticle.js", {
        ko, "models/session": { isSignedIn: signedIn }, "models/i18n": i18n, "models/user": user,
        "models/pager": load("models/pager.js", { ko, "models/i18n": i18n }),
        "models/comments": { settings: async () => ({ news: true }) },
        "models/newsmeta": { author: value => value, formatDate: value => value, stamp: () => "", created: () => "" },
        "models/news": {
            get: async () => ({ ok: true, message: "", data: { author: "Article author", authorId: "article-author" } }),
            comments: async () => ({ ok: true, data: { items: [item], page: 1, total: 1, pageCount: 1 } }),
            addComment: async (...args) => { calls.push(args); return { ok: true }; },
            editComment: async (...args) => { calls.push(args); return { ok: true }; }
        }
    });
    const view = new NewsArticle({ params: { id: "news" } });
    await view.activate();
    assert.equal(view.authorHref(), "#/user/article-author");
    let row = view.comments()[0];
    assert.equal(row.author, "comments.anonymous");
    assert.equal(row.authorHref, null);
    assert.equal(row.canEdit, true);
    row.startEdit();
    assert.equal(row.draftAnonymous(), true);
    row.draft("Public");
    row.draftAnonymous(false);
    await row.saveEdit();
    assert.deepEqual(Array.from(calls[0]), ["comment", "Public", false]);
    view.newComment(" New hidden ");
    view.isAnonymous(true);
    await view.post();
    assert.deepEqual(Array.from(calls[1]), ["news", "New hidden", true]);
    assert.equal(view.isAnonymous(), false);
    item = { ...item, authorId: "real", author: "Real author" };
    await view.refreshComments();
    row = view.comments()[0];
    assert.equal(row.author, "Real author");
    assert.equal(row.authorHref, "#/user/real");
    signedIn(false);
    view.newComment("Guest");
    assert.equal(view.canPost(), false);
});
