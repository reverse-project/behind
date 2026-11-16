# Cikkek

A **cikkek** nyilvános oldalak címmel és tartalommal, amelyeket az adminisztrátorok írnak a böngészőben, kategóriákba csoportosítanak, és kereshetők. Mindenki elolvashatja őket a felső menü **Cikkek** pontjánál (`#/articles`). Hasonlóak a [hírekhez](./managing-news.md), de hozzászólások, ütemezés és kiemelés nélkül: a cikk a közzététele pillanatától látható, és a törléséig látható marad.

## A cikkek olvasása

- A **Cikkek** oldal a közzétett cikkeket sorolja fel, a legújabbal kezdve (a létrehozásuk szerint), oldalanként tízet, alul oldalgombokkal.
- Minden bejegyzés mutatja a címét, a **rövid tartalmát**, a **szerzőt**, a létrehozás időpontját (vagy, ha szerkesztették, az utolsó módosítás időpontját: „Frissítve: ...”), mindkettőt az [auditnaplóból](./audit-log.md) véve, valamint a kategóriáját.
- A lista feletti gombok **kategória** szerint szűrik a cikkeket. A **Mind** mindent megmutat. Amíg egy fejlécképpel rendelkező kategória ki van választva, a képe a lista felett látható, és a kategória minden bejegyzésén is megjelenik ez a kép.
- A **Keresés** mező azokat a cikkeket találja meg, amelyek **címe** vagy **tartalma** tartalmazza a beírt szavakat, a kis- és nagybetűk megkülönböztetése nélkül. A keresés a kategóriaszűrővel együtt működik. A rövid tartalomban nem keres.
- A **Tovább olvasom** (vagy a cím) megnyitja a cikket, amely a **tartalmát** (ha nem írtak tartalmat, a rövid tartalmát), a szerzőt, valamint a létrehozás vagy a módosítás időpontját mutatja.

## Cikkek kezelése

Az eszközöket az **Adminisztráció > Cikkek** (`#/manage/articles`) és az **Adminisztráció > Cikkkategóriák** (`#/manage/article-categories`) alatt találja.

| Művelet | Szükséges |
|---------|-----------|
| A cikkek és kategóriák listáinak megtekintése | az `Article.Read` engedély |
| Cikkek és kategóriák létrehozása és szerkesztése, képek feltöltése | az `Article.Write` engedély |
| Cikkek és kategóriák törlése | az `Article.Delete` engedély |
| A törölt cikkek és kategóriák megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztőknek) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem.

### Cikkek írása

Az **Új cikk** megnyitja a szerkesztőt a következőkkel:

- a **cím** (kötelező),
- a **kategória** (nem kötelező, a cikkkategóriák listájából választható),
- a **rövid tartalom**, amely a cikklistában jelenik meg, és a **tartalom**, amely a cikk megnyitásakor jelenik meg. Mindkettőt az [oldalak](./managing-sites.md#cím-url-szegmens-és-tartalom) formázott szövegszerkesztőjében írja. A szkripteket, stílusokat és ismeretlen címkéket a cikk mentésekor eltávolítjuk.

A szerző a cikket létrehozó bejelentkezett felhasználó. A tartalom üresen is hagyható: a megnyitott cikk ilyenkor a rövid tartalmat mutatja. A kezelőlista rendelkezik a címben és a tartalomban kereső **Keresés** mezővel, kezdőbetű szerinti szűrővel és oldalgombokkal.

### Verziók, vázlatok és az éles verzió

A cikkek **verziókban** mentődnek, a [hírekhez](./managing-news.md#verziók-vázlatok-és-az-élő-verzió) hasonlóan: minden mentés új verziót hoz létre, és semmi nem íródik felül.

- A **Vázlat mentése** egy új, nem megjelenített verziót tárol. Az a cikk, amelyet még soha nem tettek közzé, a listában *Vázlat*, és nem látható a Cikkek oldalon.
- A **Mentés és közzététel** egy új verziót tárol, és ezt teszi az **éles** verzióvá, azzá, amelyet a Cikkek oldal mutat.
- Egy már éles cikk vázlatának mentése megtartja az éles verziót, a lista ilyenkor egy *Újabb vázlat* jelvényt mutat.
- A **Betöltés a szerkesztőbe** egy régebbi verziót másol a szerkesztőbe, az **Élesítés** bármelyik verzióra átváltja az éles verziót, így lehet visszaállni egy korábbi állapotra is.

### Kategóriák

A cikkkategóriák különállnak a hírkategóriáktól. A kategóriának van egy **neve**, amely a cikkkategóriák között egyedi (a kis- és nagybetűk megkülönböztetése nélkül), és egy nem kötelező **fejlécképe**. Egy cikk legfeljebb egy kategóriához tartozik. A kategória törlése megtartja a cikkeit, amelyek kategória nélkül jelennek meg.

### Törlés és visszaállítás

- A **Törlés** átmenetileg töröl egy cikket vagy kategóriát. Semmi nem vész el.
- A fejlesztők mindkét listán egy **Törölt cikkek megjelenítése**, illetve **Törölt kategóriák megjelenítése** kapcsolót látnak. A törölt bejegyzésnél a **Visszaállítás** és a **Végleges törlés** gomb jelenik meg. Egy kategória csak akkor állítható vissza, ha egyetlen másik cikkkategória sem használja a nevét.

Minden itt végzett módosítás rögzítésre kerül az [auditnaplóban](./audit-log.md), az azonosítók helyett a cikk címével vagy a kategória nevével.
