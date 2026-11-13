#!/usr/bin/env bash

REPOSITORY=${1:-ghcr.io/reverse-project/behind}

cd ../src/Sites/Behind.Sites.FSE

APP_VERSION=$(dotnet msbuild Behind.Sites.FSE.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-sites-fse-manifest
podman manifest add behind-sites-fse-manifest reverse-project/behind/sites/fse:x64
podman manifest add behind-sites-fse-manifest reverse-project/behind/sites/fse:arm64
podman manifest push behind-sites-fse-manifest "docker://${REPOSITORY}/sites/fse:latest"
podman manifest push behind-sites-fse-manifest "docker://${REPOSITORY}/sites/fse:${APP_VERSION}"
podman manifest rm behind-sites-fse-manifest
