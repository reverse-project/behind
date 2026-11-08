#!/usr/bin/env bash

cd ../src/Sites/Behind.Sites.FSE.Database.Migrator

APP_VERSION=$(dotnet msbuild Behind.Sites.FSE.Database.Migrator.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-sites-fse-database-migrator-manifest
podman manifest add behind-sites-fse-database-migrator-manifest reverse-project/behind/sites/fse-database-migrator:x64
podman manifest add behind-sites-fse-database-migrator-manifest reverse-project/behind/sites/fse-database-migrator:arm64
podman manifest push behind-sites-fse-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/sites/fse-database-migrator:latest
podman manifest push behind-sites-fse-database-migrator-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/sites/fse-database-migrator:${APP_VERSION}
podman manifest rm behind-sites-fse-database-migrator-manifest
