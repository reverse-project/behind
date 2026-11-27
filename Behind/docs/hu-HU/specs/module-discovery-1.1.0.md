# Modulfelderítés 1.1.0

Állapot: aktuális. Ez a verzió a [konfiguráció 1.7.0](./configuration-json-1.7.0.md)
leírásában szereplő, korábban verzió nélküli `GET /modules` választ bővíti.
A válasz megfelel a [modulfelderítés sémájának](../../schemas/module-discovery-1.1.0.schema.json).

## Felderítés

A nyilvános `GET /modules` JSON választ és `Cache-Control: no-store` fejlécet ad,
és csak a bekapcsolt modulok leíróit sorolja fel. Minden leíró tartalmazza a
kulcsot, nevet, frontend útvonalat, útvonalakat (path, controller, open),
adminisztrációs oldalakat (name, icon, href, grant), stílusokat és widgeteket
(key, name, model).

Az új `availableGrants` tömb a telepített hozzáférési katalógus jogosultságait
adja a kikapcsolt modulok területei nélkül. A Core jogosultságai megmaradnak.
Csak megjelenítés szűrésére szolgál, nem jogosultsági döntésre. A szerepkör-,
felhasználó- és munkamenet-API-k megtartják a teljes tárolt vagy számított adatot.

A felület szűri a szerepkörök jogosultságait és korlátozásait, a felhasználói
szerkesztőket és számított jogosultságokat, a munkamenet szerepkör-jogosultságait
és a profil kezelési adatait. A szerepkör- és felhasználószerkesztő mentéskor
változatlanul továbbítja a rejtett közvetlen választásokat. A szerepkör
jogosultságainak kifejezett visszaállítása továbbra is minden alapértelmezett
jogosultságot visszaállít. A kikapcsolás nem törli a hozzárendeléseket; ezek
visszakapcsolás és böngészőfrissítés után újra láthatók.

## Kompatibilitás és migráció

A válasz az `availableGrants` mezővel bővül; a leírók korábbi mezői változatlanok.
A kliensek kezeljék az új mezőket. Az összetartozó szerver- és frontendverziót
együtt kell telepíteni: a tömb nélküli válasznál a frontend elrejti a hozzáférési
bejegyzéseket és hibát naplóz. Adatbázis- vagy konfigurációmigráció nem szükséges.
Más nyitott böngészőket a kapcsoló módosítása után újra kell tölteni.
