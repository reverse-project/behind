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

async function setup(accounts, primary = true, primaryHasPin = false, linkUnlinkEnabled = false, pending = [], profile = {}) {
    const requests = [];
    const sessions = [];
    const reloads = [];
    const confirmations = [];
    const handlers = new Set();
    let session;
    const storage = new Map([["behind.session", JSON.stringify({ accessToken: accessToken("owner-session", "original") })]]);
    const location = { hash: "#/profile", reload: () => reloads.push(true) };
    const window = {
        atob,
        confirm: message => {
            confirmations.push(message);
            return profile.confirmRelease ?? true;
        },
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
            data: url.endsWith("/requests") ? pending : {
                accounts,
                isPrimary: primary,
                primaryUserId: "owner",
                primaryHasPin,
                linkUnlinkEnabled,
                ...profile
            }
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
            panel = new component.viewModel({ manager: profile.manager ?? primary, userId: profile.userId });
        }
    });
    await new Promise(resolve => setImmediate(resolve));
    return { panel, session, sessions, requests, reloads, location, http, invalidation, confirmations };
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

test("filter the server-authorized profiles by Adult and Child and clear a PIN when changing type", async () => {
    const managed = { userId: "managed-without-pin", hasPin: false };
    const child = { userId: "child", isChild: true, hasPin: true };
    const adult = { userId: "adult", isChild: false, hasPin: true };
    const { panel, requests } = await setup([managed], false, false, false, [], {
        switchAccounts: [child, adult],
        isLinked: true
    });

    assert.equal(panel.linked(), true);
    assert.deepEqual(Array.from(panel.profileTypes()), ["adult", "child"]);
    assert.equal(panel.profileType(), "adult");
    assert.deepEqual(Array.from(panel.switchProfiles()), [adult]);
    panel.selected(adult);
    panel.pin("9876");
    panel.profileType("child");
    assert.equal(panel.selected(), undefined);
    assert.equal(panel.pin(), "");
    assert.deepEqual(Array.from(panel.switchProfiles()), [child]);
    panel.selected(child);
    panel.pin("1234");
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/child/switch");
    assert.equal(requests[0].body.pin, "1234");
    panel.dispose();
});

test("the primary account menu switches to an authorized profile without a PIN", async () => {
    const child = { userId: "child-without-pin", displayName: "Child", isChild: true, hasPin: false };
    const { panel, requests, sessions } = await setup([child], true, false, false, [], {
        manager: false,
        switchAccounts: [child],
        isLinked: false
    });

    assert.equal(panel.primary(), true);
    assert.equal(panel.linked(), false);
    assert.equal(panel.manager, false);
    assert.deepEqual(Array.from(panel.accounts()), [child]);
    const template = fs.readFileSync(path.join(frontend, "components/subaccounts.html"), "utf8");
    const switcherCondition = template.match(/data-bind="if: (!manager[^"]+)"/)[1];
    assert.ok(vm.runInNewContext(switcherCondition, panel));
    assert.deepEqual(Array.from(panel.switchProfiles()), [child]);
    panel.selected(child);
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/child-without-pin/switch");
    assert.equal(requests[0].body.pin, null);
    assert.deepEqual(sessions, [{ accessToken: "new-token" }]);
    panel.dispose();
});

test("a switched adult manages children with their own proof and ends the session on release", async () => {
    const child = { userId: "child", displayName: "Child", isChild: true, hasPin: true, canRelease: true };
    const { panel, requests, session, location } = await setup([child], false, false, true, [], {
        manager: true,
        userId: "adult",
        isLinked: true,
        currentHasPin: true,
        canManageChildren: true,
        canBrowseAndRelease: true
    });
    assert.equal(panel.canManageChildren(), true);
    assert.equal(panel.child(), true);
    assert.deepEqual(Array.from(panel.managementAccounts()).map(account => account.userId), ["adult", "child"]);
    panel.selected(child);
    assert.deepEqual(Array.from(panel.proofMethods()), ["primaryPin", "password", "magicToken"]);
    panel.pin("3333");
    panel.proofMethod("primaryPin");
    panel.credential("1111");
    await panel.save();
    assert.equal(requests[0].url, "/profile/subaccounts/child");
    assert.equal(requests[0].body.proof.primaryPin, "1111");
    assert.equal(requests[0].body.isChild, true);

    panel.name("New child");
    panel.pin("4444");
    panel.proofMethod("currentPin");
    panel.credential("1111");
    await panel.save();
    assert.equal(requests[1].url, "/profile/subaccounts");
    assert.equal(requests[1].body.isChild, true);
    assert.equal(requests[1].body.proof.currentPin, "1111");
    assert.equal("primaryPin" in requests[1].body, false);

    panel.selected(child);
    await panel.release();
    assert.equal(requests[2].method, "DELETE");
    assert.equal(session.accessToken(), null);
    assert.equal(location.hash, "#/sign-in");
    panel.dispose();
});

test("a switched adult can enter an authorized child without a PIN", async () => {
    const child = { userId: "child", displayName: "Child", isChild: true, hasPin: false };
    const { panel, requests } = await setup([], false, false, false, [], {
        manager: false,
        isLinked: true,
        canManageChildren: true,
        switchAccounts: [child]
    });
    panel.selected(child);
    await panel.switchUser();
    assert.equal(requests[0].url, "/profile/subaccounts/child/switch");
    assert.equal(requests[0].body.pin, null);
    panel.dispose();
});

test("a linked profile can accept an invitation without management access", async () => {
    const invitation = { userId: "adult", displayName: "Alex", requestId: "second-adult" };
    const { panel, requests } = await setup([], false, false, false, [invitation], {
        manager: true,
        isLinked: true,
        canRespondToInvitations: true
    });
    assert.equal(panel.primary(), false);
    assert.equal(panel.linkEnabled(), false);
    assert.equal(panel.requests()[0], invitation);
    await panel.requestLink(invitation);
    assert.equal(requests.length, 0);
    await panel.confirmLink(invitation);
    assert.equal(requests[0].url, "/profile/subaccounts/requests/second-adult/confirm");
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
    assert.match(read("views/profile.html"), /<subaccount-panel params="manager: true, userId: userId">/);
    assert.match(read("components/subaccounts.html"), /<div data-bind="if: manager && requests\(\)\.length">\s*<section class="mt-4">/);
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

test("a switched-back primary can browse and release without invitation access", async () => {
    const child = { userId: "child", displayName: "Alex", canRelease: true };
    const candidate = { userId: "candidate", displayName: "Pat" };
    const { panel, http, session, requests, confirmations, invalidation, location } =
        await setup([child], true, true, false, [], { canBrowseAndRelease: true, linkUnlinkRequiresDirectSignIn: true });
    const get = http.get;
    http.get = async url => url.includes("/candidates?") ? { ok: true, data: [candidate] } : get(url);
    await panel.search();
    assert.equal(panel.candidates()[0], candidate);
    await panel.requestLink(candidate);
    await panel.confirmLink({ requestId: "invitation" });
    await panel.declineLink({ requestId: "invitation" });
    assert.deepEqual(requests, []);

    panel.selected(child);
    const release = http.delete;
    http.delete = async url => {
        await invalidation.invalidate();
        return release(url);
    };
    await panel.release();
    assert.deepEqual(requests, [{ method: "DELETE", url: "/profile/subaccounts/child" }]);
    assert.match(confirmations[0], /subaccount\.releaseEndsSession/);
    assert.equal(session.isSignedIn(), false);
    assert.equal(location.hash, "#/sign-in");
    assert.equal(panel.canBrowseAndRelease(), false);
    panel.dispose();
});

test("a canceled or failed release keeps the switched-back primary signed in", async () => {
    const child = { userId: "child", canRelease: true };
    for (const confirmRelease of [false, true]) {
        const { panel, http, session, location } = await setup([child], true, true, false, [], {
            canBrowseAndRelease: true, linkUnlinkRequiresDirectSignIn: true, confirmRelease
        });
        panel.selected(child);
        let calls = 0;
        http.delete = async () => {
            calls++;
            return { ok: false, status: 409 };
        };
        await panel.release();
        assert.equal(calls, Number(confirmRelease));
        assert.equal(session.isSignedIn(), true);
        assert.equal(location.hash, "#/profile");
        assert.equal(panel.busy(), false);
        panel.dispose();
    }
});

test("a delayed release response leaves a replacement session signed in", async () => {
    const child = { userId: "child", canRelease: true };
    const { panel, http, session, location } =
        await setup([child], true, true, false, [], { canBrowseAndRelease: true, linkUnlinkRequiresDirectSignIn: true });
    panel.selected(child);
    const replacement = accessToken("other-session", "original");
    http.delete = async () => {
        session.replace({ accessToken: replacement });
        return { ok: true };
    };
    await panel.release();
    assert.equal(session.accessToken(), replacement);
    assert.equal(location.hash, "#/profile");
    panel.dispose();
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

test("a directly signed-in subaccount sets, changes, removes, and sets its PIN again", async () => {
    const profile = { manager: true, userId: ko.observable(""), currentHasPin: false };
    const { panel, requests, location, reloads, sessions } = await setup([], false, false, false, [], profile);
    assert.equal(panel.editAccount(), null);
    profile.userId("self");
    assert.equal(panel.editAccount().userId, "self");
    assert.equal(panel.pinOnly(), true);
    assert.equal(panel.editAccount().hasPin, false);
    assert.equal(panel.proofMethods().length, 0);
    await panel.save();
    await panel.requestPinProof();
    panel.clearPin(true);
    await panel.save();
    assert.equal(requests.length, 0);
    panel.clearPin(false);
    panel.pin("0051");
    profile.currentHasPin = true;
    await panel.save();
    assert.equal(panel.editAccount().hasPin, true);
    assert.equal(panel.message(), "subaccount.ownPinSet");
    assert.equal(panel.pin(), "");
    assert.deepEqual(Array.from(panel.proofMethods()), ["currentPin"]);
    assert.equal(panel.proofLabel("currentPin"), "subaccount.yourCurrentPin");
    await panel.save();
    await panel.requestPinProof();
    panel.pin("0052");
    await panel.save();
    for (const method of ["primaryPin", "password", "magicToken"]) {
        panel.proofMethod(method);
        panel.credential("wrong method");
        await panel.save();
    }
    assert.equal(requests.length, 1);
    panel.proofMethod("currentPin");
    panel.credential("0051");
    await panel.save();
    assert.equal(panel.editAccount().userId, "self");
    assert.equal(panel.message(), "subaccount.ownPinChanged");
    assert.equal(panel.pin(), "");
    assert.equal(panel.credential(), "");
    panel.credential("0052");
    panel.clearPin(true);
    profile.currentHasPin = false;
    await panel.save();
    assert.equal(panel.editAccount().hasPin, false);
    assert.equal(panel.proofMethods().length, 0);
    assert.equal(panel.credential(), "");
    assert.equal(panel.clearPin(), false);
    assert.equal(panel.message(), "subaccount.ownPinRemoved");
    panel.pin("0001");
    profile.currentHasPin = true;
    await panel.save();
    assert.equal(panel.editAccount().hasPin, true);
    assert.equal(panel.message(), "subaccount.ownPinSet");
    assert.deepEqual(JSON.parse(JSON.stringify(requests)), [
        { url: "/profile/subaccounts/self/pin", body: { pin: "0051", clearPin: false } },
        { url: "/profile/subaccounts/self/pin", body: { pin: "0052", clearPin: false, proof: { currentPin: "0051" } } },
        { url: "/profile/subaccounts/self/pin", body: { pin: null, clearPin: true, proof: { currentPin: "0052" } } },
        { url: "/profile/subaccounts/self/pin", body: { pin: "0001", clearPin: false } }
    ]);
    assert.equal(location.hash, "#/profile");
    assert.deepEqual(reloads, []);
    assert.deepEqual(sessions, []);
    panel.dispose();
});

test("an outdated PIN setup form refreshes to require the current PIN", async () => {
    for (const status of [403, 409]) {
        const profile = { manager: true, userId: "self", currentHasPin: false };
        const { panel, http } = await setup([], false, false, false, [], profile);
        http.put = async (url, body) => {
            assert.equal(url, "/profile/subaccounts/self/pin");
            assert.equal(body.proof, undefined);
            profile.currentHasPin = true;
            return { ok: false, status };
        };
        panel.pin("0051");
        await panel.save();
        assert.equal(panel.editAccount().hasPin, true);
        assert.deepEqual(Array.from(panel.proofMethods()), ["currentPin"]);
        assert.equal(panel.pin(), "");
        assert.equal(panel.message(), "subaccount.ownPinSetupRejected");
        panel.dispose();
    }
});

test("a switched subaccount always targets itself and ignores stale PIN responses", async () => {
    const sibling = { userId: "sibling", hasPin: true };
    const profile = { manager: true, userId: "self", currentHasPin: true };
    const { panel, http, session } = await setup([sibling], false, true, false, [], profile);
    panel.selected(sibling);
    panel.clearPin(true);
    panel.credential("0050");
    http.put = async (url, body) => {
        assert.equal(url, "/profile/subaccounts/self/pin");
        assert.deepEqual({ ...body.proof }, { currentPin: "0050" });
        return { ok: false, status: 403 };
    };
    await panel.save();
    assert.equal(panel.message(), "subaccount.currentPinRejected");
    assert.equal(panel.editAccount().userId, "self");

    let reply;
    http.put = () => new Promise(resolve => { reply = resolve; });
    const pending = panel.save();
    session.replace({ accessToken: accessToken("replacement", "initial") });
    reply({ ok: true });
    await pending;
    assert.equal(panel.editAccount(), null);
    assert.equal(panel.credential(), "");
    assert.equal(panel.pin(), "");
    assert.equal(panel.busy(), false);
    panel.dispose();
});

test("a previous session's PIN success message does not appear after a slow profile refresh", async () => {
    const profile = { manager: true, userId: "self", currentHasPin: true };
    const { panel, http, session } = await setup([], false, false, false, [], profile);
    let reply;
    http.get = () => new Promise(resolve => { reply = resolve; });
    panel.clearPin(true);
    panel.credential("1928");
    const saving = panel.save();
    await new Promise(resolve => setImmediate(resolve));
    session.end();
    reply({ ok: true, data: { isPrimary: false, primaryUserId: "owner", currentHasPin: false, accounts: [] } });
    await saving;
    assert.equal(panel.message(), "");
    assert.equal(panel.editAccount(), null);
    assert.equal(panel.busy(), false);
    panel.dispose();
});
