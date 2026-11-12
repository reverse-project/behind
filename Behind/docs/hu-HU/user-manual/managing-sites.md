# Oldalak kezelése

## Opcionális tartalmi nyelvek

A meglévő tartalomszerkesztő **Tartalom nyelve** mezőjében válasszon nyelvet, vagy hagyja üresen. A választható értékek az alkalmazás felületének elérhető nyelvei. A nyelv a láthatóságot szabályozza, nem fordít automatikusan: a nyelv nélküli tartalom minden kiválasztott nyelven megjelenik, ezért csak az eltérő tartalmat kell lefordítani. Nyelvválasztás nélkül csak a nyelv nélküli tartalom látható.

Az útvonalak nem tartalmaznak nyelvet. A kiválasztott nyelvet a tartalomkérések kapják meg; alapértelmezését a bejelentkezett felhasználó profilja, a böngésző gyorsítótára vagy a webhely alapértelmezése adja. Az oldalak és útvonalálnevek különböző nyelveken használhatják ugyanazt a slugot, de azonos nyelven két aktív bejegyzés nem. Fordított oldal létrehozásakor válassza ki a nyelvet. Minden fordítás továbbra is a meglévő kezelőoldalakon szerkeszthető.

Az elrendezésszerkesztőben adjon hozzá **Nyelvváltó** widgetet, vagy kapcsolja be a váltót egy **Oldalfejléc** widgetben. A váltó módosítja a tartalomkérések nyelvét az útvonal megváltoztatása nélkül. Bejelentkezett felhasználónál a kiválasztott nyelv a profil felületi nyelveként is mentésre kerül.

A gyermek tartalom, például oldalszakasz, galériaelem vagy képforgó dia, saját hozzárendelés nélkül a szülő nyelvét örökli. Csak akkor rendeljen nyelvet az elrendezéshez, ha a fejlécnek, navigációnak vagy widgeteknek el kell térniük; a nyelv nélküli elrendezések és menük a nyelvhez rendelt oldalakon is megosztottak.

## Oldalelrendezések és widgetek

### Legfelső rétegű jelvények

A képes jelvények margó, belső térköz és a képek közötti hézag nélkül simulnak a kiválasztott oldalsarokhoz. A szöveges jelvények megtartják a külső és belső térközüket.

Az elrendezésszerkesztő **Legfelső rétegű jelvények > Jelvény hozzáadása** műveletével például „Fejlesztés alatt” felirat vagy hivatkozással ellátott EU-támogatási kép helyezhető el. Egy elrendezés legfeljebb 20 jelvényt tartalmazhat. Adjon meg **Szöveg / képleírás** értéket (kötelező, legfeljebb 200 karakter), oldalsarkot, 40–600 képpontos szélességet és opcionális hivatkozást. Kép nélkül a szöveg jelenik meg; feltöltött PNG, JPEG, GIF vagy WebP esetén a kép akadálymentes leírása lesz. A kép eltávolítása visszaállítja a szöveges jelvényt. A feltöltés az egyéni widgetek képeivel azonos engedélyt, méretkorlátot és `/layouts/images/{id}` tárolást használ.

A jelvények a teljes nyilvános elrendezés szakaszai és menüi felett jelennek meg. Az oldalhoz, nem a nézetablakhoz rögzülnek: a felső jelvények elgörgethetők, az alsók az oldal alján látszanak. Az azonos sarokban lévők a lista sorrendjében helyezkednek el, kis képernyőn szélességük csökken. Ne takarják el a fontos navigációt. A hivatkozások az aktuális lapon nyílnak meg, helyi útvonalat vagy HTTP-, HTTPS- és levelezési címet fogadnak; parancsfájlok és protokoll nélküli külső címek tiltottak. Csak maga a jelvény fogad kattintást, az átlátszó fedőréteg nem. A rögzített kezelési, fiók- és korlátozott hozzáférésű oldalakon nincs jelvény; a szerkesztő előnézetében van. A módosításokat mentse; az elrendezés másolása a jelvényeket is másolja.

### Csevegés

A csevegési előzmény az adatbázisban tárolódik, a korábbi 200 üzenetes korlát nélkül. Az üzenetek a legrégebbitől a legújabbig jelennek meg; felfelé görgetve automatikusan betöltődnek a korábbiak, vagy használhatja a **Régebbi üzenetek betöltése** gombot. Az üzenet a szerző profilképét (vagy kezdőbetűjét), létrehozási és utolsó szerkesztési idejét mutatja. Bejelentkezve legfeljebb 1 000 karakteres üzenetet küldhet, vagy a **Válasz** gombbal válaszolhat. A **Szerkesztés** csak saját üzenetnél, a létrehozást követő öt percig érhető el; a módosítás nem indítja újra ezt az időt. Az egyidejű szerkesztés elutasításra kerül az újabb szöveg felülírása helyett.

Az üzenetek azonnal megjelennek. Vendégek olvashatnak; bejelentkezett felhasználók alapértelmezésben **Chat.Read** és **Chat.Write** engedélyt kapnak. A szerepkör- vagy felhasználói hozzáféréskezelésben a **Chat.Write** korlátozása tiltja a küldést, választ és szerkesztést; a **Chat.Read** korlátozása elrejti a csevegést és tiltja az API-ját az adott bejelentkezett felhasználónak. A korlátozás felülírja a szerepkör engedélyeit. Mivel vendégek olvashatnak, a korlátozott felhasználó kijelentkezve továbbra is olvashatja a nyilvános csevegést.

A **Chat.Delete** saját üzenet eltávolítására jogosít; a moderátornak **Chat.Manage** engedélyt adjon, amellyel bármely üzenetet eltávolíthat. Egyik sem írja felül a szerzőre vonatkozó ötperces szerkesztési korlátot. A törlés auditált, és minden verziót elrejt, a válaszok idézett előnézetében is. A régi `widget-chat.json` fájl érintetlen marad, nem kerül importálásra; az adatbázisos csevegés üres előzménnyel indul.

Nyissa meg az **Adminisztráció > Elrendezések** oldalt (`#/manage/layouts`). Megtekintéshez `Configuration.Read`, mentéshez `Configuration.Manage` szükséges, és a konfiguráció nem lehet csak olvasható. Adjon hozzá vagy másoljon elrendezést, válassza ki az alapértéket, majd sorokkal, oszlopokkal és átfogásokkal rendezze el a szakaszokat és widgeteket. Minden szakaszhoz tartozik **Szélesség**: az **Oldalszélesség** (alapértelmezett) az oldal tárolóján belül tartja a szakaszt, a **Teljes szélesség** a nézetablak teljes szélességében, szélétől szélig megjeleníti. Az előnézet az elhelyezést mutatja; a Mentés az összes módosítást együtt alkalmazza. Minden elrendezéshez pontosan egy **Oldaltartalom** widget kell, amely az aktuális útvonal oldalát jeleníti meg.

A motor a teljes nyilvános oldalt rendereli: az elrendezésen kívül nincs fejléc vagy lábléc. Az új elrendezés teljes szélességű **Fejléc**, oldalszélességű **Oldaltartalom** és teljes szélességű **Lábléc** szakaszból áll. A Fejléc és Lábléc widget nem kötelező: eltávolítható, áthelyezhető, menükkel vagy egyéni widgetekkel helyettesíthető. A Fejléc arculatot, navigációt és fiókműveleteket, a Lábléc alkalmazásadatokat és lábléchivatkozásokat ad. A korábbi elrendezések betöltéskor automatikusan megkapják ezeket a widgeteket a megjelenés megőrzéséhez; az átállást mentéssel lehet véglegesíteni. A fiók-, kezelési és korlátozott hozzáférésű oldalak rögzített keretet használnak. Mentés után a szerkesztő körül a kiválasztott mentett elrendezés jelenik meg az oldal elhagyásáig; a többi kezelőoldal változatlan marad.

A szakaszok és widgetek **CSS-osztályok** mezője a webhely stíluslapjában megadott, szóközzel elválasztott osztályneveket fogad (legfeljebb 500 karakter, például `site-banner theme-dark`). Minden név ASCII-betűvel, aláhúzással vagy kötőjellel kezdődik, majd betűket, számjegyeket, aláhúzásokat és kötőjeleket tartalmazhat. Az osztályok a renderelt szakasz vagy widget burkolóelemét formázzák; nem engedélyeznek parancsfájlokat, tetszőleges stíluslapokat vagy tisztítatlan HTML-t. A szerkesztő elhelyezési vezérlőit nem befolyásolják.

Minden widgetelhelyezéshez külön **Szegély nélkül**, **Margó nélkül** és **Belső térköz nélkül** kapcsoló tartozik. Ezek a cella/widget keretének szegélyét (a lekerekítést is), külső margóját vagy belső térközét távolítják el. Tartalommal, kártyával, üzenettel, fejléccel és lábléccel is működnek, a webhelytémákban is. A beágyazott tartalom, gombok, beviteli vezérlők és a szakasz/rács térközei megtartják saját formázásukat; utóbbiakhoz a szakasz szélességét és egyéni CSS-osztályokat használja. Minden kapcsoló alapból kikapcsolt a meglévő és új elhelyezéseknél is. Az elhelyezési előnézet megtartja szerkesztési körvonalait, hogy a szegély nélküli cella is látható legyen; a renderelt megjelenéshez mentse a módosításokat.

Az **Oldalfejléc** widget **Hamburgermenü gombjának helye** beállítása **Webhely alapértéke**, **Bal** vagy **Jobb** lehet. A webhely meglévő reszponzív töréspontján az összecsukott navigáció gombját helyezi át; az asztali navigáció és az összecsukás működése nem változik. Az alapérték megőrzi a téma jelenlegi elhelyezését. Minden fejlécelhelyezés külön oldalt választhat; a rögzített kezelési/fiókfejléc a webhely alapértékét használja.

A widgetek közös rácscellákat foglalhatnak el. Az **Átfedés az előző szakasszal (px)** 0–2000 képponttal feljebb mozgatja a szakaszt; az első szakasznak nincs előzménye, ezért annak eltolása nem érvényesül. A **Réteg** (-100 és 100 között) külön szabályozza a szakaszok és az egyes szakaszokon belüli widgetek sorrendjét: a nagyobb szám előrébb kerül, egyezéskor a lista sorrendje számít. Különböző szakaszok között a szakaszok rétege elsőbbséget élvez a widgetek rétegével szemben. Az előnézet helykitöltő magasságokat használ, ezért a tényleges átfedés a megjelenített tartalomtól függ. Kis képernyőn az átfedő widgeteket tartalmazó szakaszok megtartják a rácsot, a többi szakasz widgetjei a beállított sorrendben egymás alá kerülnek. A szakaszok eltolása kis képernyőn is érvényes.

Hozzáadhat friss híreket, cikkeket, letöltéseket, vízszintes/függőleges menüket (menünévvel), egyéni formázott szöveget vagy HTML-t, csevegést, ütemezett [üzenetet](./managing-messages.md) (válassza ki a widget **Üzenet** listájából; amíg él, bezárható értesítést mutat) és telepített modulwidgeteket, például Fórumüzeneteket/statisztikát. A veszélyes HTML és parancsfájlok eltávolításra kerülnek; a HTML-szerkesztés nem JavaScript futtatására szolgál. Az adatok megtartják az eredeti tartalom hozzáférési szabályait.

A widgetek alapértelmezésben Bootstrap-kártyaként jelennek meg, ahol szükséges, formázott listákkal és statisztikákkal. A webhely témája testre szabhatja a színeket; az oldaltartalom megőrzi a korábbi megjelenését.

A menüwidgetek megőrzik a szülő- és gyermekhivatkozásokat: a vízszintes menük lenyíló almenüt, a függőlegesek behúzott almenüt mutatnak. Az egyéni widgetek formázott szöveges szerkesztőjében kép tölthető fel, illeszthető be vagy húzható be (PNG, JPEG, GIF vagy WebP, az oldalképek méretkorlátjával). Feltöltéshez `Configuration.Manage` szükséges; a képek az elrendezések VFS-ében tárolódnak, és a `/layouts/images/{id}` útvonalról érhetők el. Mentéskor a feltöltött képek az elrendezéskonfigurációhoz kapcsolódnak. A szövegigazítás és a biztonságos `ql-align-*` / `ql-image-*` HTML-osztályok a nyilvános oldalon is működnek; a tetszőleges CSS-osztályok és beágyazott stílusok biztonsági okból továbbra is törlődnek.

Rendeljen elrendezést pontos hash-útvonalhoz (`#/news`) vagy `*` jellel végződő előtaghoz (`#/site/*`); a legpontosabb egyezés nyer. Egyéb útvonalaknál az alapelrendezés érvényesül. A fiók- és adminisztrációs oldalak egyszerűek maradnak, kivéve a szerkesztő mentés utáni előnézetét. Az alapelrendezés törlése előtt válasszon másik alapértéket. A törlés átmeneti; fejlesztők megjeleníthetik a törölteket, visszaállíthatják vagy végleg törölhetik őket. Az életciklus-művelet után mentsen. Ütközésnél másik szerkesztő mentett előbb: újratöltés után próbálkozzon újra.

A menümódosítások automatikusan, az oldaltartalom elvesztése nélkül frissítik a menüket. Az elrendezéskonfiguráció biztonsági másolata a beállított adatkönyvtár `layouts.json` fájlja, az adatbázistól külön.

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
