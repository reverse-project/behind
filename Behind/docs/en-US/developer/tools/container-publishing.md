# Container Publishing Scripts

The scripts in the repository's [`tools`](../../../../tools) directory build and publish the application's container images. The default destination prefix is `ghcr.io/reverse-project/behind`.

## Requirements

- The .NET SDK required by the projects (currently .NET 11)
- Podman
- Access to and authentication with the destination container registry

## Running the scripts

Run an individual script from the repository's `tools` directory:

```bash
cd tools
bash publish-web-container.sh
```

Pass a repository prefix as the first argument to use a different destination, for example:

```bash
bash publish-web-container.sh git.prod.zwebnt.hu/reverse-project/behind
bash publish-all.sh git.prod.zwebnt.hu/reverse-project/behind
```

The prefix must identify a container registry and repository namespace, without a URL scheme or trailing slash. Use a registry that supports container pushes; GitHub's container registry uses `ghcr.io`, not `github.com`.

Each script publishes Linux x64 and ARM64 images, then pushes a multi-architecture manifest with `latest` and the project version tags. The `publish-all.sh` script runs all `publish-*.sh` scripts sequentially.

| Script | Image |
|---|---|
| [`publish-core-tools-container.sh`](../../../../tools/publish-core-tools-container.sh) | Core Tools CLI |
| [`publish-core-database-migrator-container.sh`](../../../../tools/publish-core-database-migrator-container.sh) | Core database migrator |
| [`publish-web-container.sh`](../../../../tools/publish-web-container.sh) | Web application |
| [`publish-web-database-migrator-container.sh`](../../../../tools/publish-web-database-migrator-container.sh) | Web database migrator |
| [`publish-sites-fse-container.sh`](../../../../tools/publish-sites-fse-container.sh) | FSE site |
| [`publish-sites-fse-database-migrator-container.sh`](../../../../tools/publish-sites-fse-database-migrator-container.sh) | FSE database migrator |
| [`publish-sites-rpdothu-container.sh`](../../../../tools/publish-sites-rpdothu-container.sh) | RP.hu site |
| [`publish-all.sh`](../../../../tools/publish-all.sh) | Runs all publishing scripts |
