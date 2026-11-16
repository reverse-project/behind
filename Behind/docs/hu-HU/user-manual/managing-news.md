# Hírek

A **Hírek** közlemények nyilvános listája, amelyet az adminisztrátorok a böngészőben írnak. Mindenki elolvashatja a felső menü **Hírek** pontja alatt (`#/news`), a bejelentkezett felhasználók pedig hozzászólhatnak.

## A hírek olvasása

- A **Hírek** oldal a jelenleg érvényes híreket listázza, a legújabbakkal kezdve (ütemezett hírnél a kezdete, egyébként a létrehozása szerint), oldalanként tízet, alul lapozógombokkal.
- Minden bejegyzés mutatja a címét, a **rövid tartalmát**, a **szerzőt**, a létrehozás idejét (vagy szerkesztés után az utolsó módosítás idejét: „Frissítve: ..."), mindkettőt az [auditnaplóból](./audit-log.md) véve, a kategóriáját és a hozzászólások számát.
- A **kiemelt** hír az első oldal tetején jelenik meg, *Kiemelt* jelvénnyel és színes kerettel, az újabb hírek előtt.
- A lista feletti **betűgombok** csak azokat a híreket mutatják, amelyek címe az adott betűvel kezdődik (`#` az egyéb karaktereknél). Csak azok a betűk kattinthatók, amelyekhez van hír a kiválasztott kategóriában, a **Mind** megszünteti a szűrést. Másik kategória választása törli a betűt.
- A lista feletti gombok **kategória** szerint szűrik a híreket. A **Mind** mindent megmutat. Amíg egy fejléképpel rendelkező kategória ki van választva, a képe a lista felett látszik, és a kategória minden bejegyzése is megmutatja ezt a képet.
- A **Tovább olvasom** (vagy a cím) megnyitja a hírt, amely megmutatja a **tartalmát** (a rövid tartalmat, ha nem írtak tartalmat), a szerzőt, a létrehozás vagy módosítás idejét és a hozzászólásokat.

## Hozzászólások

A kiszolgáló adminisztrátora a `backend.json` fájlban (`Backend:Comments:News`) kikapcsolhatja a hírekhez fűzött hozzászólásokat. Amíg ki vannak kapcsolva, a hozzászólások száma és a hozzászólások szakasz nem jelenik meg, és senki sem írhat, szerkeszthet vagy törölhet hozzászólást; a meglévő hozzászólások megmaradnak, és a beállítás visszakapcsolásakor újra megjelennek.


- A bejelentkezett felhasználók a hír alatt írnak hozzászólást, és a **Hozzászólás** gombbal küldik el. A hozzászólások egyszerű szövegek, legfeljebb 2000 karakter hosszúak, a legrégebbitől kezdve vannak listázva, lapozógombokkal.
- A saját hozzászólásait **szerkesztheti** és törölheti. A **Szerkesztés** a helyén nyitja meg a hozzászólást, a **Mentés** a hozzászólás új verziójaként tárolja, a hozzászólás ezután *szerkesztve* jelzést mutat a módosítás idejével (az [auditnaplóból](./audit-log.md) véve). A `News.Manage` engedély birtokosai (az adminisztrátorok és a későbbi moderátorok) bármely hozzászólást szerkeszthetik, a `News.Delete` engedély birtokosai bármely hozzászólást törölhetik. A törölt hozzászólás logikailag törlődik, lásd alább.
- Bejelentkezés nélkül elolvashatja a hozzászólásokat, írásukhoz pedig bejelentkezésre kérik.

## Hírek kezelése

Az eszközöket az **Adminisztráció > Hírek** (`#/manage/news`) és az **Adminisztráció > Hírkategóriák** (`#/manage/news-categories`) alatt találja.

| Művelet | Ehhez kell |
|---------|------------|
| A hírek és kategóriák listájának megtekintése | a `News.Read` engedély |
| Hírek és kategóriák létrehozása és szerkesztése, képek feltöltése | a `News.Write` engedély |
| Hírek és kategóriák törlése, bármely hozzászólás törlése | a `News.Delete` engedély |
| Bármely hozzászólás szerkesztése | a `News.Manage` engedély |
| A törölt hírek és kategóriák megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem.

### Hír írása

Az **Új hír** megnyitja a szerkesztőt a következőkkel:

- a **cím** (kötelező),
- a **kategória** (nem kötelező, a választható kategóriák listája),
- a **rövid tartalom**, amely a hírlistában jelenik meg, és a **tartalom**, amely a hír megnyitásakor jelenik meg. Mindkettőt az [oldalak](./managing-sites.md#cím-url-szegmens-és-tartalom) formázott szöveges szerkesztőjében írja: címsorok, félkövér, dőlt, listák, idézetek, kódblokkok, hivatkozások és feltöltött képek (PNG, JPEG, GIF vagy WebP, legfeljebb az oldalak képméret-korlátjáig, alapértelmezés szerint 5 MB). A szkripteket, stílusokat és ismeretlen címkéket a rendszer a hír mentésekor eltávolítja.

Az ütemezés **nem kötelező**. Alapértelmezés szerint a hírnek nincs ütemezése: a közzététele után azonnal látható, és a törléséig megmarad. Kapcsolja be a **Hír ütemezése** kapcsolót, hogy megadhasson **Kezdet** értéket (bekapcsolt ütemezésnél kötelező, az aktuális idővel van kitöltve) és egy nem kötelező **Vége (nem kötelező)** értéket. Az ütemezett hír csak a kezdetétől a végéig jelenik meg a Hírek oldalon, nyitható meg és kommentelhető (a kezdet előtt rejtett marad, a végén eltűnik), és a végének a kezdete után kell lennie. Hagyja üresen a véget, hogy a hír a törléséig megmaradjon. Az éppen nem megjelenő hírt a kezelők továbbra is megtalálják, így szerkeszthetik, meghosszabbíthatják vagy törölhetik.

A **Kiemelt** kapcsoló az adott hírt kiemeltként jelöli meg. Egyszerre csak egy hír lehet kiemelt: egy kiemelt verzió közzététele megszünteti a korábban kiemelt hír kiemelését, a kiemelt hír törlése pedig megszünteti a kiemelését (a visszaállítás nem hozza vissza).

A szerző az a bejelentkezett felhasználó, aki a hírt létrehozta. A tartalom üresen is hagyható: a megnyitott hír ilyenkor a rövid tartalmat mutatja. A listán van **Keresés** mező a címre, betűszűrő és lapozógombok.

### Verziók, vázlatok és az élő verzió

A híreket **verziókban** menti a rendszer, mint az [oldalaknál](./managing-sites.md#verziók-vázlatok-és-az-élő-verzió): minden mentés új verziót hoz létre, és semmi sem íródik felül.

- A **Vázlat mentése** új, nem megjelenített verziót tárol. A soha nem közzétett hír a listában *Vázlat*, és nem látható a Hírek oldalon.
- A **Mentés és közzététel** új verziót tárol, és ezt teszi az **élő** verzióvá, azzá, amelyet a Hírek oldal mutat.
- Egy már élő hír vázlatának mentése megtartja az élő verziót, a lista ilyenkor *Újabb vázlat* jelvényt mutat.
- A **Betöltés a szerkesztőbe** egy régebbi verziót másol a szerkesztőbe, a mentés pedig ennek alapján új verziót hoz létre. Az **Élesítés** bármely verzióra átváltja az élő verziót, így lehet visszalépni is.
- Az adminisztrációs lista a legutóbbi verziót és az élő verziót mutatja, a jelvények (*Vázlat*, *Újabb vázlat*, *Ütemezett*, *Lejárt*, *Kiemelt*) az élő verziót és annak kezdetét és végét írják le.
- A Hírek oldalon látható *Létrehozva* és *Frissítve* idő az 1. verzió mentésének ideje, illetve a megjelenített verzió mentésének ideje, az [auditnaplóból](./audit-log.md) véve.

### Kategóriák

Egy kategóriának van **neve**, amely a kategóriák között egyedi (a kis- és nagybetűk megkülönböztetése nélkül), és nem kötelező **fejléképe**. A képet a kategória űrlapjának fájlmezőjével válassza ki: azonnal feltöltődik és előnézetként jelenik meg, a **Kép eltávolítása** elhagyja. A **Szerkesztés** betölti a kategóriát az űrlapba, a **Mentés** pedig tárolja.

Egy hír legfeljebb egy kategóriába tartozik. Egy kategória törlése megtartja a hozzá tartozó híreket, amelyek kategória nélkül jelennek meg.

### Törlés és visszaállítás

- A **Törlés** logikailag töröl egy hírt (a hozzászólásaival együtt) vagy egy kategóriát. Semmi sem vész el.
- A fejlesztők mindkét listán látnak egy **Törölt hírek megjelenítése**, illetve **Törölt kategóriák megjelenítése** kapcsolót. A törölt bejegyzésen **Visszaállítás** és **Végleges törlés** jelenik meg. Egy hír visszaállítása a hozzászólásait is visszaállítja. Egy kategória csak akkor állítható vissza, ha nincs másik kategória, amely a nevét használja. Egy kategória végleges törlése megtartja a híreit, kategória nélkül.

Minden itt végzett módosítás bekerül az [auditnaplóba](./audit-log.md), azonosítók helyett a hír címével, a kategória nevével vagy a hozzászólás szerzőjével.
