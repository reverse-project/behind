FROM fedora:43

RUN dnf update -y && dnf install -y \
    curl wget git unzip bzip2 xz sudo \
    gcc gcc-c++ make ripgrep fd-find libicu \
    clang zlib-devel zlib-static \
    procps-ng lldb python3 python3-pip \
    podman qemu-user-static \
    java-latest-openjdk-headless openssh-server tar gzip ss \
    libXext libXrender libXtst libXi freetype fontconfig \
    && dnf clean all

WORKDIR /root

ENV PATH="/root/.local/bin:/root/.local/share/mise/shims:/root/.local/share/mise/bin:$PATH"

USER root
WORKDIR /workspace
