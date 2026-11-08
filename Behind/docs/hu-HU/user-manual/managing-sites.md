# Oldalak kezelése

Az **Oldalak** olyan tartalmi oldalak, amelyeket az adminisztrátorok a böngészőben írnak, és mindenki elolvashat. Az **Adminisztráció > Oldalak** alatt találja őket (`#/sites`).

| Művelet | Ehhez kell |
|---------|------------|
| Az oldalak és verzióik listájának megtekintése | a `Site.Read` engedély |
| Oldalak létrehozása, verziók mentése, közzététel, verzió élesítése és képek feltöltése | a `Site.Write` engedély |

Az adminisztrátorok alapértelmezés szerint mindkét engedéllyel rendelkeznek.

## Oldalak keresése

A listán van **Keresés** mező (a legújabb verzió címére és URL-szegmensére illeszkedik), a címre vonatkozó betűszűrő és lapozógombok, mint a felhasználók oldalán. Az oldalak cím szerint, ábécésorrendben vannak listázva.

## Törlés és visszaállítás

- A **Törlés** (a `Site.Delete` engedély) logikailag törli az egész oldalt az összes verziójával. Megszűnik a kiszolgálása, az URL-szegmense újra szabad, és semmi sem vész el.
- A fejlesztők (a `RawDatabase.Manage` engedély) látnak egy **Törölt oldalak megjelenítése** kapcsolót. A törölt oldalon **Visszaállítás** jelenik meg, amely visszahozza az összes verziót és az élő verziót, valamint **Végleges törlés**, amely véglegesen eltávolítja az oldalt, a verzióit és az élő verzióját. A visszaállítást a rendszer elutasítja, ha közben egy másik oldal átvette az URL-szegmenst.

## Verziók, vázlatok és az élő verzió

Minden mentés az oldal **új verzióját** hozza létre, semmi sem íródik felül.

- A **Vázlat mentése** új, nem közzétett verziót tárol. A vázlat soha nem jelenik meg a látogatóknak.
- A **Mentés és közzététel** új verziót tárol, közzéteszi, és ezt teszi az **élő** verzióvá.
- A látogatóknak csak az **élő** verzió jelenik meg. Egy élő oldal vázlatának mentése nem változtatja meg, amit a látogatók látnak.
- A **Betöltés a szerkesztőbe** egy régebbi verziót másol a szerkesztőbe. A mentés ennek alapján új verziót hoz létre.
- Az **Élesítés** az oldal bármely verziójára átváltja az élő verziót, így lehet visszalépni is. Az élesített vázlat ebben a pillanatban közzétételre kerül.

Minden verziósor mutatja az állapotát (Vázlat, Közzétéve vagy Élő), a címét, az URL-szegmensét, a mentésének idejét és a közzétételének idejét.

## Cím, URL-szegmens és tartalom

- A **cím** a tartalom felett jelenik meg.
- Az **URL-szegmens** az oldal címe. A címből generálódik (kisbetűk, számok és kötőjelek), amíg Ön nem ír az URL-szegmens mezőbe. A **Generálás a címből** visszaállítja. Minden verziónak saját URL-szegmense van, és két oldal nem használhatja egyszerre ugyanazt az URL-szegmenst: egy URL-szegmens foglalt, amíg egy másik oldal élő vagy legújabb verziójához tartozik.
- A **tartalom** formázott szöveges szerkesztőben készül, címsorokkal, félkövér, dőlt, aláhúzott, áthúzott szöveggel, listákkal, behúzással, igazítással, idézetekkel, kódblokkokkal és hivatkozásokkal. A kép gombbal tölthet fel képet (PNG, JPEG, GIF vagy WebP, legfeljebb a szerkesztő alatt látható korlátig, alapértelmezés szerint 5 MB). Képfájl szerkesztőbe húzása vagy beillesztése szintén feltölti azt.

A tartalmat a rendszer mentéskor megtisztítja: a szkripteket, stílusokat, eseménykezelőket és ismeretlen címkéket eltávolítja.

## Szakaszok

A szakaszok kezdőoldal-szerű elrendezéssé alakítják az oldalt. A tartalom alatt jelennek meg, az Ön által megadott sorrendben, és a mentett verzió részei: a **Vázlat mentése** és a **Mentés és közzététel** a szakaszokat a címmel és a tartalommal együtt tárolja, a régebbi verziók megtartják a saját szakaszaikat, a **Betöltés a szerkesztőbe** pedig visszahozza az adott verzió szakaszait.

- A **Szakasz hozzáadása** hozzáfűz egy szakaszt (egy oldalnak legfeljebb 30 szakasza lehet). A nyílgombokkal előrébb vagy hátrébb mozgathat egy szakaszt, az **Eltávolítás** kiveszi; az oldalon semmi sem változik a mentésig.
- A **Cím (nem kötelező)** mezőt nem kötelező kitölteni.
- A **Megjelenés** lehet **Normál** (egyszerű, mint az oldal többi része) vagy **Kiemelt** (az oldal kiemelő színében megjelenő blokk, világos szöveggel).
- A **Szöveg** ugyanabban a formázott szöveges szerkesztőben készül, mint a tartalom.
- A **Kép (nem kötelező)** PNG, JPEG, GIF vagy WebP lehet, legfeljebb a képméret-korlátig. Képpel megadhat leírást (akkor jelenik meg, ha a kép nem látható, és a képernyőolvasók felolvassák), és kiválaszthatja, hogy a kép a szövegtől **balra** vagy **jobbra** legyen. Keskeny képernyőkön a kép a szöveg felett jelenik meg. A **Kép eltávolítása** kiveszi a képet a szakaszból.
- Minden szakaszhoz kell cím, szöveg vagy kép, különben az oldal nem menthető.

Egy szakaszokkal rendelkező oldal tartalma lehet üres, ilyenkor az oldal csak a szakaszokat (és a képforgót, ha van) mutatja. A szakaszok szövegét a rendszer a tartalomhoz hasonlóan megtisztítja.

## Képforgó

Egy oldalnak lehet képforgója: egy, az élő oldal tartalma felett megjelenő, magától továbblépő csúszka. Használata minden oldalnál az Ön döntése: a képek nélküli oldal a korábbiak szerint jelenik meg. Nyisson meg egy meglévő oldalt a szerkesztőben, és használja a **Képforgó** szakaszt (új oldalnál a mentésig nem jelenik meg).

- A **Képek hozzáadása** egy vagy több képet tölt fel (PNG, JPEG, GIF vagy WebP, legfeljebb a képméret-korlátig, korlátozott számú képpel; a korlátok a szakaszban láthatók). A nyilakkal módosíthatja a sorrendet, a kuka gombbal pedig eltávolíthat egy képet a képforgóból.
- Minden képhez tartozik egy nem kötelező **felirat**, amely a kép felett jelenik meg, és egy nem kötelező **hivatkozás**, amely a képre kattintva megnyílik. A hivatkozás `https://`, `http://`, `mailto:`, `#/` (a webhely egy oldala) vagy `/` karakterekkel kezdődik.
- A **Másodperc képenként** megadja, meddig marad egy kép (a mező alatt látható megengedett tartományon belül). Az **Előnézet** úgy mutatja a csúszkát, ahogyan a látogatók látják.
- A **Képforgó mentése** új verziót tárol a képforgóból, és azonnal megjeleníti az oldalon, amennyiben maga az oldal élő. A vázlatoldal képforgója nem jelenik meg a látogatóknak.
- A látogatók használhatják az előző és következő gombokat, a pontokat és a lapozó ujjmozdulatokat. A csúszka szünetel, amíg a mutató felette van, és nem lép tovább magától azoknál a látogatóknál, akiknek az eszköze csökkentett mozgást kér.
- A **Képforgó eltávolítása** (`Site.Delete` kell hozzá) elrejti. A fejlesztő a szakaszban **Törölve** jelvényt lát, és a **Visszaállítás** gombbal visszaállíthatja, vagy a **Végleges törlés** gombbal véglegesen törölheti (ez a képeit is törli). A képforgó követi az oldalát: egy oldal törlése, visszaállítása vagy végleges törlése ugyanezt teszi a képforgójával.

## Elérési címek

Az élő oldal a `#/<slug>` címen érhető el. Ha az URL-szegmens megegyezik egy beépített oldaléval (például `users`), a beépített oldal nyer, és az oldal a `#/site/<slug>` tartalék címen marad elérhető. A szerkesztő az élő verzió mindkét címét felsorolja.

### Egyéni kezdőlap

Az oldal adminisztrátora egy URL-szegmenst **egyéni kezdőlapként** nevezhet meg a `frontend.json` fájlban (`Frontend:Sites:IndexSlug`). Amíg az ilyen URL-szegmensű oldal élő, a `#/` kezdőoldal a beépített üdvözlőoldal helyett ezt az oldalt mutatja. Az oldal a saját címén is kiszolgálásra kerül. A szerkesztő **Egyéni kezdőlap** jelvényt mutat az élő verzión és az Oldalak listán, és jelzi, ha a beírt URL-szegmens az egyéni kezdőlap. A beállítás nélkül, vagy amíg nincs élő oldal ezzel az URL-szegmenssel, a `#/` az üdvözlőoldalt mutatja.

Az élő oldal nélküli cím az „Ez az oldal nem létezik." üzenetet mutatja.

Minden itt végzett módosítás bekerül az [auditnaplóba](./audit-log.md), az azonosító helyett az oldal címével.
