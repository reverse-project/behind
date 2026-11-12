const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const forum = path.resolve(__dirname, "../../src/Forum/Behind.Forum.WebApi/Frontend/modules/forum");
const ko = require(path.join(core, "libraries/knockout.min.js"));
const i18n = { t: key => key };
const session = { isSignedIn: ko.observable(true), hasGrant: () => true };

function load(file, dependencies, browserWindow = { confirm: () => true }) {
    let result;
    vm.runInNewContext(fs.readFileSync(file, "utf8"), {
        define(names, factory) {
            result = factory(...names.map(name => {
                assert.ok(name in dependencies, name);
                return dependencies[name];
            }));
        },
        window: browserWindow,
        FormData,
        Promise
    }, { filename: file });
    return result;
}

const createPager = load(path.join(core, "models/pager.js"), { ko, "models/i18n": i18n });

function model(name, api) {
    const Model = load(path.join(forum, `viewmodels/${name}.js`), {
        ko, "models/session": session, "models/i18n": i18n,
        "models/pager": createPager, "modules/forum/models/forum": api,
        "models/user": { href: id => id ? "#/user/" + encodeURIComponent(id) : null }
    });
    return new Model();
}

function page(items = [], index = 1, total = items.length, pageCount = total ? 1 : 0) {
    return { items, index, total, pageCount };
}

test("forum navigates boards, topics, replies and production pagers", async () => {
    const paths = [];
    const board = { key: "board", entity: { name: "General" } };
    const topic = { id: "topic", title: "Welcome" };
    const api = {
        async get(url) {
            paths.push(url);
            if (url.startsWith("boards")) {
                return page([board], 1, 21, 2);
            }
            if (url.includes("topicId")) {
                return page([{ id: "reply", body: "Hello" }]);
            }
            return page([topic]);
        }
    };
    const view = model("forum", api);
    await view.activate();
    assert.equal(view.boardPager.page(), 1);
    assert.equal(view.boardPager.pageCount(), 2);
    await view.boardPager.next();
    assert.ok(paths.includes("boards?page=2"));
    await view.openBoard(board);
    assert.equal(view.topicPager.page(), 1);
    assert.equal(view.topics()[0], topic);
    await view.openTopic(topic);
    assert.equal(view.replies()[0].body, "Hello");
    await view.back();
    assert.equal(view.topic(), null);
    await view.back();
    assert.equal(view.board(), null);
    assert.equal(view.busy(), false);
});

test("forum submits moderation-safe payloads and shows pending feedback", async () => {
    let saved;
    const view = model("forum", {
        get: async () => page(),
        save: async (url, body) => {
            saved = { url, body };
            return { state: "Pending" };
        }
    });
    view.board({ key: "board" });
    view.title("New topic");
    view.body("Body");
    await view.submit();
    assert.equal(saved.url, "posts");
    assert.equal(saved.body.boardId, "board");
    assert.equal(saved.body.topicId, null);
    assert.equal(saved.body.authorId, undefined);
    assert.equal(saved.body.state, undefined);
    assert.equal(saved.body.isAnonymous, false);
    assert.equal(view.message(), "forum.pending");
    assert.equal(view.body(), "");
});

test("anonymous forum messages hide public profile links while owners retain controls", async () => {
    let saved;
    const view = model("forum", {
        get: async () => page([{ id: "reply", authorId: null, author: "", isAnonymous: true, canEdit: true }]),
        save: async (url, body) => { saved = { url, body }; return { state: "Published" }; }
    });
    view.board({ key: "board" });
    view.topic({ id: "topic" });
    await view.loadReplies();
    assert.equal(view.replies()[0].author, "comments.anonymous");
    assert.equal(view.userHref(view.replies()[0].authorId), null);
    assert.equal(view.replies()[0].canEdit, true);
    view.body("Anonymous reply");
    view.isAnonymous(true);
    await view.submit();
    assert.equal(saved.body.isAnonymous, true);
    assert.equal(view.isAnonymous(), false);
});

test("forum refresh failures cannot become success-shaped feedback", async () => {
    const view = model("forum", {
        get: async () => { throw new Error("Refresh failed"); },
        save: async () => ({ state: "Published" })
    });
    view.board({ key: "board" });
    view.title("Topic");
    view.body("Body");
    await view.submit();
    assert.equal(view.message(), "Refresh failed");
    assert.equal(view.busy(), false);
});

test("manager edits overrides and moderates and restores each resource", async () => {
    const calls = [];
    const board = { key: "board", entity: { name: "General", description: "", moderation: "Pre" } };
    const api = {
        async get(url) {
            if (url === "settings") {
                return { allowBoardOverrides: true, allowUserOverrides: true };
            }
            if (url === "categories") {
                return [];
            }
            if (url.startsWith("boards")) {
                return page([board]);
            }
            if (url.startsWith("manage/posts")) {
                return page([{ id: "post", state: "Pending", body: "Hello" }]);
            }
            return page([{ id: "user", userId: "user", moderation: "Pre", name: "Ada" }]);
        },
        async save(url, body) { calls.push({ url, body }); }
    };
    const view = model("forummanager", api);
    await view.activate();
    view.boards.items()[0].edit();
    assert.equal(view.boardMode(), "Pre");
    await view.saveBoard();
    assert.equal(calls[0].url, "boards/board");
    assert.equal(calls[0].body.moderation, "Pre");
    await view.posts.items()[0].approve();
    assert.ok(calls.some(call => call.url === "posts/post/review" && call.body.state === "Published"));
    await view.posts.items()[0].reject();
    assert.ok(calls.some(call => call.body && call.body.state === "Rejected"));
    view.rules.items()[0].edit();
    view.userMode("Post");
    await view.saveUser();
    assert.ok(calls.some(call => call.url === "manage/users" && call.body.moderation === "Post"));
    await view.boards.items()[0].remove();
    await view.posts.items()[0].restore();
    await view.rules.items()[0].hardDelete();
    assert.ok(calls.some(call => call.url === "boards/board/delete"));
    assert.ok(calls.some(call => call.url === "posts/post/restore"));
    assert.ok(calls.some(call => call.url === "manage/users/user/hard/delete"));
});

test("profile upload and removal update the visible picture", async () => {
    const Profile = load(path.join(core, "viewmodels/profile.js"), {
        ko, "models/auth": {}, "models/session": session,
        "models/user": { href: id => id ? "#/user/" + encodeURIComponent(id) : null,
            label: user => (user.displayName || "user") + "#" + user.id },
        "models/profiles": {},
        "models/about": {}, "models/usersettings": {}, "models/i18n": i18n,
        "models/profilepicture": {
            upload: async () => ({ url: "/profile/images/picture" }),
            remove: async () => undefined
        }
    });
    const view = new Profile();
    await view.choosePicture(null, { target: { files: [{}], value: "avatar.png" } });
    assert.equal(view.pictureUrl(), "/profile/images/picture");
    assert.equal(view.message(), "profile.pictureSaved");
    await view.removePicture();
    assert.equal(view.pictureUrl(), null);
    assert.equal(view.busy(), false);
});

const bbcode = load(path.join(forum, "models/bbcode.js"), {});

test("BBCode renders nested formatting, lists, quotes, code and safe links", () => {
    assert.equal(bbcode.render("[b]Bold [i]italic[/i][/b]"), "<strong>Bold <em>italic</em></strong>");
    assert.equal(bbcode.render("[u]u[/u][s]s[/s]"), "<u>u</u><s>s</s>");
    assert.equal(bbcode.render("[quote]Hello\nworld[/quote]"), "<blockquote>Hello<br>world</blockquote>");
    assert.equal(bbcode.render("[list][*]One[*]Two[/list]"), "<ul><li>One</li><li>Two</li></ul>");
    assert.equal(bbcode.render("[list=1][*]One[/*][/list]"), "<ol><li>One</li></ol>");
    assert.equal(bbcode.render("[code]<b>[b]text[/b]</b>[/code]"), "<pre><code>&lt;b&gt;[b]text[/b]&lt;/b&gt;</code></pre>");
    assert.match(bbcode.render("[url=https://example.com?a=1&b=2]Link[/url]"), /href="https:\/\/example.com\?a=1&amp;b=2"/);
    assert.match(bbcode.render("[url]https://example.com[/url]"), /href="https:\/\/example.com"/);
    assert.match(bbcode.render("[url=mailto:ada@example.com]Email[/url]"), /href="mailto:ada@example.com"/);
    assert.match(bbcode.render("[url=https://example.com]Link[/url]"), /target="_blank" rel="nofollow noopener noreferrer"/);
});

test("BBCode preserves plain text and malformed tags without interpreting HTML or unsafe URLs", () => {
    assert.equal(bbcode.render("Hello\n<script>alert(1)</script>"), "Hello<br>&lt;script&gt;alert(1)&lt;/script&gt;");
    for (const url of ["javascript:alert(1)", "data:text/html,test", "//example.com", 'https://x" onclick="alert(1)', "java\nscript:alert(1)"]) {
        assert.doesNotMatch(bbcode.render(`[url=${url}]Click[/url]`), /<a /);
    }
    assert.equal(bbcode.render("[b]unfinished"), "[b]unfinished");
    assert.equal(bbcode.render("[unknown]text[/unknown]"), "[unknown]text[/unknown]");
    assert.equal(bbcode.render("[lb]b[rb]literal[lb]/b[rb]"), "[b]literal[/b]");
    assert.equal(bbcode.render("[code][lb]/code[rb][/code]"), "<pre><code>[/code]</code></pre>");
    assert.doesNotThrow(() => bbcode.render("[quote]".repeat(10000) + "Text" + "[/quote]".repeat(10000)));
});

test("forum editor modes preserve BBCode and submit only valid lengths", async () => {
    const saved = [];
    const view = model("forum", {
        get: async () => page(),
        save: async (url, body) => { saved.push(body); return { state: "Published" }; }
    });
    view.board({ key: "board", entity: { name: "General" } });
    view.title("Formatting");
    view.body("[b]Hello[/b]");
    view.showSource();
    assert.equal(view.editorMode(), "source");
    view.showPreview();
    assert.equal(view.editorMode(), "preview");
    view.showVisual();
    assert.equal(view.body(), "[b]Hello[/b]");
    await view.submit();
    assert.equal(saved[0].body, "[b]Hello[/b]");
    view.title("Limit");
    view.body("x".repeat(20001));
    assert.equal(Boolean(view.canSubmit()), false);
    await view.submit();
    assert.equal(saved.length, 1);
    assert.equal(view.message(), "forum.tooLong");
    view.body("x".repeat(20000));
    assert.equal(Boolean(view.canSubmit()), true);
    await view.submit();
    assert.equal(saved[1].body.length, 20000);
    view.body(" ");
    await view.submit();
    assert.equal(saved.length, 2);
    assert.equal(view.message(), "forum.required");
});

test("shared rich editor keeps defaults and supports a restricted BBCode toolbar", () => {
    const instances = [];
    let imageAttachments = 0;
    let linkAttachments = 0;
    class Quill {
        constructor(element, options) {
            this.options = options;
            instances.push(this);
        }
        getModule() { return { container: { querySelectorAll: () => [] } }; }
        on() {}
    }
    load(path.join(core, "bindings/richeditor.js"), {
        ko, quill: Quill, "models/i18n": i18n,
        "bindings/richeditorimages": { register() {}, attach() { imageAttachments++; } },
        "bindings/richeditorlinks": { register() {}, attach() { linkAttachments++; } }
    });
    const defaults = { nodeType: 1 };
    ko.bindingHandlers.richEditor.init(defaults, () => ({ value: ko.observable(""), upload() {} }));
    assert.ok(instances[0].options.modules.toolbar.container.some(group => group.includes("image")));
    assert.equal(instances[0].options.formats, undefined);
    assert.equal(linkAttachments, 1);
    const restricted = { nodeType: 1 };
    const toolbar = [["bold", "link"]];
    const formats = ["bold", "link"];
    ko.bindingHandlers.richEditor.init(restricted, () => ({
        value: ko.observable(""), toolbar, formats, linkTargets: false
    }));
    assert.equal(instances[1].options.modules.toolbar.container, toolbar);
    assert.equal(instances[1].options.formats, formats);
    assert.equal(linkAttachments, 1);
    assert.equal(imageAttachments, 2);
});

test("forum links always confirm their destination and only open a protected new tab", () => {
    let confirmed = false;
    const prompts = [];
    const opened = [];
    const disposed = [];
    const listeners = new Map();
    const attributes = new Map([["href", "https://example.com/?a=1&b=2"]]);
    const link = {
        getAttribute: name => attributes.get(name),
        setAttribute: (name, value) => attributes.set(name, value),
        removeAttribute: name => attributes.delete(name)
    };
    const element = {
        contains: node => node === link,
        addEventListener: (name, callback, capture) => {
            assert.equal(capture, true);
            listeners.set(name, callback);
        },
        removeEventListener: (name, callback, capture) => {
            assert.equal(capture, true);
            assert.equal(listeners.get(name), callback);
            listeners.delete(name);
        },
        querySelectorAll: () => [link]
    };
    const bindings = {};
    load(path.join(forum, "bindings/bbcode.js"), {
        ko: {
            bindingHandlers: bindings,
            unwrap: value => value,
            utils: { domNodeDisposal: { addDisposeCallback: (node, callback) => disposed.push(callback) } }
        },
        "modules/forum/models/bbcode": bbcode,
        "models/i18n": i18n,
        "bindings/richeditor": {}
    }, {
        confirm: message => { prompts.push(message); return confirmed; },
        open: (...args) => opened.push(args)
    });
    bindings.bbcode.init(element);
    bindings.bbcode.update(element, () => "[url=https://example.com/?a=1&b=2]Link[/url]");
    assert.equal(attributes.has("href"), false);
    assert.equal(attributes.get("data-forum-url"), "https://example.com/?a=1&b=2");
    assert.equal(attributes.get("role"), "link");
    assert.equal(attributes.get("tabindex"), "0");
    const trigger = (type, options = {}) => {
        let prevented = false;
        listeners.get(type)({
            type, button: 0, target: { closest: () => link },
            preventDefault() { prevented = true; }, stopPropagation() {}, ...options
        });
        return prevented;
    };
    assert.equal(trigger("click"), true);
    assert.equal(opened.length, 0);
    assert.equal(prompts[0], "forum.openLink\n\nhttps://example.com/?a=1&b=2");
    confirmed = true;
    assert.equal(trigger("click", { ctrlKey: true }), true);
    assert.equal(trigger("auxclick", { button: 1 }), true);
    assert.equal(trigger("keydown", { key: "Enter" }), true);
    assert.equal(opened.length, 3);
    for (const args of opened) {
        assert.deepEqual(args, ["https://example.com/?a=1&b=2", "_blank", "noopener,noreferrer"]);
    }
    trigger("auxclick", { button: 2 });
    assert.equal(trigger("keydown", { key: "Tab" }), false);
    trigger("click", { target: { closest: () => null } });
    assert.equal(prompts.length, 4);
    assert.equal(opened.length, 3);
    disposed[0]();
    assert.equal(listeners.size, 0);
});
