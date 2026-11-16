# Menük

A **menük** az alkalmazás által megjelenített hivatkozáslisták. A **főmenü** beépített: ez tartalmazza a felső sáv füleit (**Kezdőlap**, **Hírek**, **Cikkek**, **Galériák**, **Szégyenfal**) és az Ön nevére kattintva megnyíló menü bejegyzéseit (**Profil**, **Munkamenet**, **Munkamenetek** és **Adminisztráció**). További menüket is felvehet, például egy **footer** menüt, amely minden oldal alján jelenik meg a verzió és a licencek mellett.

Az eszközöket az **Adminisztráció > Menük** alatt találja (`#/manage/menus`).

| Művelet | Ehhez kell |
|---------|------------|
| A menük és elemeik megtekintése | a `Menu.Read` engedély |
| Menük és elemek létrehozása és szerkesztése | a `Menu.Write` engedély |
| Menük és elemek törlése | a `Menu.Delete` engedély |
| A törölt menük és elemek megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem.

## Menük

Egy menünek van **neve** és **címe**.

- A **név** alapján kérhető le a menü, például `footer`. Kisbetűkké, számokká és kötőjelekké alakul, és egyedinek kell lennie a kis- és nagybetűk megkülönböztetése nélkül. Az oldal a `main` nevű menüt a felső sávjaként, a `footer` nevű menüt pedig az alján jeleníti meg.
- A **cím** az olvasható név, amely a listákban jelenik meg.
- A beépített **main** menün *Beépített* jelvény látható. A neve nem módosítható, és nem törölhető, de a címét és a benne lévő összes elemet módosíthatja.

Az **Elemek** megnyitja a menü elemeit.

A menük és a menüelemek listáján van **Keresés** mező, **betűgombok**, amelyek csak azokat a bejegyzéseket mutatják, amelyek címe az adott betűvel kezdődik, valamint lapozógombok alul. Az elemek listája lapos, pozíció, majd cím szerint rendezett, az almenüelemnél pedig látszik, hogy melyik elem a szülője.

## Menüelemek

Egy elemnek van:

- **címe**, a hivatkozás szövege,
- **URL**-je: az alkalmazás egy oldala (`#/news`), ennek a webhelynek egy másik címe (`/about`) vagy `http://` vagy `https://` kezdetű külső cím, `mailto:` vagy `tel:` hivatkozás. Más típusú címeket (például `javascript:`) a rendszer elutasít,
- **Megnyitás itt** beállítása: **Jelenlegi oldal**, **Új lap** vagy **Új ablak** (egy kis, különálló böngészőablak),
- **pozíciója**: az elemek pozíció, majd cím szerint vannak rendezve,
- nem kötelező **szülője**. A szülővel rendelkező elem **almenüelem**: a szülője legördülő menüvé válik, amely felsorolja az almenüelemeit. Csak ugyanazon menü legfelső szintű hivatkozásai lehetnek szülők, és az almenüelemek egy szint mélyek lehetnek. Az almenüelemekkel rendelkező elem maga nem válhat almenüelemmé,
- a **szerepkörök**, amelyek látják.

### Ki látja az elemet

Ha egyik szerepkört sem jelöli be, az elem **mindenkinek** látható, a be nem jelentkezett látogatóknak is. Ha egy vagy több szerepkört bejelöl, az elem csak azoknak a bejelentkezett felhasználóknak látható, akik rendelkeznek **legalább az egyikkel**. Így van elrejtve az **Adminisztráció** bejegyzés mindenki elől, kivéve az adminisztrátorokat és a fejlesztőket. Az almenüelem is rejtett, ha a szülője rejtett. Az elem elrejtése csak a hivatkozást rejti el: a mögötte lévő oldal továbbra is ellenőrzi a felhasználó hozzáférését.

Ha egy szerepkört töröl, a csak neki korlátozott elemeket senki sem látja, amíg a szerepkört vissza nem állítják.

### Felhasználói menüelemek

A főmenüben egy elem lehet **felhasználói menüelem**: ez a bejelentkezett felhasználó menüjében (a felhasználónév alatt) szerepel, és nem a felső sávban. Felhasználói menüelemet csak a főmenübe, legfelső szinten lehet felvenni, ezért a **Típus** mező csak ott érhető el. A **Kijelentkezés** mindig a felhasználói menü utolsó bejegyzése, és nem módosítható.

## Törlés és visszaállítás

- A **Törlés** logikailag töröl egy menüt vagy elemet. A törölt menü az elemeivel együtt rejtett, a törölt elem az almenüelemeivel együtt rejtett. Semmi sem vész el.
- A fejlesztők mindkét listán látnak egy **Törölt menük megjelenítése**, illetve **Törölt menüelemek megjelenítése** kapcsolót. A törölt bejegyzésen **Visszaállítás** és **Végleges törlés** jelenik meg. Egy menü csak akkor állítható vissza, ha nincs másik menü, amely a nevét használja.
- Egy menü **Végleges törlése** eltávolítja az összes elemét, egy elemé pedig az almenüelemeit.

Minden itt végzett módosítás bekerül az [auditnaplóba](./audit-log.md), azonosítók helyett a menü vagy az elem címével.
