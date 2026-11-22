#!/usr/bin/env bash

REPOSITORY=${1:-ghcr.io/reverse-project/behind}

cd ../src/Sites/Behind.Sites.RPDotHu.Database.Migrator

APP_VERSION=$(dotnet msbuild Behind.Sites.RPDotHu.Database.Migrator.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-sites-rpdothu-database-migrator-manifest
podman manifest add behind-sites-rpdothu-database-migrator-manifest reverse-project/behind/sites/rpdothu-database-migrator:x64
podman manifest add behind-sites-rpdothu-database-migrator-manifest reverse-project/behind/sites/rpdothu-database-migrator:arm64
podman manifest push behind-sites-rpdothu-database-migrator-manifest "docker://${REPOSITORY}/sites/rpdothu-database-migrator:latest"
podman manifest push behind-sites-rpdothu-database-migrator-manifest "docker://${REPOSITORY}/sites/rpdothu-database-migrator:${APP_VERSION}"
podman manifest rm behind-sites-rpdothu-database-migrator-manifest
