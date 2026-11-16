#!/usr/bin/env bash

REPOSITORY=${1:-ghcr.io/reverse-project/behind}

cd ../src/Web/Behind.Web

APP_VERSION=$(dotnet msbuild Behind.Web.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-web-manifest
podman manifest add behind-web-manifest reverse-project/behind/web:x64
podman manifest add behind-web-manifest reverse-project/behind/web:arm64
podman manifest push behind-web-manifest "docker://${REPOSITORY}/web:latest"
podman manifest push behind-web-manifest "docker://${REPOSITORY}/web:${APP_VERSION}"
podman manifest rm behind-web-manifest