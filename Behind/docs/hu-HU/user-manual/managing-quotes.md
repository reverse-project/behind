# Idézetek

Az **Idézetek** a Behind példamodulja: rövid idézetek listája a szerzőjükkel. Megmutatja, hogyan ad hozzá egy modul saját oldalt, adminisztrációs eszközt, engedélyeket, menüelemet és adatbázistáblát az alkalmazáshoz.

Az idézeteket mindenki elolvashatja a főmenü **Idézetek** pontja alatt (`#/quotes`). Az **Adminisztráció > Idézetek** alatt kezelheti őket (`#/manage/quotes`).

| Művelet | Ehhez kell |
|---------|------------|
| Az idézetek megtekintése a kezelőoldalon | a `Quotes.Read` engedély |
| Idézetek létrehozása és szerkesztése | a `Quotes.Write` engedély |
| Idézetek törlése | a `Quotes.Delete` engedély |
| A törölt idézetek megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapértelmezés szerint minden engedéllyel rendelkeznek, a felhasználók olvashatnak.

## Idézetek írása

Egy idézetnek van **idézet** szövege (legfeljebb 1000 karakter) és **szerzője** (legfeljebb 200 karakter), mindkettő kötelező. A **Szerkesztés** betölti az idézetet az űrlapba, a **Mentés** pedig új verzióként tárolja. Egy friss telepítés egy üdvözlő idézettel indul.

## Idézetek keresése

A keresőmező az idézet szövegében és a szerzőben keres, a betűsor a szerző első betűje szerint szűr, a lista pedig lapozott.

## Idézetek törlése

A **Törlés** elrejti az idézetet (csak logikailag törlődik). A fejlesztők a **Törölt idézetek mutatása** kapcsolóval láthatják a töröltöket, majd a **Visszaállítás** gombbal visszaállíthatják az idézetet, vagy a **Végleges törlés** gombbal véglegesen eltávolíthatják. Minden módosítás megjelenik az [auditnaplóban](./audit-log.md), a `Quotes` tábla pedig szerkeszthető a [nyers adatbázisban](./managing-raw-database.md).
