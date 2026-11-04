#!/usr/bin/env bash

cd ../src/Core/Behind.Core.Tools

APP_VERSION=$(dotnet msbuild Behind.Core.Tools.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-core-tools-manifest
podman manifest add behind-core-tools-manifest reverse-project/behind/core-tools:x64
podman manifest add behind-core-tools-manifest reverse-project/behind/core-tools:arm64
podman manifest push behind-core-tools-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/core-tools:latest
podman manifest push behind-core-tools-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/core-tools:${APP_VERSION}
podman manifest rm behind-core-tools-manifest