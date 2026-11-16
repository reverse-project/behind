const assert = require("node:assert/strict");
const { execFileSync } = require("node:child_process");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { test } = require("node:test");

const tools = path.resolve(__dirname, "../../tools");
const images = {
    "publish-core-database-migrator-container.sh": "core-database-migrator",
    "publish-core-tools-container.sh": "core-tools",
    "publish-sites-fse-container.sh": "sites/fse",
    "publish-sites-fse-database-migrator-container.sh": "sites/fse-database-migrator",
    "publish-sites-rpdothu-container.sh": "sites/rpdothu",
    "publish-web-container.sh": "web",
    "publish-web-database-migrator-container.sh": "web-database-migrator"
};
const defaultRepository = "ghcr.io/reverse-project/behind";
const customRepository = "registry.example:5000/example/behind";

function runScript(script, repository) {
    const directory = fs.mkdtempSync(path.join(os.tmpdir(), "behind-publish-test-"));
    try {
        const log = path.join(directory, "commands");
        for (const command of ["dotnet", "podman"]) {
            const executable = path.join(directory, command);
            fs.writeFileSync(executable,
                `#!/usr/bin/env bash\nprintf "%s\\n" "${command} $*" >> "$COMMAND_LOG"\n` +
                'if [ "$1" = msbuild ]; then printf "1.2.3\\n"; fi\n',
                { mode: 0o755 });
        }
        const args = [path.join(tools, script)];
        if (repository !== undefined) {
            args.push(repository);
        }
        execFileSync("bash", args, {
            cwd: tools,
            env: { ...process.env, PATH: `${directory}${path.delimiter}${process.env.PATH}`, COMMAND_LOG: log },
            stdio: "pipe"
        });
        return fs.readFileSync(log, "utf8").trimEnd().split("\n");
    } finally {
        fs.rmSync(directory, { recursive: true, force: true });
    }
}

function assertImageCommands(commands, image, repository) {
    const manifest = `behind-${image.replaceAll("/", "-")}-manifest`;
    for (const architecture of ["x64", "arm64"]) {
        assert.ok(commands.includes(`podman manifest add ${manifest} reverse-project/behind/${image}:${architecture}`));
    }
    for (const tag of ["latest", "1.2.3"]) {
        assert.ok(commands.includes(`podman manifest push ${manifest} docker://${repository}/${image}:${tag}`));
    }
}

test("individual publish scripts use default and custom repositories", () => {
    for (const [script, image] of Object.entries(images)) {
        for (const [argument, repository] of [[undefined, defaultRepository], [customRepository, customRepository]]) {
            const commands = runScript(script, argument);
            assertImageCommands(commands, image, repository);
            assert.equal(commands.filter(command => command.startsWith("dotnet publish ")).length, 2);
            assert.equal(commands.filter(command => command.startsWith("podman manifest push ")).length, 2);
        }
    }
});

test("publish-all forwards the repository", () => {
    for (const [argument, repository] of [[undefined, defaultRepository], [customRepository, customRepository]]) {
        const commands = runScript("publish-all.sh", argument);
        for (const image of Object.values(images)) {
            assertImageCommands(commands, image, repository);
        }
        assert.equal(commands.filter(command => command.startsWith("podman manifest push ")).length, 2 * Object.keys(images).length);
    }
});
