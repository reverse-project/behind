# Fórum

A fő webes telepítés tartalmazza a **Fórumot**. A fórumtáblák kategóriához rendelhetők; a nyilvános fórumlista kategóriák szerint csoportosítja őket, a kategória nélküli táblák külön jelennek meg. Válasszon fórumtáblát, nyisson témát a válaszok olvasásához, vagy jelentkezzen be új téma és válasz írásához. A táblák és témák listája, a navigációs útvonal és a szerzői panelek egyszerűsített, mobilbarát fórumfelületet alkotnak. A szerző mellett a megjelenítendő név és profilkép látható. Üres név esetén az elsődleges e-mail-cím jelenik meg nyilvánosan. Csak az elérhetetlen fiókoknál jelenik meg az **Ismeretlen szerző** felirat. A függő és elutasított üzenetek nem nyilvánosak. A tábla eltávolítása vagy a téma elutasítása/törlése a válaszokat is elrejti.

## Üzenetek írása

A **Vizuális szerkesztő** eszköztárával félkövér, dőlt, aláhúzott és áthúzott szöveg, idézet, kód, lista és hivatkozás készíthető. A **BBCode** nézetben a forrás szerkeszthető, az **Előnézet** a megjelenést mutatja. A témák és válaszok BBCode-ként tárolódnak, legfeljebb **20 000 karakterrel, a címkékkel együtt**. A moderálási lista ugyanazt a formázást használja.

Az üzenetek, előnézetek és a moderálási lista hivatkozásai mindig megerősítést kérnek, a cél URL-jének megjelenítésével. Jóváhagyás után új lapon nyílnak meg, soha nem váltják le a fórum oldalát. A megszakítás nem nyitja meg a hivatkozást.

Támogatott címkék: `[b]félkövér[/b]`, `[i]dőlt[/i]`, `[u]aláhúzott[/u]`, `[s]áthúzott[/s]`, `[quote]idézet[/quote]`, `[code]kód[/code]`, `[list][*]elem[*]elem[/list]`, `[list=1][*]számozott elem[/list]`, `[url=https://example.com]hivatkozás[/url]` és `[url]https://example.com[/url]`. A hivatkozások HTTP-, HTTPS- és mailto-címeket fogadnak el. A `[lb]` és `[rb]` szögletes zárójelet jelöl. A meglévő egyszerű szöveg olvasható marad; a HTML szövegként jelenik meg, és soha nem fut le.

## Moderálás

Az **Adminisztráció > Fórum kezelése** (`Forum.Manage`) kezeli a fórumkategóriákat, táblákat és a felhasználói szabályokat. Hozzon létre kategóriákat a **Fórumkategóriák** részen, majd új fórumtábla létrehozásakor vagy szerkesztésekor válasszon kategóriát; a **Kategória nélkül** beállítással a tábla kategórián kívül marad. A hozzárendelt táblákkal rendelkező kategóriák csak akkor törölhetők, ha előbb áthelyezi vagy eltávolítja a tábláikat. Az **Adminisztráció > Fórum moderálása** (`ForumModeration.Manage`) ugyanezen oldal moderálási vezérlőit nyitja meg. Válassza a **Függőben**, **Közzétéve** vagy **Elutasítva** állapotot, majd a **Frissítés** gombot. A **Közzététel** jóváhagy egy üzenetet; az **Elutasítás** már közzétett üzenetet is visszavon.

A **Beállítások > forum.json** oldalon (`Configuration.Manage`) állítható:

- `Forum:Moderation`: `Post` (alapérték: azonnali megjelenés, utólagos moderálás) vagy `Pre` (jóváhagyás szükséges).
- `Forum:AllowBoardOverrides`: táblánkénti mód engedélyezése; a **Globális beállítás használata** örökli az alapmódot.
- `Forum:AllowUserOverrides`: felhasználónkénti mód engedélyezése. Az **Adminisztráció > Felhasználók** oldal azonosítójával szabály hozható létre/módosítható; törlése visszaállítja az öröklést.

Az engedélyezett felhasználói szabály elsőbbséget élvez az engedélyezett táblaszabállyal, majd a globális móddal szemben. A letiltott felülbírálások megmaradnak, de nem érvényesülnek. A beállítás újraindítás nélkül frissül, és **csak az új üzenetekre** hat, nem a meglévő függő/közzétett üzenetekre. A globális konfiguráció szerkesztése fejlesztői jog marad; a fórumkezelő csak engedélyezés után módosíthat felülbírálást.

A törlés átmeneti. A fejlesztők a **Töröltek megjelenítése** és **Frissítés** után visszaállíthatnak vagy végleg törölhetnek. A táblához rendelt kategória, illetve a tárolt üzenetekkel/válaszokkal rendelkező tábla/téma csak a hivatkozások vagy gyermekek eltávolítása után törölhető végleg. A változások verziózottak és auditáltak.

## Profilkép

A **Profil** oldalon válasszon legfeljebb **2 MiB** méretű PNG, JPEG, GIF vagy WebP képet; a mentés azonnali. Az **Eltávolítás** törli a profilkép-hivatkozást. A kép és a megjelenítendő név a fórumüzenetek mellett látszik; üres név esetén az elsődleges e-mail-cím nyilvános. Adjon meg nevet, ha nem szeretné megjeleníteni az e-mail-címét. A korábban letöltött képeket a böngésző gyorsítótára az eltávolítás után is megőrizheti.
