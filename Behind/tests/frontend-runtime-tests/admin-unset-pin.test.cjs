const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");

const frontend = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");
const ko = require(path.join(frontend, "libraries/knockout.min.js"));

test("administrators confirm PIN removal and the refreshed row hides the action", async () => {
    let confirmed = false;
    let hasPin = true;
    let loads = 0;
    const requests = [];
    const dependencies = {
        ko,
        "models/auth": {
            async loadUsers() {
                loads++;
                return { ok: true, data: { letters: [], users: { items: [
                    { id: "target", emails: [], roles: [], permissions: { canUnsetPin: hasPin } }
                ] } } };
            },
            async unsetUserPin(id) {
                requests.push(id);
                hasPin = false;
                return { ok: true, message: "removed" };
            }
        },
        "models/session": { hasGrant: () => true },
        "models/pager": () => ({ page: () => 1, isOutOfRange: () => false, update() {} }),
        "models/letterpager": () => ({ selected: () => "", update() {} }),
        "models/user": { label: () => "Target", href: () => "" },
        "models/i18n": { t: (key, args) => [key, args] },
        "viewmodels/useraccess": function () {},
        "viewmodels/serviceusers": function () {
            this.canManage = ko.observable(false);
            this.load = () => Promise.resolve();
            this.invalidate = () => this.canManage(false);
            this.dispose = this.invalidate;
        },
        "viewmodels/serviceusers": function () {
            this.canManage = ko.observable(false);
            this.load = () => Promise.resolve();
            this.invalidate = () => this.canManage(false);
            this.dispose = this.invalidate;
        }
    };
    let Users;
    vm.runInNewContext(fs.readFileSync(path.join(frontend, "viewmodels/users.js"), "utf8"), {
        define(names, factory) { Users = factory(...names.map(name => dependencies[name])); },
        window: { confirm([key, [name]]) {
            assert.equal(key, "users.confirmUnsetPin");
            assert.equal(name, "Target");
            return confirmed;
        } }
    });
    const view = new Users();
    await view.refresh();
    await view.users()[0].unsetPin();
    assert.deepEqual(requests, []);
    confirmed = true;
    await view.users()[0].unsetPin();
    assert.deepEqual(requests, ["target"]);
    assert.equal(loads, 2);
    assert.equal(view.users()[0].canUnsetPin, false);
    assert.equal(view.message(), "removed");
});
