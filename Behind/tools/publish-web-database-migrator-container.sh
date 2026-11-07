#!/usr/bin/env bash

cd ../src/Web/Behind.Web.Database.Migrator

APP_VERSION=$(dotnet msbuild Behind.Web.Database.Migrator.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-web-database-migrator-manifest
podman manifest add behind-web-database-migrator-manifest reverse-project/behind/web-database-migrator:x64
podman manifest add behind-web-database-migrator-manifest reverse-project/behind/web-database-migrator:arm64
podman manifest push behind-web-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/web-database-migrator:latest
podman manifest push behind-web-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/web-database-migrator:${APP_VERSION}
podman manifest rm behind-web-database-migrator-manifest