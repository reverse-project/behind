const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { test } = require('node:test');
const script = path.resolve(__dirname, '../../../.devcontainer/docker-host.sh');
for (const [name, settings, expected] of [
    ['Docker wins', { DOCKER_HOST: 'tcp://docker:2376', CONTAINER_HOST: 'tcp://podman:2376' }, 'tcp://docker:2376'],
    ['empty Docker wins', { DOCKER_HOST: '', CONTAINER_HOST: 'tcp://podman:2376' }, ''],
    ['container fallback', { CONTAINER_HOST: 'tcp://podman:2376' }, 'tcp://podman:2376'],
    ['empty container wins', { CONTAINER_HOST: '' }, ''],
    ['local fallback', {}, 'unix:///run/podman/podman.sock'],
    ['mounted socket', { CONTAINER_HOST: 'unix:///run/user/1000/podman/podman.sock', XDG_RUNTIME_DIR: '/run/user/1000' }, 'unix:///run/podman/podman.sock']
]) {
    test(name, () => {
        const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'behind-host-'));
        try {
            const env = { ...process.env };
            delete env.DOCKER_HOST;
            delete env.CONTAINER_HOST;
            delete env.XDG_RUNTIME_DIR;
            const output = path.join(directory, 'engine.env');
            const result = spawnSync('bash', [script, output], { env: { ...env, ...settings }, encoding: 'utf8' });
            assert.equal(result.status, 0, result.stderr);
            assert.equal(fs.readFileSync(output, 'utf8'), `DOCKER_HOST=${expected}\n`);
        } finally {
            fs.rmSync(directory, { recursive: true, force: true });
        }
    });
}
