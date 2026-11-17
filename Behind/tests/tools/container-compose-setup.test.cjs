const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const setup = path.resolve(__dirname, "../../examples/container-compose/setup.sh");

for (const state of ["missing", "file", "directory", "generator-failure", "no-generated-file"]) {
    test(`compose setup handles ${state}`, () => {
        const directory = fs.mkdtempSync(path.join(os.tmpdir(), "behind-compose-setup-"));
        try {
            fs.copyFileSync(setup, path.join(directory, "setup.sh"));
            fs.mkdirSync(path.join(directory, "config"));
            const caching = path.join(directory, "config/caching.json");
            const original = '{"Caching":{"Provider":"RedisCompatible","Key":"Custom"}}';
            if (state === "file") {
                fs.writeFileSync(caching, original);
            }
            if (state === "directory") {
                fs.mkdirSync(caching);
                fs.writeFileSync(path.join(caching, "preserved"), "keep");
            }
            const engine = path.join(directory, "engine");
            fs.writeFileSync(engine, `#!/usr/bin/env bash
set -eu
printf '%s\\n' "$*" > called
if [[ "$FIXTURE_STATE" == generator-failure ]]; then
    exit 7
fi
if [[ "$FIXTURE_STATE" != no-generated-file && ! -e config/caching.json ]]; then
    printf '%s' '{"Caching":{"Provider":"Memory","Key":""}}' > config/caching.json
fi
`, { mode: 0o755 });
            const result = spawnSync("bash", [path.join(directory, "setup.sh"), engine], {
                cwd: os.tmpdir(), env: { ...process.env, FIXTURE_STATE: state }, encoding: "utf8"
            });
            if (state === "directory") {
                assert.equal(result.status, 1);
                assert.match(result.stderr, /must be a regular file/);
                assert.equal(fs.existsSync(path.join(directory, "called")), false);
                assert.equal(fs.readFileSync(path.join(caching, "preserved"), "utf8"), "keep");
            } else {
                assert.equal(fs.readFileSync(path.join(directory, "called"), "utf8"), "compose run --rm core-tools\n");
                assert.equal(result.status, state === "generator-failure" ? 7 : state === "no-generated-file" ? 1 : 0);
                if (result.status === 0) {
                    assert.equal(fs.statSync(caching).isFile(), true);
                    if (state === "file") {
                        assert.equal(fs.readFileSync(caching, "utf8"), original);
                    }
                }
            }
        } finally {
            fs.rmSync(directory, { recursive: true, force: true });
        }
    });
}
