# Nyers adatbázis

A **Nyers adatbázis** szerkesztő lehetővé teszi a fejlesztők számára, hogy az adatbázis sorait pontosan úgy nézzék meg és módosítsák, ahogyan tárolva vannak. Kihagyja a kezelőoldalak ellenőrzéseit és a logikai törlést, ezért csak akkor használja, ha egy kezelőoldal nem tudja azt, amire szüksége van.

Az **Adminisztráció > Nyers adatbázis** alatt találja (`#/manage/raw-database`). Ehhez a `RawDatabase.Manage` engedély kell, amellyel csak a fejlesztők rendelkeznek.

## Táblák

Az oldal az összes tábla áttekintésével nyílik meg: nevük, entitásuk, sorainak és oszlopainak száma. Kattintson egy táblára (vagy a **Megnyitás** gombra) a sorainak megtekintéséhez, az **Összes tábla** gombbal pedig visszaléphet. A táblát a **Tábla** listában is kiválaszthatja. A lista az adatbázis minden tábláját a sorainak számával mutatja: az összes entitás tábláit (felhasználók, szerepkörök, oldalak, hírek, cikkek, galériák, letöltések, menük, fájlok, munkamenetek, ...), a kapcsolótábláikat (például egy felhasználó szerepkörei) és a verziótáblákat. Egy új entitás automatikusan megjelenik itt.

Az **AuditLogEntries** tábla csak olvasható: megnézheti, de nem módosíthatja.

## Sorok megtekintése

- A sorok minden oszloppal együtt jelennek meg, és lapozottak. A hosszú értékek rövidítve látszanak a listában.
- A **Megtekintés** megnyit egy sort, és minden értéket teljes egészében megmutat. Az **Előző** és a **Következő** az aktuális oldal sorai között lapoz, a **Szerkesztés** módosítja a sort, a **Bezárás** pedig elrejti. Csak olvasható táblákon is működik.
- A **Keresés** minden olyan oszlopban megkeresi a szöveget, amely szöveget, számot, azonosítót vagy dátumot tartalmaz (a bináris és az érzékeny oszlopokban nem keres).
- Kattintson egy oszlopnévre a szerinte rendezéshez, kattintson újra a sorrend megfordításához.
- A *NULL*-ként megjelenő érték üres az adatbázisban; az üres szöveg ettől különböző dolog.
- Az érzékeny értékek, például a jelszó-hashek és a tokenek mindig `***` formában jelennek meg.

## Sorok módosítása

| Művelet | Mi történik |
|---------|-------------|
| **Új sor** | Töltse ki az oszlopokat, és mentse. Az üres mezők kimaradnak; az üresen hagyott guid típusú kulcsoszlop új, generált azonosítót kap. Jelölje be a **NULL** jelölőnégyzetet üres érték tárolásához. |
| **Szerkesztés** | Módosítsa az oszlopokat, és mentse. Csak a módosított oszlopok íródnak ki. A kulcsoszlopok nem módosíthatók. A `***` érték úgy marad, ahogy tárolva van, amíg újat nem ír be. |
| **Törlés** | Megerősítés után véglegesen eltávolítja a sort. Nincs visszaállítás. |

Ha az adatbázis elutasít egy módosítást, például mert egy értéknek egyedinek kell lennie, vagy hiányzik egy kapcsolódó sor, az oldal megmutatja az adatbázis üzenetét, és semmi sem íródik ki. Ha egy érték nem fér bele az oszlopába, például szöveg egy szám típusú oszlopban, az oldal megmondja, melyik oszlopról van szó.

## Auditnapló

Minden nyers módosítás bekerül az [Auditnaplóba](./audit-log.md), mint a kezelőoldalak módosításai: **Létrehozva** egy új sornál, **Módosítva** minden módosított oszlop régi és új értékével (az érzékeny értékek maszkolva vannak) és **Véglegesen törölve** egy törölt sornál. A módosítás és az audit bejegyzése együtt mentődik, vagy egyáltalán nem.
