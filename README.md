# Behind

Behind is a modular, self-hosted web application built with ASP.NET Core and .NET 11. It brings site and content management together with user and access administration, and can be extended with modules such as Forum, Quotes, Sport, and Markdown Viewer. SQLite and PostgreSQL are supported.

## Features

- Manage sites, news, articles, galleries, downloads, and menus.
- Manage users, roles, access, and audit history.
- Add modules for forums, quotes, sports data, and Markdown documentation.
- Choose SQLite or PostgreSQL for application data.
- Develop and orchestrate services with .NET Aspire.

## Documentation

- [All documentation and available languages](./Behind/docs/README.md)
- [English user manual](./Behind/docs/en-US/user-manual/README.md)
- [English developer documentation](./Behind/docs/en-US/developer/README.md)
- [Getting started](./Behind/docs/en-US/developer/GETTING_STARTED.md)

## Build and test

Requires the .NET 11 SDK. From the repository root:

```sh
cd Behind
dotnet restore Behind.slnx
dotnet build Behind.slnx
dotnet test tests/Behind.Tests/Behind.Tests.csproj
```

The test suite includes disposable PostgreSQL and Valkey integration tests using Testcontainers
and requires a running Docker-compatible engine. To run just these tests:

```sh
dotnet test tests/Behind.Tests/Behind.Tests.csproj --filter "Category=Integration"
```

For rootless Podman, start its API socket (`systemctl --user start podman.socket`)
and set `DOCKER_HOST=unix://$XDG_RUNTIME_DIR/podman/podman.sock`.
The test assembly also discovers `CONTAINER_HOST`, the mounted Podman socket, or
the rootless `$XDG_RUNTIME_DIR/podman/podman.sock` before Testcontainers initializes.
This applies equally to `dotnet test`, Test Explorer and both VS Code coverage
tasks, without a Docker CLI or a Docker alias. An explicit `DOCKER_HOST` wins;
an existing Docker socket keeps standard Docker discovery.
Inside the devcontainer, use `DOCKER_HOST=unix:///run/podman/podman.sock` and
`TESTCONTAINERS_HOST_OVERRIDE=host.containers.internal` so mapped ports are reachable.
The devcontainer now exports these defaults to VS Code as well as terminals.
Reload/rebuild the devcontainer to refresh its environment. If the Podman host's
mapped ports are not reachable through `host.containers.internal`, set
`TESTCONTAINERS_HOST_OVERRIDE` to a reachable engine-host address (not the container
IP). The host environment's override is forwarded when creating the devcontainer;
no machine-specific IP is checked into the repository.
Set `TESTCONTAINERS_RYUK_DISABLED=true` when using rootless Podman; tests still dispose
their containers on completion or assertion failure, but an interrupted test process
may require manual cleanup. Leave Ryuk enabled with Docker.
Podman auto-discovery supplies this setting only when it is absent; explicit
Ryuk and host overrides are preserved. When setting `DOCKER_HOST` yourself outside
the devcontainer, also configure the appropriate Ryuk and mapped-port options.
The PostgreSQL image is configured in
[postgresql.testsettings.json](./Behind/tests/Behind.Tests/postgresql.testsettings.json)
and defaults to `postgres:latest`. An unavailable engine fails the integration tests
rather than silently skipping them. To run without containers, use
`--filter "Category!=Integration"`.
The HybridCache tests use the Valkey image from
[caching.testsettings.json](./Behind/tests/Behind.Tests/caching.testsettings.json).

For configuration and instructions to run the application, see the [Getting Started guide](./Behind/docs/en-US/developer/GETTING_STARTED.md).

## Contributing

This GitHub repository is a mirror. To contribute, reach out to me to request access to the private Git repository.

## Support the project

If Behind is useful to you, consider [sponsoring its development](https://github.com/sponsors/reverse-project).

## License

Behind is licensed under the [GNU General Public License v2.0 or later](./LICENSE).
