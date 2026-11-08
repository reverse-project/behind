# Behind felhasználói kézikönyv

Ez a kézikönyv azt ismerteti, hogy a Behind webalkalmazásban jelenleg mit tehet.

## Mindenkinek

- **[Első lépések](./getting-started.md)** - fiók létrehozása, bejelentkezés, kijelentkezés és a navigációs sáv.
- **[Az Ön fiókja](./your-account.md)** - profil, megjelenítendő név, az aktuális munkamenet és az összes munkamenete.
- **[Szégyenfal](./wall-of-shame.md)** - a letiltott fiókok nyilvános listája.

## Adminisztrátoroknak és fejlesztőknek

- **[Adminisztráció](./administration.md)** - az Adminisztráció oldal és az, hogy kik láthatják.
- **[Felhasználók kezelése](./managing-users.md)** - felhasználók keresése, kitiltása, törlése, visszaállítása, kijelentkeztetése és hozzáférésük szerkesztése.
- **[Szerepkörök és hozzáférések kezelése](./managing-roles-and-access.md)** - szerepkörök, engedélyek, korlátozások, és az, hogyan számítódnak ki a felhasználók számított engedélyei.
- **[Oldalak kezelése](./managing-sites.md)** - oldalak írása a formázott szövegszerkesztővel, verziók, vázlatok, közzététel és címek.
- **[Hírek](./managing-news.md)** - hírek olvasása és hozzászólás, hírek írása és a hírkategóriák kezelése.
- **[Cikkek](./managing-articles.md)** - cikkek olvasása és keresése, cikkek írása és a cikkkategóriák kezelése.
- **[Galériák](./managing-galleries.md)** - galériák böngészése, képek feltöltése csoportosan, a képek leírása és a galéria közzététele.
- **[Letöltések](./managing-downloads.md)** - a mappafa böngészése, fájlok letöltése, mappák létrehozása és több fájl egyidejű feltöltése.
- **[Menük](./managing-menus.md)** - a főmenü és más menük, azok elemei, almenüelemei, céljai, és az, hogy kik látják őket.
- **[Idézetek](./managing-quotes.md)** - a példamodul: idézetek olvasása és kezelése átmeneti törléssel, visszaállítással és végleges törléssel.
- **[Sport](./managing-sport.md)** - a Sport modul: csapatok, játékosok, szezonok, ranglisták és statisztikák böngészése, kezelésük szezononkénti játékosképekkel és csapatlogókkal.
- **[Dokumentáció](./managing-markdown-viewer.md)** - a Markdown Viewer modul: Markdown dokumentumok olvasása és statikus dokumentációs weboldal előállítása egy könyvtárból.
- **[Útvonal-álnevek](./managing-route-aliases.md)** - rövid útvonalak oldalakhoz, például `#/manuals` egy letöltési könyvtárhoz.
- **[Hozzáférési beállítások](./managing-access-settings.md)** - egy korlátozó oldal, amely a weboldalt csak egy szerepkörrel rendelkező felhasználóknak mutatja, és az új fiókok jóváhagyása a bejelentkezésük előtt.
- **[Megszemélyesítés](./impersonation.md)** - egy másik felhasználó nevében eljárni, hogy lássa, amit ő lát, és hogyan jelenik meg ez az auditnaplóban.
- **[Nyers adatbázis](./managing-raw-database.md)** - bármely tábla sorainak megtekintése, beszúrása, szerkesztése és végleges törlése (fejlesztőknek).
- **[Auditnapló](./audit-log.md)** - annak böngészése, hogy ki mit és mikor módosított.
- **[Beállítások](./managing-settings.md)** - a konfigurációs fájlok fülekként, maszkolt titkok, titkok létrehozása és cseréje (fejlesztőknek).
- **[Saját szerepkör emelése](./elevation.md)** - adminisztrátorrá vagy fejlesztővé válás hitelesítő kóddal.

## Jó tudni

- Az alkalmazáshoz engedélyezett JavaScript szükséges.
- Azok az oldalak, amelyekhez bejelentkezés szükséges, a „Nincs bejelentkezve.” üzenetet jelenítik meg egy bejelentkezési hivatkozással.
- Az adminisztrátoroknak szóló oldalak adatok helyett üzenetet mutatnak, ha a fiókja nem rendelkezik a szükséges hozzáféréssel.
