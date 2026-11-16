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
Inside the devcontainer, use `DOCKER_HOST=unix:///run/podman/podman.sock` and
`TESTCONTAINERS_HOST_OVERRIDE=host.containers.internal` so mapped ports are reachable.
Set `TESTCONTAINERS_RYUK_DISABLED=true` when using rootless Podman; tests still dispose
their containers on completion or assertion failure, but an interrupted test process
may require manual cleanup. Leave Ryuk enabled with Docker.
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
