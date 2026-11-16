# Galériák

A **galéria** képek nyilvános gyűjteménye címmel és nem kötelező leírással. Minden képnek lehet saját leírása. Mindenki böngészheti a galériákat a felső menü **Galériák** pontjánál (`#/galleries`).

## A galériák böngészése

- A **Galériák** oldal a közzétett galériákat kártyákként mutatja, a legújabbal kezdve (a létrehozásuk szerint), oldalanként tizenkettőt, alul oldalgombokkal. A kártya mutatja a **bélyegképet**, a címet, a képek számát, a szerzőt, a létrehozás vagy a módosítás időpontját (mindkettőt az [auditnaplóból](./audit-log.md) véve) és a leírást.
- A **Keresés** mező azokat a galériákat találja meg, amelyek címe vagy leírása tartalmazza a beírt szavakat, a kis- és nagybetűk megkülönböztetése nélkül.
- Egy galéria megnyitása megmutatja a leírását és a képek bélyegképeinek rácsát, mindegyiket a leírásával, ha van neki. A bélyegképre kattintva a kép egy nagy nézegetőben nyílik meg, alatta a leírásával, ha van. A nyílgombokkal vagy a balra és jobbra nyílbillentyűkkel léphet az előző vagy a következő képre, a bezárás gombbal vagy az **Esc** billentyűvel pedig bezárhatja.

## Galériák kezelése

Az eszközöket az **Adminisztráció > Galériák** alatt találja (`#/manage/galleries`).

| Művelet | Szükséges |
|---------|-----------|
| A galériák listájának megtekintése | a `Gallery.Read` engedély |
| Galériák létrehozása és szerkesztése, képek feltöltése | a `Gallery.Write` engedély |
| Galériák törlése | a `Gallery.Delete` engedély |
| A törölt galériák megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztőknek) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem.

### Galéria létrehozása

Az **Új galéria** megnyitja a szerkesztőt:

1. Adja meg a **címet** (kötelező) és, ha szeretné, egy **leírást**.
2. Válassza ki a képeket a **Képek feltöltése** alatt. Egyszerre annyit jelölhet ki, amennyit csak szeretne, PNG, JPEG, GIF vagy WebP formátumban. A feltöltés csoportosan történik, az előrehaladás látható, és a kiszolgáló minden képhez bélyegképet hoz létre. A nem használható fájlokat az okukkal együtt felsoroljuk, a többi képet hozzáadjuk.
3. Igény szerint adjon leírást a képeknek a leírásmezőikben. A nyílgombokkal módosíthatja a sorrendet, a szemetes gombbal pedig eltávolíthat egy képet a galériából.
4. A **Piszkozat mentése** megtartja a galériát közzététel nélkül. A **Mentés és közzététel** megjeleníti a Galériák oldalon; a leírások nem kötelezők, ezért leírásokkal vagy azok nélkül is közzéteheti. A feltöltött képeket csak a galéria mentése után őrizzük meg.

### A galéria bélyegképe

A szerkesztő **Bélyegkép** beállítása határozza meg, mit mutat a Galériák oldal:

- **Létrehozás a galéria képeiből** (alapértelmezett): az első négy képből készült négyzet alakú kollázs, amelyet a kiszolgáló a galéria minden mentésekor létrehoz.
- **Feltöltött kép használata**: válasszon egy fájlt a bélyegkép-feltöltésben, amelyet a kiszolgáló kicsinyít le.

### Verziók, piszkozatok és az élő verzió

A galériák **verziókban** mentődnek, a [cikkekhez](./managing-articles.md#verziók-vázlatok-és-az-éles-verzió) hasonlóan: minden mentés új verziót hoz létre a saját képlistájával és leírásaival, és semmi nem íródik felül.

- Az a galéria, amelyet még soha nem tettek közzé, *Piszkozat*, és nem látható a Galériák oldalon.
- Egy már élő galéria piszkozatának mentése megtartja az élő verziót, a lista ilyenkor egy *Újabb piszkozat* jelvényt mutat.
- A **Betöltés a szerkesztőbe** egy régebbi verziót másol a szerkesztőbe, az **Élesítés** bármelyik verzióra átváltja az élő verziót.

### Törlés és visszaállítás

- A **Törlés** átmenetileg töröl egy galériát. Semmi nem vész el.
- A fejlesztők a listán egy **Törölt galériák megjelenítése** kapcsolót látnak. A törölt galériánál a **Visszaállítás** és a **Végleges törlés** gomb jelenik meg; utóbbi a galériát az összes verziójával együtt eltávolítja.

Minden itt végzett módosítás rögzítésre kerül az [auditnaplóban](./audit-log.md), az azonosítók helyett a galéria címével és a kép leírásával.
