const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const frontend = path.resolve(__dirname, "../../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(frontend, "libraries/knockout.min.js"));
const source = fs.readFileSync(path.join(frontend, "components/subaccounts.js"), "utf8");

async function setup(accounts, primary = true, primaryHasPin = false, linkUnlinkEnabled = false, pending = []) {
    const requests = [];
    const sessions = [];
    const reloads = [];
    const handlers = new Set();
    const session = {
        accessToken: ko.observable("owner-token"),
        isImpersonating: ko.observable(false),
        start: tokens => sessions.push(tokens)
    };
    const http = {
        get: async url => ({
            ok: true,
            data: url.endsWith("/requests") ? pending : { accounts, isPrimary: primary, primaryUserId: "owner", primaryHasPin, linkUnlinkEnabled }
        }),
        delete: async url => {
            requests.push({ url, method: "DELETE" });
            return { ok: true };
        },
        put: async (url, body) => {
            requests.push({ url, body });
            return { ok: true };
        },
        post: async (url, body) => {
            requests.push({ url, body });
            return { ok: true, data: { accessToken: "new-token" } };
        }
    };
    const invalidation = {
        subscribe: (_, handler) => {
            handlers.add(handler);
            return { dispose: () => handlers.delete(handler) };
        },
        invalidate: () => Promise.all([...handlers].map(handler => handler()))
    };
    let panel;
    vm.runInNewContext(source, {
        window: { confirm: () => true, location: { reload: () => reloads.push(true) } },
        define: (_, factory) => {
            const component = factory(ko, http, session, invalidation, { t: key => key }, { label: account => account.displayName }, "");
            panel = new component.viewModel({ manager: primary });
        }
    });
    await new Promise(resolve => setImmediate(resolve));
    return { panel, session, sessions, requests, reloads, http };
}

test("create a child without a sign-in email and clear its PIN on edit", async () => {
    const child = { userId: "child", displayName: "Alex", isChild: true, birthday: "2017-10-12", hasPin: true };
    const { panel, requests } = await setup([child]);
    panel.name(child.displayName);
    panel.child(true);
    panel.birthday(child.birthday);
    panel.pin("9123");
    panel.primaryPin("0101");
    await panel.save();
    assert.deepEqual(JSON.parse(JSON.stringify(requests[0])), {
        url: "/profile/subaccounts",
        body: { displayName: "Alex", isChild: true, birthday: "2017-10-12", pin: "9123", primaryPin: "0101", email: null }
    });
    assert.equal(panel.pin(), "");
    assert.equal(panel.primaryPin(), "");
    panel.selected(child);
    panel.clearPin(true);
    await panel.save();
    assert.equal(requests[1].url, "/profile/subaccounts/child");
    assert.equal(requests[1].body.pin, null);
    assert.equal(requests[1].body.clearPin, true);
    panel.dispose();
});

test("switch to a sibling only after server acceptance and discard the PIN when changing targets", async () => {
    const siblings = [{ userId: "protected", hasPin: true }, { userId: "open", hasPin: false }];
    const { panel, http, sessions, requests, reloads } = await setup(siblings, false);
    assert.equal(panel.primary(), false);
    assert.equal(panel.linked(), true);
    panel.selected(siblings[0]);
    panel.pin("0001");
    const post = http.post;
    http.post = async () => ({ ok: false, status: 403 });
    await panel.switchUser();
    assert.deepEqual(sessions, []);
    assert.equal(panel.message(), "subaccount.denied");
    panel.selected(siblings[1]);
    assert.equal(panel.pin(), "");
    http.post = post;
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/open/switch");
    assert.equal(requests[0].body.pin, null);
    assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
    assert.equal(reloads.length, 1);
    panel.dispose();
});

test("skip setup or preserve an existing primary PIN when creating an account", async () => {
    for (const primaryHasPin of [false, true]) {
        const { panel, requests } = await setup([], true, primaryHasPin);
        assert.equal(panel.primaryHasPin(), primaryHasPin);
        panel.primaryPin("9876");
        panel.skipPrimaryPin(!primaryHasPin);
        panel.name("Alex");
        await panel.save();
        assert.equal("primaryPin" in requests[0].body, false);
        assert.equal(panel.primaryPin(), "");
        assert.equal(panel.skipPrimaryPin(), false);
        panel.dispose();
    }
});

test("switch back to the primary account with its PIN", async () => {
    const owner = { userId: "owner", hasPin: true, displayName: "Alex" };
    const { panel, sessions, requests } = await setup([owner], false, true);
    assert.equal(panel.label(owner), "Alex (subaccount.primaryLabel)");
    panel.selected(owner);
    panel.pin("0012");
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/owner/switch");
    assert.equal(requests[0].body.pin, "0012");
    assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
    panel.dispose();
});

test("the common account menu and profile expose localized subaccount controls", () => {
    const read = name => fs.readFileSync(path.join(frontend, name), "utf8");
    assert.match(read("components/menuitems.html"), /attributes.href === '#\/profile' && \$component.isDropdown/);
    assert.match(read("views/profile.html"), /<subaccount-panel params="manager: true">/);
    const labels = (source + read("components/subaccounts.html")).match(/subaccount\.[a-zA-Z]+/g);
    for (const locale of ["en-US", "hu-HU"]) {
        const messages = JSON.parse(read("locales/" + locale + ".json")).messages;
        for (const label of labels) {
            assert.ok(messages[label], locale + " " + label);
        }
    }
});

test("browse, request, and respond to an identified primary account", async () => {
    const parent = { userId: "proposer", displayName: "Morgan", requestId: "invitation" };
    const candidate = { userId: "candidate", displayName: "Jo" };
    const { panel, http, requests } = await setup([], true, false, true, [parent]);
    assert.equal(panel.requests()[0], parent);
    const get = http.get;
    http.get = url => {
        if (url.includes("/candidates?")) {
            if (url.endsWith("&page=2&pageSize=10")) {
                return Promise.resolve({ ok: true, data: [{ userId: "page2", displayName: "More" }] });
            }
            assert.equal(url, "/profile/subaccounts/candidates?query=Jo%20%26&page=1&pageSize=10");
            return Promise.resolve({ ok: true, data: Array(10).fill(candidate) });
        }
        return get(url);
    };
    panel.query("Jo &");
    await panel.search();
    assert.equal(panel.candidates()[0], candidate);
    assert.equal(panel.moreCandidates(), true);
    await panel.loadMore();
    assert.equal(panel.candidates()[10].userId, "page2");
    assert.equal(panel.moreCandidates(), false);
    await panel.requestLink(candidate);
    await panel.declineLink(parent);
    await panel.confirmLink(parent);
    assert.deepEqual(requests.map(request => request.url), [
        "/profile/subaccounts/candidate/request",
        "/profile/subaccounts/requests/invitation/decline",
        "/profile/subaccounts/requests/invitation/confirm"
    ]);
    panel.dispose();
});

test("release requires an address and enabled linking", async () => {
    const recovered = { userId: "recover", email: "recover@example.org", canRelease: true };
    const child = { userId: "child", email: null };
    const { panel, requests } = await setup([child, recovered], true, true, true);
    panel.selected(child);
    await panel.release();
    panel.selected({ userId: "child", email: "pending@example.org", canRelease: false });
    await panel.release();
    panel.selected(recovered);
    await panel.release();
    assert.deepEqual(requests, [{ method: "DELETE", url: "/profile/subaccounts/recover" }]);
    panel.dispose();

    const disabled = await setup([recovered]);
    disabled.panel.selected(recovered);
    await disabled.panel.release();
    await disabled.panel.confirmLink({ requestId: "pending" });
    assert.deepEqual(disabled.requests, []);
    disabled.panel.dispose();
});

test("signing out during a search clears busy state and discards private results", async () => {
    const { panel, session, http } = await setup([], true, false, true);
    let respond;
    http.get = () => new Promise(resolve => { respond = resolve; });
    const operation = panel.search();
    session.accessToken(null);
    assert.equal(panel.busy(), false);
    respond({ ok: true, data: [{ userId: "candidate", displayName: "Alex" }] });
    await operation;
    assert.equal(panel.candidates().length, 0);
    assert.equal(panel.linkEnabled(), false);
    panel.dispose();
});
