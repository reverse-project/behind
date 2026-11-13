# Konténerközzétételi szkriptek

A repository [`tools`](../../../../tools) mappájában található szkriptek elkészítik és közzéteszik az alkalmazás konténerképeit. Az alapértelmezett célprefix `ghcr.io/reverse-project/behind`.

## Követelmények

- A projektekhez szükséges .NET SDK (jelenleg .NET 11)
- Podman
- Hozzáférés és hitelesítés a célként használt konténerregisztrihez

## A szkriptek futtatása

Az egyes szkripteket a repository `tools` mappájából kell futtatni:

```bash
cd tools
bash publish-web-container.sh
```

Másik cél használatához adja meg a repository prefixét első argumentumként, például:

```bash
bash publish-web-container.sh git.prod.zwebnt.hu/reverse-project/behind
bash publish-all.sh git.prod.zwebnt.hu/reverse-project/behind
```

A prefix egy konténerregisztrit és repository-névteret azonosítson, URL-séma és záró perjel nélkül. Konténerképek feltöltését támogató regisztrit használjon; a GitHub konténerregisztrije `ghcr.io`, nem `github.com`.

Minden szkript Linux x64 és ARM64 képet készít, majd `latest` és a projekt verziójával ellátott címkékkel feltölt egy többarchitektúrás manifestet. A `publish-all.sh` sorban futtatja az összes `publish-*.sh` szkriptet.

| Szkript | Kép |
|---|---|
| [`publish-core-tools-container.sh`](../../../../tools/publish-core-tools-container.sh) | Core Tools parancssori alkalmazás |
| [`publish-core-database-migrator-container.sh`](../../../../tools/publish-core-database-migrator-container.sh) | Core adatbázis-migrátor |
| [`publish-web-container.sh`](../../../../tools/publish-web-container.sh) | Webalkalmazás |
| [`publish-web-database-migrator-container.sh`](../../../../tools/publish-web-database-migrator-container.sh) | Web adatbázis-migrátor |
| [`publish-sites-fse-container.sh`](../../../../tools/publish-sites-fse-container.sh) | FSE webhely |
| [`publish-sites-fse-database-migrator-container.sh`](../../../../tools/publish-sites-fse-database-migrator-container.sh) | FSE adatbázis-migrátor |
| [`publish-sites-rpdothu-container.sh`](../../../../tools/publish-sites-rpdothu-container.sh) | RP.hu webhely |
| [`publish-all.sh`](../../../../tools/publish-all.sh) | Az összes közzétételi szkript futtatása |
