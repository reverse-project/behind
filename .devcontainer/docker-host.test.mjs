import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const directory = fileURLToPath(new URL('.', import.meta.url));
const script = join(directory, 'docker-host.sh');
const config = JSON.parse(readFileSync(join(directory, 'devcontainer.json'), 'utf8'));

test('devcontainer uses the host-generated environment file', () => {
  assert.deepEqual(config.initializeCommand, [
    'bash', '${localWorkspaceFolder}/.devcontainer/docker-host.sh',
    '${localWorkspaceFolder}/.devcontainer/docker-host.env'
  ]);
  assert.deepEqual(config.runArgs, ['--env-file', '${localWorkspaceFolder}/.devcontainer/docker-host.env']);
  assert.equal(config.remoteEnv.DOCKER_HOST, '${containerEnv:DOCKER_HOST}');
  assert.ok(config.mounts.includes('source=${localEnv:XDG_RUNTIME_DIR}/podman/podman.sock,target=/run/podman/podman.sock,type=bind'));
  assert.equal(config.remoteEnv.TESTCONTAINERS_RYUK_DISABLED, '${localEnv:TESTCONTAINERS_RYUK_DISABLED:true}');
  assert.equal(config.remoteEnv.TESTCONTAINERS_HOST_OVERRIDE, '${localEnv:TESTCONTAINERS_HOST_OVERRIDE:host.containers.internal}');
});

for (const [name, endpoint, runtime, expected] of [
  ['mounted socket', 'unix:///run/user/1000/podman/podman.sock', '/run/user/1000', 'unix:///run/podman/podman.sock'],
  ['runtime with trailing slash', 'unix:///run/user/1000//podman/podman.sock', '/run/user/1000/', 'unix:///run/podman/podman.sock'],
  ['unset endpoint', undefined, '/run/user/1000', 'unix:///run/podman/podman.sock'],
  ['empty endpoint', '', '/run/user/1000', ''],
  ['other socket', 'unix:///forwarded/podman.sock', '/run/user/1000', 'unix:///forwarded/podman.sock'],
  ['TCP endpoint', 'tcp://engine.example:2376', '/run/user/1000', 'tcp://engine.example:2376'],
  ['HTTPS endpoint', 'https://engine.example:2376', '/run/user/1000', 'https://engine.example:2376'],
  ['SSH endpoint', 'ssh://user@engine.example/run/podman/podman.sock', '/run/user/1000', 'ssh://user@engine.example/run/podman/podman.sock']
]) {
  test(name, () => {
    const temp = mkdtempSync(join(tmpdir(), 'behind-docker-host-'));
    try {
      const file = join(temp, 'docker-host.env');
      const env = { ...process.env, XDG_RUNTIME_DIR: runtime };
      delete env.DOCKER_HOST;
      if (endpoint !== undefined) {
        env.DOCKER_HOST = endpoint;
      }
      execFileSync('bash', [script, file], { env });
      assert.equal(readFileSync(file, 'utf8'), `DOCKER_HOST=${expected}\n`);
    } finally {
      rmSync(temp, { recursive: true, force: true });
    }
  });
}

test('rejects an endpoint that could add an environment entry', () => {
  const temp = mkdtempSync(join(tmpdir(), 'behind-docker-host-'));
  try {
    const result = spawnSync('bash', [script, join(temp, 'docker-host.env')], {
      env: { ...process.env, DOCKER_HOST: 'tcp://engine.example:2376\nOTHER=value' }
    });
    assert.equal(result.status, 1);
  } finally {
    rmSync(temp, { recursive: true, force: true });
  }
});
