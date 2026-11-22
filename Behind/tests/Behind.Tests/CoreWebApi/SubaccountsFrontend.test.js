const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const frontend = path.resolve(__dirname, "../../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(frontend, "libraries/knockout.min.js"));
const httpSource = fs.readFileSync(path.join(frontend, "models/http.js"), "utf8");
const sessionSource = fs.readFileSync(path.join(frontend, "models/session.js"), "utf8");
const source = fs.readFileSync(path.join(frontend, "components/subaccounts.js"), "utf8");

function accessToken(sessionId, nonce) {
    const payload = Buffer.from(JSON.stringify({ session_id: sessionId, nonce })).toString("base64url");
    return "header." + payload + ".signature";
}

async function setup(accounts, primary = true, primaryHasPin = false, linkUnlinkEnabled = false, pending = []) {
    const requests = [];
    const sessions = [];
    const reloads = [];
    const handlers = new Set();
    let session;
    const storage = new Map([["behind.session", JSON.stringify({ accessToken: accessToken("owner-session", "original") })]]);
    const location = { hash: "#/profile", reload: () => reloads.push(true) };
    const window = {
        atob,
        confirm: () => true,
        location,
        addEventListener() {},
        localStorage: {
            getItem: key => storage.get(key) ?? null,
            setItem: (key, value) => storage.set(key, value),
            removeItem: key => storage.delete(key)
        }
    };
    vm.runInNewContext(sessionSource, {
        window,
        define: (_, factory) => { session = factory({ extend: Object.assign }, ko); }
    });
    const start = session.start;
    session.replace = start;
    session.start = tokens => {
        sessions.push(tokens);
        start(tokens);
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
        window,
        define: (_, factory) => {
            const component = factory(ko, http, session, invalidation, { t: key => key }, { label: account => account.displayName }, { password: () => true, magicLink: () => true }, "");
            panel = new component.viewModel({ manager: primary });
        }
    });
    await new Promise(resolve => setImmediate(resolve));
    return { panel, session, sessions, requests, reloads, location, http, invalidation };
}

test("create a child without a sign-in email and clear its PIN on edit", async () => {
    const child = { userId: "child", displayName: "Alex", isChild: true, birthday: "2017-10-12", hasPin: true };
    const { panel, requests } = await setup([child]);
    panel.name(child.displayName);
    panel.child(true);
    panel.birthday(child.birthday);
    panel.pin("9123");
    panel.primaryPin("0101");
    panel.credential("password proof");
    await panel.save();
    assert.deepEqual(JSON.parse(JSON.stringify(requests[0])), {
        url: "/profile/subaccounts",
        body: { displayName: "Alex", isChild: true, birthday: "2017-10-12", pin: "9123", primaryPin: "0101", email: null, proof: { password: "password proof" } }
    });
    assert.equal(panel.pin(), "");
    assert.equal(panel.primaryPin(), "");
    panel.selected(child);
    panel.clearPin(true);
    await panel.save();
    assert.equal(requests.length, 1);
    panel.credential("9123");
    await panel.save();
    assert.equal(requests[1].body.proof.currentPin, "9123");
    assert.equal(requests[1].url, "/profile/subaccounts/child");
    assert.equal(requests[1].body.pin, null);
    assert.equal(requests[1].body.clearPin, true);
    panel.dispose();
});

test("switch to a sibling only after server acceptance and discard the PIN when changing targets", async () => {
    const siblings = [{ userId: "protected", hasPin: true }, { userId: "open", hasPin: false }];
    const { panel, http, session, sessions, requests, reloads, location } = await setup(siblings, false);
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
    assert.equal(session.accessToken(), "new-token");
    assert.equal(location.hash, "#/profile");
    assert.equal(reloads.length, 0);
    panel.dispose();
});

test("accept a successful switch after renewal changes the token for the same session", async () => {
    const child = { userId: "child", hasPin: true };
    const { panel, session, sessions, http, reloads } = await setup([child]);
    panel.selected(child);
    panel.pin("8123");
    const post = http.post;
    http.post = async (url, body) => {
        session.renew({ accessToken: accessToken("owner-session", "renewed") });
        assert.equal(panel.busy(), true);
        assert.equal(body.pin, "8123");
        return post(url, body);
    };
    await panel.switchUser();
    assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
    assert.equal(reloads.length, 0);
    assert.equal(panel.busy(), false);
    panel.dispose();
});

test("discard switch responses after session changes", async () => {
    const child = { userId: "child" };
    for (const supersede of [
        ({ session }) => session.replace({ accessToken: accessToken("other-session", "original") }),
        ({ session }) => session.end(),
        ({ session }) => session.impersonate({ accessToken: accessToken("impersonated-session", "original") }),
        ({ panel, session }) => {
            panel.dispose();
            session.replace({ accessToken: accessToken("other-session", "original") });
        }
    ]) {
        const fixture = await setup([child]);
        const { panel, sessions, http, reloads } = fixture;
        panel.selected(child);
        const post = http.post;
        http.post = async (url, body) => {
            await supersede(fixture);
            return post(url, body);
        };
        await panel.switchUser();
        assert.deepEqual(sessions, []);
        assert.equal(reloads.length, 0);
        panel.dispose();
    }
});

test("complete a successful switch after the account list refreshes or the panel is disposed", async () => {
    const child = { userId: "child" };
    for (const refresh of [
        ({ invalidation }) => invalidation.invalidate(),
        ({ panel }) => panel.dispose()
    ]) {
        const fixture = await setup([child]);
        const { panel, sessions, http, reloads } = fixture;
        panel.selected(child);
        const post = http.post;
        http.post = async (url, body) => {
            await refresh(fixture);
            return post(url, body);
        };
        await panel.switchUser();
        assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
        assert.equal(reloads.length, 0);
        panel.dispose();
    }
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
    const { panel, session, sessions, requests, location, reloads } = await setup([owner], false, true);
    assert.equal(panel.label(owner), "Alex (subaccount.primaryLabel)");
    panel.selected(owner);
    panel.pin("0012");
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/owner/switch");
    assert.equal(requests[0].body.pin, "0012");
    assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
    assert.equal(session.accessToken(), "new-token");
    assert.equal(location.hash, "#/profile");
    assert.equal(reloads.length, 0);
    panel.dispose();
});

test("late session renewal and unauthorized requests cannot affect the replacement session", async () => {
    const { panel, session } = await setup([]);
    panel.dispose();
    const pending = [];
    const $ = {
        extend: Object.assign,
        isEmptyObject: object => Object.keys(object).length === 0,
        ajax: options => ({
            then(resolve, reject) {
                pending.push({
                    options,
                    reply(status, data) {
                        const responseText = JSON.stringify(data);
                        const xhr = { status, responseText, getResponseHeader: () => null };
                        if (status === 200) {
                            resolve(responseText, "success", xhr);
                        } else {
                            reject(xhr);
                        }
                    }
                });
            }
        })
    };
    let http;
    vm.runInNewContext(httpSource, {
        define: (_, factory) => { http = factory($, session, { current: () => null }); }
    });

    const oldSession = http.loadSession();
    const oldProfile = http.get("/profile/details");
    const nextToken = accessToken("new-session", "original");
    session.replace({ accessToken: nextToken });
    const nextSession = http.loadSession();
    assert.equal(http.loadSession(), nextSession);
    assert.equal(pending[2].options.headers.Authorization, "Bearer " + nextToken);

    pending[0].reply(200, { refreshed: { accessToken: accessToken("owner-session", "late") } });
    pending[1].reply(401, {});
    assert.equal((await oldSession).stale, true);
    assert.equal((await oldProfile).stale, true);
    assert.equal(http.loadSession(), nextSession);
    assert.equal(session.accessToken(), nextToken);
    assert.equal(pending.length, 3);

    pending[2].reply(200, { user: { id: "new-user" } });
    assert.equal((await nextSession).data.user.id, "new-user");

    const delayedSession = http.loadSession();
    session.replace({ accessToken: accessToken("third-session", "original") });
    const thirdSession = http.loadSession();
    pending[4].reply(200, { user: { id: "third-user" } });
    await thirdSession;
    pending[3].reply(401, {});
    assert.equal((await delayedSession).stale, true);
    assert.equal(session.isSignedIn(), true);
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
    session.end();
    assert.equal(panel.busy(), false);
    respond({ ok: true, data: [{ userId: "candidate", displayName: "Alex" }] });
    await operation;
    assert.equal(panel.candidates().length, 0);
    assert.equal(panel.linkEnabled(), false);
    panel.dispose();
});

test("linked subaccount PIN changes distinguish the primary PIN from the target PIN", async () => {
    const child = { userId: "child", displayName: "Alex", hasPin: true };
    const withoutPin = { userId: "other-child", displayName: "Sam", hasPin: false };
    const { panel, requests } = await setup([child, withoutPin], true, true);
    assert.equal(panel.proofMethods().includes("primaryPin"), false);
    panel.selected(panel.managementAccounts()[0]);
    assert.equal(panel.proofMethods().includes("primaryPin"), false);
    assert.equal(panel.proofLabel("currentPin"), "subaccount.yourCurrentPin");
    panel.selected(child);
    assert.deepEqual(Array.from(panel.proofMethods()), ["currentPin", "primaryPin", "password", "magicToken"]);
    assert.equal(panel.proofLabel("currentPin"), "subaccount.subaccountCurrentPin");
    assert.equal(panel.proofLabel("primaryPin"), "subaccount.yourCurrentPin");
    panel.clearPin(true);
    panel.proofMethod("primaryPin");
    panel.credential("1234");
    await panel.save();
    assert.equal(requests[0].url, "/profile/subaccounts/child");
    assert.equal(requests[0].body.clearPin, true);
    assert.deepEqual({ ...requests[0].body.proof }, { primaryPin: "1234" });
    panel.selected(withoutPin);
    assert.deepEqual(Array.from(panel.proofMethods()), ["primaryPin", "password", "magicToken"]);
    panel.dispose();
});

test("primary PIN changes send scoped magic proof only on explicit save", async () => {
    const { panel, requests } = await setup([], true, true);
    panel.selected(panel.managementAccounts()[0]);
    panel.pin("007");
    panel.proofMethod("magicToken");
    await panel.requestPinProof();
    assert.equal(requests[0].url, "/profile/subaccounts/owner/pin-proof");
    assert.equal(requests[0].body.operation, "change");
    panel.credential("one-time proof");
    await panel.save();
    assert.equal(requests[1].url, "/profile/subaccounts/owner/pin");
    assert.equal(requests[1].body.proof.magicToken, "one-time proof");
    assert.equal(requests[1].body.pin, "007");
    assert.equal(panel.credential(), "");
    panel.dispose();
});
