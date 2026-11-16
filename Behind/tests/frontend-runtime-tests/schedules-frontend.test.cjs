const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const { test } = require("node:test");
const core = path.resolve(__dirname, "../../src/Core/Behind.Core.WebApi/Frontend");

function load(file) {
    let exported;
    vm.runInNewContext(fs.readFileSync(path.join(core, file), "utf8"), {
        define(factory) { exported = factory(); }
    }, { filename: file });
    return exported;
}

test("cron field calculator composes the standard five fields", () => {
    const fields = load("models/cronfields.js");
    assert.equal(fields.compose({
        minute: { mode: "interval", value: "15" },
        hour: { mode: "every", value: "" },
        dayOfMonth: { mode: "every", value: "" },
        month: { mode: "every", value: "" },
        dayOfWeek: { mode: "range", value: "1-5" }
    }), "*/15 * * * 1-5");
});

test("cron field calculator parses supported forms and leaves custom expressions alone", () => {
    const fields = load("models/cronfields.js");
    assert.deepEqual(JSON.parse(JSON.stringify(fields.parse("0 2 * * 1-5"))), {
        minute: { mode: "specific", value: "0" },
        hour: { mode: "specific", value: "2" },
        dayOfMonth: { mode: "every", value: "" },
        month: { mode: "every", value: "" },
        dayOfWeek: { mode: "range", value: "1-5" }
    });
    assert.deepEqual(JSON.parse(JSON.stringify(fields.parse("*/10 * 1,15 * *"))), {
        minute: { mode: "interval", value: "10" },
        hour: { mode: "every", value: "" },
        dayOfMonth: { mode: "specific", value: "1,15" },
        month: { mode: "every", value: "" },
        dayOfWeek: { mode: "every", value: "" }
    });
    assert.equal(fields.parse("0 0 L * *"), null);
    assert.equal(fields.parse("0 0 * *"), null);
});
