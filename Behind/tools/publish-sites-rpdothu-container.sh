#!/usr/bin/env bash

cd ../src/Sites/Behind.Sites.RPDotHu

APP_VERSION=$(dotnet msbuild Behind.Sites.RPDotHu.csproj -getProperty:Version)

dotnet publish /t:PublishContainer -r linux-x64 -c Release -p:ContainerImageTags=x64
dotnet publish /t:PublishContainer -r linux-arm64 -c Release -p:ContainerImageTags=arm64
podman manifest create behind-sites-rpdothu-manifest
podman manifest add behind-sites-rpdothu-manifest reverse-project/behind/sites/rpdothu:x64
podman manifest add behind-sites-rpdothu-manifest reverse-project/behind/sites/rpdothu:arm64
podman manifest push behind-sites-rpdothu-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/sites/rpdothu:latest
podman manifest push behind-sites-rpdothu-manifest docker://git.prod.zwebnt.hu/reverse-project/behind/sites/rpdothu:${APP_VERSION}
podman manifest rm behind-sites-rpdothu-manifest
