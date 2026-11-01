#!/usr/bin/env bash

cd ../src/Core/Behind.Core.Database.Migrator

APP_VERSION=$(dotnet msbuild Behind.Core.Database.Migrator.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-core-database-migrator-manifest
podman manifest add behind-core-database-migrator-manifest reverse-project/behind/core-database-migrator:x64
podman manifest add behind-core-database-migrator-manifest reverse-project/behind/core-database-migrator:arm64
podman manifest push behind-core-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/core-database-migrator:latest
podman manifest push behind-core-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/core-database-migrator:${APP_VERSION}
podman manifest rm behind-core-database-migrator-manifest