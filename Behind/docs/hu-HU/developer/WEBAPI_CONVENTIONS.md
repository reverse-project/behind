# WebApi konvenciók

## Kéréskultúra és naplókontextus

A kérés formázási és felületi kultúrája először a kiválasztott érvényes tartalmi nyelv, majd az ellenőrzött munkamenet felhasználójának mentett nyelve, végül a `Frontend:Site:DefaultLocale` (ennek hiányában `en-US`). Mindkét kultúra csak a kérés idejére érvényes, utána visszaáll. Az ezen belül keletkező OpenTelemetry napló tartalmazza a `session.locale` mezőt, ellenőrzött munkamenetnél pedig a `session.id`, `user.id`, `session.impersonated` és adott esetben `session.impersonator.id` mezőt. Ezek nem függenek az OpenTelemetry `IncludeScopes` beállításától; hitelesítési adat, IP-cím és böngészőazonosító nem kerül hozzájuk.

## Privát üzenetek és felhasználói jelentések

A privát API aktív munkamenetet követel és ETag nélkül `Cache-Control: no-store` fejlécet használ. A `PrivateMessage.Read/Write/Delete` a postaládaműveleteket szabályozza; a tagság és az üzenetek láthatósága külön ellenőrzött. A `PrivateMessageModeration.Manage` csak a rendszerválaszokat teszi elérhetővé, nem ad korlátlan privát hozzáférést.

A `GET /private-messages` mappát/lapozást fogad; az írás, válasz, tagság, postaláda és tiltólista a `/private-messages` útvonalat használja. A `POST /reports` felhasználóazonosítókat, indoklást és hozzáférhető forrást fogad. A kiszolgáló megőrzi a bizonyítékot és azonosítja a szerzőt, a névtelen személy bejelentőnek való felfedése nélkül. A `Report.Manage` védi a listázást, ellenőrzést és lezárást; a fejlesztői visszaállítás/végleges törlés `RawDatabase.Manage` jogosultságot igényel. A kézbesítés küldés előtt megmarad; megszakított küldés ismeretlen eredmény, nem automatikus újrapróbálás.

A `GET /private-messages/blocks` `page` (1–1 000 000) és `pageSize` (1–100, alapérték 20) paramétert fogad, és `{items,total,page,pageSize,pageCount}` választ ad, nyilvános megjelenítési címke és felhasználóazonosító szerint rendezve. A jelentés részletei tartalmazzák az opcionális `resolvedById` és `resolvedByName` mezőt, a kezelő adminisztrátor nyilvános profilcímkéjével.

A jelentés célpontválasztója a `GET /private-messages/recipients?reportTargets=true&search=...` útvonalat használja: csak emberi fiókot ad vissza, a beépített System és Guest azonosító nélkül, szerepkörök nélkül. A jelentés beküldése a beépített azonosítókat explicit és forrásból származó célpontként is elutasítja.

Minden címzettkeresés kizárja a System, Guest és bejelentkezett felhasználó azonosítóját az eredménykorlát alkalmazása előtt, üzenetírás, meghívás, tiltás és jelentés esetén is.

A címzettkeresés kérésenként legfeljebb 10 felhasználót és 10 szerepkört ad vissza, a korlátozás előtt megjelenítési név/név és azonosító szerint rendezve.

Minden privát beszélgetés csoportként jelenik meg és működik, a régi `IsGroup=false` sor is; az új beszélgetés `IsGroup=true` értéket tárol. A régi `isGroup` kérésmező elfogadott, de nem változtatja meg a működést. Legalább egy, a feladótól különböző címzett szükséges. Látható és rejtett szerepkör címzéséhez és meghívásához `PrivateMessageModeration.Manage` kell; e jogosultság nélkül a címzettkeresés nem ad szerepköröket. A kétszemélyes közvetlen válasz továbbra is ellenőrzi a tiltólistát. A rendszer- és jelentésbeszélgetések megőrzik korlátozott tagságukat és válaszláthatóságukat.

A gazda regisztrálja a `PrivateMessageEmailService` szolgáltatást, amely percenként dolgozza fel a függő/ismerten sikertelen kézbesítést az `IModuleEmailSender` használatával, eredeti megerősített címenként legfeljebb három próbálkozással. Abszolút HTTP(S) `Frontend:Url` kell; a hiányzó/hibás URL naplózott hiba, az értesítés függő marad. Az e-mail a `#/private-messages/<beszélgetés-azonosító>` útvonalat nyitja meg, a szokásos hitelesített beszélgetéshozzáféréssel. Tárgy/tartalom/résztvevőazonosság nincs az e-mailben. A tagságot küldés előtt újra ellenőrzi; megszűnt hozzáférés törli a kézbesítést. Az ismeretlen megszakítás küldés alatt hagyja a rekordot, a szolgáltatói/beállítási hiba naplózott.

A modulok Core-függőség nélkül a Common.WebApi `IReportEvidenceSource` megvalósításával adnak bizonyítékot. Minden forrás megtartja az eredeti láthatóságot, és nem hozzáférhető tartalomnál null értéket ad.

## Kapcsolat és modulok e-mail-küldése

A Contact nyilvános API-ja az aktív űrlapsémákat adja és beküldést fogad, soha nem adminisztrátori beérkezett adatot vagy címzettbeállítást. A nyilvános beküldési végpont kliensenként korlátozott, és ellenőrzi a választott tárgyat és a beállított kiegészítő mezőket. A kezelési API-hoz a Contact adminisztrációs engedélyei szükségesek; a törölt lista, visszaállítás és végleges törlés ezenfelül `RawDatabase.Manage` engedélyt igényel. Az olvasottnak jelölés az `IModuleIdentity` felületen azonosítja az aktuális adminisztrátort, nem kliens által megadott felhasználóazonosítóval.

A modulok a Common.WebApi `IModuleEmailSender` felületén küldenek; a Core adaptere az aktuálisan beállított e-mail-szolgáltatót használja, és továbbítja a kézbesítési hibát. A megszakítást az ezt támogató szolgáltatóknak továbbítja; a szabványos, nem megszakítható e-mail-felületnél csak a várakozás szakad meg, ezért a kézbesítés eredménye bizonytalan marad. A Logger szolgáltató naplózás után sikert jelez, de nem küld e-mailt postaládába. A Contact a küldés előtt menti a kérést, és hibaállapotot tart meg engedélyezett újrapróbáláshoz; a nyilvános válasz privát rekordok kiszivárogtatása nélkül különbözteti meg a mentést a kézbesítéstől.

A gazdák az `AbstractCoreContext` `IModuleModelContext` felületével és a Common.Database `ModuleModelCacheKeyFactory` regisztrációjával telepített katalógus szerint választják külön az EF-modelleket. A modulkatalógus telepítési metaadat, nem kliens által választható API-paraméter.

A telepített interaktív widgetmodell a `load(type, limit, widget)` eredményében regisztrált `{component: {name, params}}` komponenst adhat; ez frontend-szerződés, nem végrehajtható jelölést tartalmazó API-válasz. A Core Knockout-komponenskötéssel jelenít meg, és felszabadításkor vagy identitásváltozáskor elutasítja az elavult válaszokat.

Contact útvonalak:

- A `GET /contact/forms` és `GET /contact/forms/{id}` címzett nélküli nyilvános sémát ad. A `POST /contact/forms/{id}/requests` `{name,email,subject,message,fields:{key:value}}` adatot fogad rendre 200/320/200/10 000 karakterkorláttal. Kliens-IP-nként tíz perc alatt öt beküldés engedélyezett. Siker esetén 201 és `{deliveryStatus}` érkezik jogosultságvédett kezelési erőforrásra mutató Location fejléccel; a `sent`, `failed` és `sending` különbözteti meg az elfogadást az e-mail eredményétől.
- A `GET /contact/manage/forms` adminisztrációs tömböt ad `Contact.Read` engedéllyel. A `GET /contact/manage/requests` lapozott választ (`total`, `page`, `pageCount`, `pageSize`, `items`) ad, és elfogadja a `page`, `pageSize`, `search` és `unreadOnly` paramétert. A keresés a feladó nevére vagy e-mail-címére, az űrlap nevére, a tárgyra és az üzenetre illeszkedik. Az egyedi kezelési olvasás azonos útvonalat használ `/{id}` utótaggal. A `?deleted=true` csak `RawDatabase.Manage` engedéllyel választ törölt rekordot.
- A kéréslistában és a részletekben a `createdAt` az első verzió auditbejegyzéséből származik, hiányzó bejegyzés esetén null. A modulok ezeket az időket kötegelve a Common.Database `IModuleCreationOrder.CreatedAtAsync` felületén olvassák.
- A kérésválaszokban a `readBy` megőrzi a null is lehető olvasóazonosítót, a `readByName` pedig a nyilvános profilcímkét tartalmazza. Olvasatlan kérésnél mindkettő null; hiányzó profilnál a stabil `user#tag` helyettesítő érték jelenik meg. A címkéket az `IModuleUsers` kötegelve oldja fel.
- A `POST /contact/manage/forms` és `/contact/manage/forms/{id}` `Contact.Write` engedéllyel `{name,description,recipient,subjectMode,subjects,fields}` adatot fogad. Módosításkor `version` is szükséges; elavult verzióra 409 érkezik. A `subjectMode` `choices` (1–30 egyedi tárgy) vagy `freeText` (üres tárgylista). Legfeljebb 30 kiegészítő mező `{key,label,type,required,maxLength}` alakú, `text`, `textarea` vagy `email` típussal és 1–2000 közötti legnagyobb hosszal.
- A `POST /contact/manage/requests/{id}/read` `{read:boolean}` adatot fogad, a `/retry` sikertelen kézbesítést próbál újra; mindkettő `Contact.Write` engedélyt igényel. Az olvasási állapot az aktuális adminisztrátort azonosítja. Küldés alatt álló vagy elküldött kérés nem próbálható újra. Az SMTP előtt tartós küldésfoglalás akadályozza a párhuzamos ismétlést; megszakított, ismeretlen eredménynél az állapot sending marad, nem lesz automatikusan failed.
- A `DELETE /contact/manage/{forms|requests}/{id}` `Contact.Delete` engedéllyel átmenetileg töröl. A `POST .../{id}/restore` és `DELETE .../{id}/hard` `RawDatabase.Manage` engedélyt igényel. A beérkezett rekord törlése nem vonja vissza az e-mailt.

## Tartalmi nyelvek

A nyilvános tartalomkérés az `X-Content-Locale` fejlécben választ opcionális nyelvet; a nyelv nélküli tartalom minden kiválasztott nyelvvel megosztott, nyelvválasztás nélkül pedig csak a nyelv nélküli tartalom érvényes. A nyelvhez rendelt tartalom kizárólag a kiválasztott nyelven látható. A nyelv kérési környezet, soha nem része az útvonalnak. A frontend alapértelmezésként a bejelentkezett felhasználó beállítását, a böngésző gyorsítótárát vagy a webhely alapértelmezését használja, és elküldi a tartalomkérésekkel. Az ismeretlen nyelvkód hibás bemenet, nem utasítás nyelv nélküli tartalomra való visszaesésre. A nyelvszűrés a meglévő hozzáférési, közzétételi, korhatár- és törlési ellenőrzések mellett érvényes, adatfolyamokra, darabszámokra és widgetekre is. Az engedélyezett kezelési kérések minden nyelvet szerkeszthetnek; a szerkesztési jogosultság önmagában nem kerüli meg a szűrést a nyilvános olvasásnál.

A `GET /content-locales` `{locales:[codes]}` választ ad. A `GET /content-locales/{entityType}/{id}` `{locale,effectiveLocale}` választ ad; a `POST /content-locales` `{entityType,entityId,locale}` adatot fogad a tartalomtípus meglévő szerkesztési jogosultságával, és 204-et ad. Null törli a hozzárendelést: a gyökér nyelv nélkülivé válik, a gyermek örököl. A `POST /content-locales/query` legfeljebb 100 elemes `{items:[{entityType,entityId}]}` kérést fogad, és `{items:[{entityType,entityId,status,locale,effectiveLocale}]}` választ ad a meglévő szerkesztők metaadataihoz. A szerkesztők helyben használják ezeket, külön kezelőoldal nélkül. Az oldal-, útvonalálnév-, letöltésimappa- és Sport-sportág-mentés opcionális `contentLocale` mezőt fogad az atomi hozzárendeléshez; elhagyása megőrzi a tárolt értéket.

A fejlécet küldeni nem képes média a validált `contentLocale` lekérdezési paramétert használja. A kezelési láthatóságot a tényleges végpontútvonal, HTTP-metódus és engedélyezett végpontszabályok határozzák meg, nem a kliens által küldött minden-nyelv fejléc. A modul az entitásneveket a `ModuleDatabase.ContentLocaleEditPolicies` mezőben rendeli a meglévő szerkesztési szabályokhoz; a tulajdonosellenőrzést az `IContentLocalePermission` végzi.

## Ütemezett webhelyüzenetek

A `GET /messages/manage` `Message.Read` engedélyt követel, és `{messages: {total, page, pageCount, pageSize, items}, letters}` választ ad `page`, `pageSize`, `search`, `letter` és `deleted` lekérdezési paraméterekkel. A töröltek listázásához `RawDatabase.Manage` szükséges. A `GET /messages/{id}` `Message.Read` engedélyt követel; törölt részleteket csak fejlesztők érhetnek el. A `POST /messages` és `POST /messages/{id}` `Message.Write` engedéllyel `{name, title, content, level, startsAt, endsAt}` adatokat fogad. A szint `info`, `success`, `warning` vagy `danger`; az időhatárok opcionális ISO-időbélyegek, a cím opcionális. Hibás bemenetre 400, sikeres mentésre `{id, version}` jár. A `POST /messages/{id}/delete` `Message.Delete`, a `/restore` és `/hard/delete` `RawDatabase.Manage` engedélyt követel.

A nyilvános `GET /widgets/messages/{id}` csak a legújabb, nem törölt és a `[startsAt, endsAt)` időszakban lévő üzenetre ad `{id, version, title, content, level, startsAt, endsAt}` választ, egyébként 404-et. A válasz megkerüli az ETag-kezelést, és `Cache-Control: no-store` fejlécet használ. A kézzel elhelyezett `core.message` widget a `message` beállításban választ azonosítót, percenként lekérdez, a tisztított tartalmat bezárható értesítésként jeleníti meg, és eltávolításkor megszünteti az időzítőt. Példák: `Messages.http`.

## Elrendezésvégpontok és érvénytelenítés

A `GET /layouts` aktív elrendezéseket és hozzárendeléseket ad. A `GET /layouts/manage` `Configuration.Read` engedélyt követel, no-store választ ad, és csak `RawDatabase.Manage` mellett tartalmaz törölt elrendezéseket. A `POST /layouts` `Configuration.Manage` engedélyt követel, figyelembe veszi a csak olvasható konfigurációt, és az utoljára olvasott revízióval együtt fogadja a teljes konfigurációt. Siker esetén a mentett konfigurációt adja; elavult revízióra 409, hibás rácsra/hivatkozásra 400, jogosulatlan töröltrekord-módosításra 403 jár. Átmeneti törléshez állítsa be a törlésjelzőt; a fejlesztő visszaállításhoz törli a jelzőt, végleges törléshez elhagyja a már törölt elrendezést. Aktív elrendezés nem hagyható el egyszerűen. A nyilvános válaszban nincs törölt elrendezés. Példák: `Layouts.http`.

A felületi `models/invalidation` megszüntethető témakör-feliratkozásokat és aszinkron érvénytelenítést ad. A megfelelő régióhoz a `menus`, `layouts`, `widgets` vagy `widget:<widgetkulcs>` témakört használja; a komponens eltávolításakor szüntesse meg a feliratkozást. A sikeres tartalommódosítások érvénytelenítik az érintett widgettípusokat az aktuális böngészőben; más böngészők módosításai nem érkeznek automatikusan. Menümódosításhoz ne töltse újra a teljes oldalt. A modulleírók widgetjeit a `/modules` adja át, és a betöltők megtartják a modul saját hozzáférésszűrését.

Az elrendezésválasz `schemaVersion: 1` értéket tartalmaz; teljes oldalas elrendezés mentésekor ezt a revízióval együtt küldje vissza. A szakasz és widget `cssClass` mezőt fogad (alapból üres, legfeljebb 500 karakteres, szóközzel elválasztott ASCII-osztálynevek). Hibás osztály vagy nem támogatott sémaverzió mentéskor 400 választ ad. A nyilvános elrendezés opcionális `core.header` és `core.footer` widgetet tartalmazhat; a kezelési/fiók/korlátozott oldalak megtartják a rögzített keretet, kivéve a szerkesztő kiválasztott mentett elrendezésének előnézetét az oldal elhagyásáig. A korábbi fájlok olvasáskor, automatikus írás nélkül frissülnek.

A widgetelhelyezések egymástól független `borderless`, `noMargin` és `noPadding` logikai értékeket is fogadnak. Elhagyva alapértékük hamis; a mentési válasz és a későbbi olvasás megőrzi őket. A cella/widget keretére hatnak, nem a szakasz/rács térközeire vagy a beágyazott tartalomra.

Minden elrendezés opcionális `badges: [{ id, text, image, link, position, width }]` listát tartalmazhat. A jelvényképek a `POST /layouts/images` végponton tölthetők fel, mentéskor az egyéni widgetek képeivel együtt az elrendezés VFS-szülőjéhez kapcsolódnak, a megtartott törölt elrendezéseket is beleértve. Csak elrendezéskép-URL fogadható el. Hibás jelvénytartalom, hivatkozás, pozíció, szélesség vagy ismételt azonosító 400 választ ad. Az elhagyott `badges` értéke `[]`; sémaverzió-váltás nem szükséges.

A `GET /widgets/news` az élő híreket audit-létrehozási idő szerint csökkenően adja, a kiemelés elsőbbsége helyett. A `GET /widgets/downloads` összegyűjti a látható mappákat és globálisan a legfrissebb letöltéseket adja. A `POST /widgets/sanitize` legfeljebb 100 000 karakteres `{content}` mezőt fogad, és tisztított `{content}` mezőt ad vissza. A Fórum `/forum/widgets/posts?limit=N` végpontja közzétett/nem törölt táblára és témára szűr, audit szerinti időrenddel; a statisztika táblákat, témákat és közzétett üzeneteket számol.

### Csevegési végpontok

A `GET /widgets/chat?limit=20&before=<üzenet-id>` `{items, nextCursor, canWrite}` választ ad. Audit-létrehozási idő és kulcs szerinti holtverseny-felbontás alapján kiválasztja a legújabb üzeneteket, majd az egyes oldalakat a legrégebbitől a legújabbig adja vissza. A legújabb oldalhoz hagyja el a `before` mezőt; a `limit` 1–50 lehet. Felfelé görgetve a korábbi oldalak az előzmény elejére kerülnek. A kurzor üzenetének moderált eltávolítása után is használható. A `GET /widgets/chat/{id}` látható üzenetet vagy 404-et ad. Az olvasás no-store; vendégek olvashatnak, bejelentkezve `Chat.Read` kell. E jogosultság korlátozása a listát és az egyedi olvasást is tiltja, de kijelentkezés után a vendégként olvasást nem akadályozhatja meg.

A `POST /widgets/chat` `Chat.Read` és `Chat.Write` engedélyt követel, `{text, replyToId?}` adatot fogad, levágja a környező szóközöket és 1 000 karakterre korlátozza a szöveget, ellenőrzi a válasz látható szülőjét, és a szerzőt a munkamenetből veszi. Sikerre 201 és üzeneterőforrás-URL jár. A `PUT /widgets/chat/{id}` ugyanezeket az engedélyeket és `{text, version}` adatot követel; csak a szerző szerkeszthet, szigorúan a létrehozást követő öt perc lejárta előtt. Elavult verzióra 409, más szerző vagy lejárt idő esetén 403 jár. Moderátor sem kerülheti meg ezt a szabályt. A `DELETE /widgets/chat/{id}` `Chat.Read` mellett saját üzenethez `Chat.Delete`, bármely üzenethez `Chat.Manage` engedélyt követel. Minden verziót átmenetileg töröl és 204-et ad (hiányzó üzenetre 404-et). Minden engedélyhez van korlátozás, amely felülírja az engedélyt. A Users szerepkör `Chat.Read` és `Chat.Write`, az Administrators mind a négy Chat engedélyt megkapja.

Az üzenetválasz profilkép-URL-t, auditból vett `createdAt`/`updatedAt` időt, `editableUntil` mezőt, szerver által számított szerkesztési/törlési jelzőket és a legújabb látható válaszelőnézetet tartalmazza. Eltávolított szülőnél az előnézet null, korábbi szöveg soha nem szerepel benne. Az üzenetek és előnézetek szövegként, nem HTML-ként jelennek meg. Példák: `Chat.http`.

## Fórum- és profilvégpontok

A `GET /content-restrictions/browse/{entityType}?page=1&search=...` `RawDatabase.Manage` engedélyt követel, és `{total,page,pageCount,pageSize,items:[{id,displayName}]}` választ ad, oldalanként 20 elemmel. Csak regisztrált, korhatárszűrhető típus fogadható el. A legújabb nem törölt verziókat és a meglévő láthatósági szűrőket használja, címkére vagy teljes UUID-re keres, és a leképezett cím/megjelenítési név/fájlnév/név/szöveg mezőket választja UUID-helyettesítéssel. A szabálylista a közös auditcímke-forrásokkal feloldott `displayName` mezőt is tartalmazza. A válaszok nem gyorsítótárazhatók.

A `Backend.ContentRestrictions.Enabled` alapértéke false, a `backend.json` fájlból újratölthető. Kikapcsolva a korhatárok megmaradnak, de nem szűrnek. Bekapcsolva ismeretlen születési dátummal a korhatáros tartalom nem érhető el. A `GET /content-restrictions/{entityType}/{id}` válasza `{enabled,minimumAge}`, a POST `/content-restrictions` pedig `{entityType,entityId,minimumAge}` adatokkal új szabályverziót ment. Mindkettőhöz `ContentRestriction.Write` és az elem nyelvi szerkesztési vagy tulajdonosfüggő jogosultsága kell. Az új pozitív korhatárnak aktív választható értéknek kell lennie, de a meglévő számszerű korhatár a választható érték eltávolítása után is megtartható. A `GET /content-age-options` listázza az értékeket, a `POST /content-age-options` újat ad hozzá, a `POST /content-age-options/{id}/delete|restore|hard/delete` pedig a `ContentAge.Manage` jogosultsággal kezeli az életciklust. A törölt értékek listázásához, visszaállításához és végleges törléséhez `RawDatabase.Manage` is kell. Az eltávolítás megőrzi a meglévő korlátozásokat. A `ModuleDatabase.ContentRestrictedEntities` és `ContentRestrictionParentProperties` modulhorgokat a `ModuleCatalog` összesíti a közös tranzitív tartalom-, média- és darabszámszűréshez.

A korhatárválaszték kötelező megjelenített `name` mezőt tartalmaz (legfeljebb 200 karakter). A létrehozás `{age,name}` adatokat fogad; a `POST /content-age-options/{id}/update` `{name}` adatokkal módosítja a nevet, és `ContentAge.Manage` jogosultságot igényel. A lista válasza `{items:[{id,age,name}]}`. Az átnevezés nem módosítja a korhatárt vagy a meglévő hozzárendeléseket.

A nyilvános szerzőhivatkozások a `#/user/<id>` profilra mutatnak. A `GET /users/profiles/{id}` csak nyilvános profiladatokat és szerver által számított kezelési jogosultságokat ad; a születési dátum nem nyilvános, az elsődleges e-mail csak engedélyezett megosztásnál jelenik meg. A hitelesített `GET/PUT /profile/details` a `{birthday, hideEmail}` adatokat olvassa/menti. Az e-mail alapértelmezés szerint rejtett. A név a `Guid.ToUserLabel` állandó, teljes UUID-ből származó, nyolc kisbetűből és számjegyből álló hashét használja, sohasem e-mail-helyettesítést. A C# és JavaScript a kötőjelek nélküli, kisbetűs UUID 64 bites FNV-1a hashének utolsó nyolc, 36-os számrendszerbeli számjegyét használja, szükség esetén kezdő nullákkal. A profil moderálása és megszemélyesítése a meglévő felhasználókezelési jogosultságokat követi.

A profilválasz `moderation` objektuma csak érvényes munkamenet és `User.Manage` jogosultság esetén tartalmaz adatot: nem nyilvános profilbeállításokat, az aktuális e-mail-címek megerősítési állapotát, a megőrzött kitiltásokat, az érvényes és közvetlen hozzáférést, valamint titkok nélküli kétlépcsős összesítést. A megszemélyesítési jogosultság önmagában nem ad hozzáférést. A megőrzött kitiltások a lágyan törölteket is tartalmazzák; a végleg törölt bejegyzések nem érhetők el.

A hírhozzászólások, csevegőüzenetek és fórumüzenetek hitelesített szerzőtől `isAnonymous` mezőt fogadnak. Névtelen válaszban az egyszerű olvasó null szerzőazonosítót, üres nevet és null képet kap, a csevegési válaszelőnézetben is. A valódi szerző a tulajdonjog ellenőrzéséhez megmarad. Felfedéshez rendre tényleges `News.Delete`/`News.Manage`, `Chat.Manage`, illetve `ForumModeration.Manage`/`Forum.Manage` engedély kell; a korlátozások érvényesek, a szerepkör neve önmagában nem jogosít. A moderátor a valódi szerzőt és nyilvánosprofil-hivatkozást látja. A vendégküldés változatlanul tiltott.

Új elem alapértelmezés szerint nem névtelen. Szerkesztésnél az elhagyott `isAnonymous` megtartja a tárolt értéket; csak a kifejezett false kapcsolja ki.

A `/forum/boards` és `/forum/posts?boardId=<id>&topicId=<id>` lapozott nyilvános böngészést ad; témákhoz a `topicId` elhagyandó. A `GET /forum/posts/{id}` csak jelenleg látható üzenetet ad vissza. A `POST /forum/posts` `Forum.Read` engedélyt és érvényes munkamenetet követel, a szerzőt és moderálási állapotot a szerver számítja, és `201` választ ad erőforrás-URL-lel. A moderáláshoz (`GET /forum/manage/posts?state=Pending`, `POST /forum/posts/{id}/review`) `ForumModeration.Manage` kell. A táblák és felhasználói szabályok kezeléséhez `Forum.Manage` szükséges. A `/delete`, `/restore`, `/hard/delete` utótagok a meglévő modulmintát követik; a töröltek listázásához, visszaállításhoz és végleges törléshez `RawDatabase.Manage` kell. Az állapot- és moderálási enumok JSON-ban szövegesek. A `GET /forum/settings` az újratölthető `forum.json` tartalmát adja; a meglévő Beállítások API `Configuration.Manage` engedéllyel szerkeszti.

A Core hitelesített `GET/POST/DELETE /profile/picture` végpontokat biztosít; a POST multipart `file` mezőt fogad, ellenőrzi a felismert kép-MIME-t és a 2 MiB mérethatárt, és a munkamenet felhasználójához kapcsolja a feltöltést. A `GET /profile/images/{id}` csak aktuális profilképet szolgál ki változatlan tartalomhoz való gyorsítótárazással és `nosniff` fejléccel. Az `IModuleIdentity` és az `IModuleUsers` a közös integrációs pontok; a modulok szerzőadatokért sem függhetnek a Core-tól.

Ez a dokumentum a Behind projektben a REST API-k építésének konvencióit, mintáit és legjobb gyakorlatait ismerteti.

## Áttekintés

A Behind WebApi réteg az alábbiakra épül:
- **ASP.NET Core 11.0.0**
- **Microsoft.AspNetCore.OpenApi 11.0.0** Scalar.AspNetCore 2.14.11 felülettel
- **JWT ******
- **Strukturált hibakezelés**
- **Railway-Oriented Programming** (monadikus visszatérési értékek)

## Indítási konfiguráció

### WebApplicationBuilder bővítmények

A `Behind.Common.WebApi.WebApplicationBuilderExtensions` fluent konfigurációt biztosít:

```csharp
extension(WebApplicationBuilder builder)
{
    public WebApplicationBuilder AddOpenApi()
    {
        builder.Services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        });
        return builder;
    }

    public WebApplicationBuilder AddOpenTelemetry()
    {
        // Elosztott nyomkövetés, metrikák és naplózás konfigurálása
        // Konfigurációs fájlon keresztül (appsettings.json)
    }
}
```

### OpenTelemetry hibakeresés

Az `OpenTelemetry` szakasz egyszer, induláskor kötődik be. Az `Instruments` elfogadott értékei: `AspNetCoreInstrumentation`, `HttpClientInstrumentation`, `RuntimeInstrumentation` (metrikák) és `OtlpExporter`; az átalakítást `TypeConverter` végzi. Minden exportálandó jelnél szerepeljen az `OtlpExporter`. Az ismeretlen nevek, érvénytelen értékek és ismeretlen tulajdonságok indítási hibát okoznak a műszerek csendes kihagyása helyett.

`HttpProtobuf` esetén minden `Endpoint` a teljes OTLP adatfogadó URL; az `AddOtlpExporter` nem fűzi hozzá a `/v1/traces`, `/v1/metrics` vagy `/v1/logs` útvonalat az explicit beállított URL-hez. Egyedi proxyútvonal csak akkor használható, ha a megfelelő OTLP fogadóhoz továbbít. A Grafana felülete, a Tempo lekérdezési API és a Prometheus scrape végpont nem OTLP fogadó.

Az SDK és az exporterek hibáinak megjelenítéséhez (nem az alkalmazás szokásos naplóihoz) helyezz el egy `OTEL_DIAGNOSTICS.json` fájlt a folyamat munkakönyvtárában vagy az alkalmazás könyvtárában:

```json
{
  "LogDirectory": "/tmp",
  "FileSize": 1024,
  "LogLevel": "Verbose",
  "FormatMessage": true
}
```

A folyamat felhasználójának írási joggal kell rendelkeznie a `LogDirectory` könyvtárhoz. Konténerben csatold a konfigurációt az alkalmazás mellé, és a létrejövő `<ExecutableName>.<ProcessId>.log` fájlt a konténerben vizsgáld (vagy csatold ki a naplókönyvtárat). Az SDK körülbelül 10 másodpercenként újraolvassa a diagnosztikai konfigurációt. Generálj egy kérést és egy alkalmazásnaplót, majd várj legalább 60 másodpercet az alapértelmezett metrikaexportálási időköz miatt. Keresd a DNS/TLS hibákat és a HTTP állapotkódokat: a `401`/`403` hitelesítési/jogosultsági hibát, a `404` rossz útvonalat, a `405` rossz metódust/útvonalat, a `415` inkompatibilis fogadót/tartalomtípust jelez. Az átirányítás vagy HTML-válasz gyakran azt jelenti, hogy az exporter felületet vagy bejelentkezési oldalt ért el. A Serilog részletességének növelése önmagában nem rögzíti ezeket az SDK-diagnosztikákat. Hibakeresés után távolítsd el a diagnosztikai konfigurációt; a kimenet érzékeny részleteket tartalmazhat.

### Élő konfiguráció

Minden JSON konfigurációs fájl az `IHostApplicationBuilder.AddReloadableJsonFile(path, optional)` (`Behind.Common.Configuration`) segítségével töltődik be, amely egy JSON-forrás, és figyeli a fájlt. Ha egy fájl a betöltés után érvénytelen JSON-né válik, az utolsó jó értékek megmaradnak, a hibát pedig a `ConfigurationReloadFailures` jelzi (a `ConfigurationReloadFailureLogger` naplózza).

A fájlokat követő beállítások az `AddReloadableOptions<T>(...)` hívással regisztrálandók: egy `ReloadableOption<T>` válaszol az `IOptions<T>`, az `IOptionsSnapshot<T>` és az `IOptionsMonitor<T>` kéréseire az aktuális értékkel, az `OnChange` eseményt csak akkor értesíti, ha a kötött érték megváltozott, és megtartja az előző értéket, ha az új nem köthető. A `SecurityConfiguration`, az `EmailConfiguration`, a `DatabaseConfiguration`, a `BackendConfiguration` és a `FrontendOptions` ezt használja, így a befecskendezett `IOptions<T>` élő. A fogyasztóknak a `.Value` értéket használatonként kell olvasniuk, és soha nem szabad mezőben tárolniuk vagy indításkor rögzíteniük. Kapcsolódó élő elemek:

- Az `AddOptionsCacheInvalidation<TSource, TTarget>()` törli a `TTarget` gyorsítótárazott elnevezett opcióit (a `JwtBearerOptions` a ****** amikor a `TSource` megváltozik.
- A `UseReloadable<T>(configure)` (`Behind.Common.WebApi`) az aktuális opciókból épít middleware-ágat, és újraépíti, amikor azok megváltoznak (`UseStaticFrontend`).
- Az `AddClientPolicy(name, Func<HttpContext, (int, TimeSpan)>)` kérésenként olvassa a kérésszám-korlátot (a korlátok a partíciókulcs részei); a magic link szabályzatok az `IMagicLinkSettingsStore` tárolót olvassák.
- Kétfaktoros szolgáltatások (`AddTwoFactor()`): a `TwoFactorTickets` és a `PasskeyCeremonies` singleton, a többi scoped; a második lépés végpontjai a `two-factor` kliens kérésszám-korlátot osztják meg, és `{ code }` hibákat adnak vissza (`TwoFactorFailure`). A `Fido2` csomag deklarál egy `None` nevű típust, ezért a típusait a `TwoFactor/Fido2Aliases.cs` globális aliasain keresztül importáljuk a `using Fido2NetLib;` helyett.
- Az e-mail küldők tranziensek, és az aktuális `EmailConfiguration` alapján jönnek létre; a `MagicLinkMailService` levelenként hozza létre a küldőjét.

A generált abszolút frontendhivatkozásokhoz és a hozzáférési kulcsok eredetéhez kifejezetten beállított kanonikus `Frontend:Url` szükséges a `frontend.json` fájlban, például `{ "Frontend": { "Url": "https://example.com/" } }`. Abszolút HTTP(S) URL legyen felhasználói adatok, lekérdezés és töredék nélkül; az alkalmazás alapútvonala támogatott. Éles környezetben HTTPS-t használj; helyi fejlesztéshez a kifejezetten beállított HTTP továbbra is használható. A közös `FrontendUri()` minden műveletnél az aktuális értéket ellenőrzi; sem a `Host`, sem a továbbított fejlécek nem helyettesítik azt. Hiányzó vagy hibás beállításnál a magic-link kérés hibát naplóz és `500` választ ad a hitelesítő adat kiadása előtt, ismeretlen e-mail-cím esetén is. A megerősítés küldése hibát naplóz, tokent nem hoz létre és nem küld; a regisztráció és az újraküldés meglévő, felhasználófelderítést gátló válaszai megmaradnak, a profil mentése `emailSent:false` értéket jelez, a profilbeli újraküldés pedig `503` választ ad. A privát üzenetek értesítő e-mailjei függőben maradnak, naplózott hibával. A hozzáférési kulcsok műveletei a szokásos kivételkezelőn keresztül hibával leállnak, nem a kérésből származtatják az eredetet. A relatív navigációs, API- és médiaútvonalak, valamint a hordozható generált dokumentáció hivatkozásai relatívak maradnak; a külső és felhasználói URL-ek nem változnak. E funkciók engedélyezése előtt állítsd be az URL-t, és korlátozd az elfogadott gazdaneveket az alkalmazásban és a belépési proxyn.
- Az adatbázis kapcsolati karakterlánca és a VFS tartalomtár gyökere hatókörönként / feloldásonként kerül meghatározásra.

Csak az adatvédelmi tanúsítvány és kulcsok, az OpenTelemetry beállításai és az adatbázis-szolgáltató olvasódnak be egyszer, induláskor.

### Statikus frontend

A `UseCore()` a `Behind.Core.WebApi/Frontend` könyvtárból beágyazott JavaScript frontendet szolgálja ki (RequireJS, jQuery, Knockout, Sammy; controllers/models/viewmodels/views elrendezés). Az `index.html` az alapértelmezett fájl. A JS, CSS és HTML fájlokat a memóriában minifikálja (fájlverziónként egyszer), a válaszokat pedig alapértelmezés szerint Brotli/Gzip tömörítéssel, ETag-újraellenőrzéssel adja vissza.

A beállítás a `Frontend:StaticFiles` szakaszon keresztül történik (például a `frontend.json` fájlban):

```json
{ "Frontend": { "StaticFiles": { "Minify": true, "Compress": true, "DefaultFile": "index.html", "CacheControl": "no-cache" } } }
```

A `*.min.*` nevű fájlokat változtatás nélkül szolgálja ki. A külső gyártói könyvtárak a `Frontend/libraries` könyvtárban találhatók.

A képeket (png, jpg, gif, webp, avif, ico, svg) változtatás nélkül, a helyes tartalomtípussal és ugyanazokkal a gyorsítótár- és biztonsági fejlécekkel szolgálja ki; csak az SVG kerül tömörítésre. Ha a tartalomgyökérben létezik `wwwroot` könyvtár, annak fájljait a beágyazott frontend mellett szintén kiszolgálja, és az azonos útvonalú beágyazott fájlokkal szemben elsőbbséget élveznek.

### Lokalizáció

A frontend szövegei a `Frontend/locales/<code>.json` JSON-fájlokból származnak (`en-US` és `hu-HU`): `{ "name": "Magyar", "messages": { "key": "text {0}" } }`. A `name` a nyelvi beállítás hivatalos neve, amelyet a Beállítások oldal megjelenít, a `{0}` stílusú helyőrzők pozíció szerint töltődnek ki. A `wwwroot/locales` könyvtárban lévő fájlok felülírják a beágyazottakat, és új nyelvi beállításokat adnak hozzá (a `wwwroot` szolgáltató élvez elsőbbséget, lásd: Statikus frontend); a kódnak a `hu-HU` formát kell követnie, a fájlnak pedig érvényes JSON-nak kell lennie, minden más fájl figyelmen kívül marad.

- A `LocaleCatalog` (`Behind.Common.WebApi.Infrastructure.Localization`, az `AddFrontend` regisztrálja a beágyazott szolgáltatón és a `wwwroot` könyvtáron felett) név szerint rendezve sorolja fel az elérhető `LocaleInfo(Code, Name)` elemeket, és a `ContainsAsync` hívással ellenőriz.
- Az alapértelmezett nyelvi beállítás a `frontend.json` `Frontend:Site:DefaultLocale` értéke; a `GET /about` `defaultLocale` néven adja vissza (`en-US`, `LocaleCatalog.Fallback`, ha üres).
- A `GET /settings` a `frontend.json` `Frontend:Site:DefaultLocale` mezőjéhez `options` listát ad (`SettingsOption(Value, Label)`, a `SettingsLocaleExtensions.OptionsFor` építi); a Beállítások oldal az opciókkal rendelkező mezőt legördülő listaként jeleníti meg, a `PUT /settings/frontend.json` pedig `400` választ ad a fájl nélküli nyelvi beállításra (`HasKnownLocale`).
- A `models/i18n.js` az `en-US` nyelvet tartalék gyanánt, majd a kért nyelvi beállítást tölti be, mielőtt az `app.js` kötné a keretet, és elérhetővé teszi az `i18n.t(key, args)` függvényt, valamint a Knockout kötéseket: `t: 'key'` (vagy `t: { key: 'key', args: [...] }`) a szöveghez és `tAttr: { attribute: 'key' }` az attribútumokhoz. A nyelvi fájlokból hiányzó kulcs esetén a HTML-be írt angol szöveg marad. Az `en-US` minden kulcsának léteznie kell a többi nyelvi fájlban is (tesztelt).
- A frontend kódra vonatkozó szabályok: nincs keménykódolt, a felhasználó számára látható szöveg. A nézetek a `t` / `tAttr` kötéseket használják (az üzenetekben nincs HTML, a vegyes jelölést szétbontjuk vagy `{0}` argumentumként adjuk át, az angol szöveg tartalékként a HTML-ben marad), a szkriptek az `i18n.t("key", [args])` függvényt használatkor hívják (soha nem modulbetöltéskor), és minden kulcsot egyszerű karakterlánc-literálként írnak. Az üzenettáblák kulcsokat tartalmaznak; a `models/results.js` fordítja le őket (a `results.sessionExpired` egy kulcs). A dátumok és számok az `i18n.dateTime`, az `i18n.date` és az `i18n.locale()` elemeket használják. A beépített menüpontnevek a `models/menus.js` fájlban `menu.item.*` kulcsokra képeződnek le.
- A `LocalizationCompletenessTests` érvényesíti mindezt: minden `.js` vagy `.html` fájlban használt kulcs létezik az `en-US` fájlban, minden `en-US` kulcs használatban van, a `hu-HU` ugyanazokkal a kulcsokkal és helyőrzőkkel rendelkezik, a nézetek pedig nem tartalmaznak szöveget `t`/`text`/`html`/`value` kötésen kívül (a `<code>`, `<option>`, `<textarea>` és `<script>` kivétel). Egy új szöveghez ezért a kulcsa mind a `locales/en-US.json`, mind a `locales/hu-HU.json` fájlban szükséges.
- A felhasználó saját nyelvi beállítása a felhasználónkénti `locale` beállítás (lásd alább); a `GET /locales` (névtelen) az elérhető `LocaleInfo(Code, Name)` elemeket sorolja fel. A nyelv a fiók nyelvi beállítása, ennek hiányában a böngészőben megjegyzett (`localStorage` kulcs: `behind.locale`), ennek hiányában az oldal alapértelmezése; az üres érték azt jelenti: „használja az alapértelmezettet”.

### Felhasználói beállítások

A felhasználónkénti kulcs/érték párok a `UserSettings` táblában találhatók (`UserId`, `Key`, `Value`; összetett kulcs: `UserId` + `Key`, a felhasználóval együtt kaszkádolt törlés), megosztott típusú összekapcsoló táblaként leképezve, mint a `UserGrants` (`UserConfiguration.ApplyUserSettings`, `UserSettingStore`), olvasásuk és írásuk az `IUserSettingQueries` és az `IUserSettingCommands` felületen keresztül történik (az üres érték eltávolítja a párt). Nem auditált entitás, ezért nincs audit címkéje.

- A `GET /profile/settings` a bejelentkezett felhasználó beállításait adja vissza `{ key: value }` alakban; a `PUT /profile/settings/{key}` `{ value }` törzset fogad (`204`; `401` érvényes munkamenet nélkül; `404` olyan kulcsra, amely nincs a `UserSettingKeys.Known` listában; `400`, ha a `UserSettingExtensions.AcceptsAsync` elutasítja az értéket, a `locale` esetén a fájl nélküli nyelvi beállítást). Mindkettő az `IgnoreETag` használja.
- Egy új beállítás egy konstans, a `UserSettingKeys.Known` listában a bejegyzése, és ha az értékét ellenőrizni kell, egy `case` az `AcceptsAsync` metódusban. A frontend a `models/usersettings.js` (`load`, `clear`, `save`, `locale`) segítségével olvassa őket, amelyet az `app.js` újratölt, amikor a munkamenet bejelentkezik, kijelentkezik vagy megszemélyesít.

### Oldalbeállítások

A Beállítások oldal (`#/settings`, `SettingsEndpoints`) a tartalomgyökérben lévő konfigurációs fájlokat kezeli. A `ConfigurationFileCatalog` (az `AddSecurity` regisztrálja singletonként) felsorolja az egyetlen fájlokat, amelyekhez hozzányúlhat: `appsettings.json`, `backend.json`, `database.json`, `email.json`, `frontend.json` és `security.json`; bármely más név `404` választ ad.

- A `GET /settings` (`Configuration.Read`) a `{ readOnly, files: [{ name, fields: [{ path, type, value, sensitive, editable, action }] }] }` struktúrát adja vissza. Egy fájl minden levele egy mező, amelynek `path` értéke a kettősponttal elválasztott konfigurációs útvonal (a tömbelemek index szerint). Egy érzékeny mező `value` értéke a valós érték `MaskSensitiveValue()` eredménye; egy útvonal érzékeny, ha a `ConnectionStrings` alatt van, vagy az utolsó szegmense tartalmazza a `password`, `secret`, `signingkey`, `apikey`, `token` vagy `credential` szöveget (`SensitiveConfigurationPathExtensions.IsSensitiveConfigurationPath`).
- A `PUT /settings/{file}` (`Configuration.Manage`) `{ values: { path: value } }` törzset fogad. Csak a meglévő, azonos JSON-típusú levelek módosíthatók (`SettingsChangeExtensions.PlanChanges`); egy érzékeny mező üres értéke, valamint a Generate vagy Rotate művelettel rendelkező mező elutasításra kerül, és egyetlen érvénytelen módosítás az egész kérést elutasítja (`400`). Az értékek írása a `JsonConfigurationFileExtensions.SetJsonNodesAsync` segítségével történik (atomi csere, a típusok megmaradnak).
- A `POST /settings/{file}/generate` és a `POST /settings/{file}/rotate` (`Configuration.Manage`) `{ path }` törzset fogad. A műveleteket a `SettingsAction.All` sorolja fel: Generate a `Security:SecretKey` és a `Security:Elevation:DeveloperSecretKey` esetén (új `Totp`, a válasz egyszer tartalmazza az `otpUri` értéket, és a kulcs létrejön, ha hiányzik), Generate a `Security:Jwt:SigningKey` esetén (`JwtSettings.GenerateSigningKey`, soha nem kerül visszaadásra), Rotate a `Security:MagicLink:ActualSecret` esetén (`MagicLinkSecretRotator.RotateNowAsync`, élőben alkalmazva és mentve). Egy új művelet a lista egy bejegyzése. A jogosultságemelési kulcsok és a JWT-kulcs élőben olvasódnak, ezért azonnal érvénybe lépnek.
- Az `email.json` típusos fájl: a `ConfigurationFileCatalog` az `email` szerkesztővel jelöli meg, ezért a `GET /settings` `editor: "email"` értékkel és mezők nélkül sorolja fel, az általános `PUT /settings/{file}` pedig `404` választ ad rá. A `GET /settings/email` (`Configuration.Read`) az opcióérzékeny kötővel beolvassa a fájlt az `EmailConfiguration` rekordba, és `EmailSettingsResponse` választ ad (szolgáltató, a szolgáltatók, a hitelesítési típusok, a beépített sablonok és az SMTP-értékek maszkolt jelszóval). A `PUT /settings/email` (`Configuration.Manage`) `EmailSettingsRequest` törzset fogad; az `EmailSettingsExtensions.Apply` érvényesíti, és létrehozza a `LoggerEmailConfiguration` vagy `SmtpEmailConfiguration` rekordot (az üres jelszó megtartja a tárolt jelszót; az érvénytelen kérés `400`), amelyet a rekordok JSON-konvertereivel (`EmailConfigurationJsonConverter`, `EmailAuthenticationJsonConverter`, `TemplatesJsonConverter`, az `Option<T>` null értékeit a `JsonNodeExtensions.WithoutNulls` távolítja el) szerializál a fájl `Email` szakaszába; a fájl többi szakasza megmarad. Egy új e-mail szolgáltató egy új `EmailConfiguration` rekord, egy `case` a konverterekben és az `EmailSettingsExtensions` osztályban.
- Az `appsettings.json` a `logging` szerkesztővel típusos: a `GET /settings/{file}/logging` (`Configuration.Read`) beolvassa a `Serilog:MinimumLevel` értéket a `MinimumLevelConfiguration` rekordba (`Option<LogEventLevel>` alapértelmezés és az `Override` szótár, a `MinimumLevelJsonConverter` konvertálja), és `LoggingSettingsResponse` választ ad (alapértelmezés, a szintnevek, a felülírások forrás szerint rendezve). A `PUT /settings/{file}/logging` (`Configuration.Manage`) `LoggingSettingsRequest` törzset fogad; a `LoggingSettingsExtensions.ToConfiguration` érvényesíti (ismert szintnevek, nem üres és egyedi források, egyébként `400`), és csak a `Serilog:MinimumLevel` csomópont cserélődik (üres esetén eltávolításra kerül), a fájl többi része megmarad. A Serilog követi a konfigurációváltozásokat, így a módosítás azonnal érvénybe lép. Más fájlok `404` választ adnak.
- Az `OptionConfigurationBinder` az absztrakt típust a megkülönböztetője alapján köti: a védett konstruktorának egyetlen karakterlánc-paraméterét (az `EmailConfiguration` `Provider`, az `Authentication` `Type` értéke) a típus `TypeConverter` osztálya konvertálja a konkrét típussá, amely ezután normál módon kötődik. Az a típus, amelynek `TypeConverter` osztálya az `IReadOnlyDictionary<string, string?>` típusból konvertál (`Templates`), a szakaszának gyermekértékeiből kötődik.
- Amíg a `Security:ReadOnlyConfiguration` értéke true, minden módosítás `403` választ ad a `ConfigurationForbiddenResponse.ConfigurationReadOnly` értékkel.
- A `Configuration.Manage` a beépített `Developers` szerepkör része (`BuiltInAccess`); a meglévő adatbázisok megtartják a szerepkör-engedélyeiket, ezért azt a Szerepkörök oldalon kell megadni.

### Oldalbeállítások

A `Frontend:Sites` szakasz (`SitesConfiguration`, például a `frontend.json` fájlban) az oldalak funkciót konfigurálja:

```json
{ "Frontend": { "Sites": { "IndexSlug": "home", "MaxImageBytes": 5242880 } } }
```

- `IndexSlug`: ha egy ilyen slug-gal rendelkező oldal (bármely slug-hoz hasonlóan normalizálva) élő, a `GET /sites/index` azt szolgálja ki, és a frontend a beépített üdvözlőoldal helyett azt mutatja a `#/` címen. Az üres vagy hiányzó érték azt jelenti, hogy nincs egyéni kezdőlap.
- `MaxImageBytes`: a legnagyobb elfogadott oldalkép-feltöltés (alapértelmezés 5 MB, a nulla vagy negatív érték az alapértelmezésre esik vissza). A nagyobb feltöltések `413` választ adnak `{ "maxBytes": ... }` törzzsel. A Kestrel kérés törzsére vonatkozó korlátja (alapértelmezés szerint 30 MB) továbbra is érvényes ezen felül.

A `GET /sites/settings` (`Site.Read`) mindkét értéket és egy oldal szakaszainak legnagyobb számát (`maxSections`) adja vissza a szerkesztőnek.

### Oldalszakaszok

A `POST /sites` és a `POST /sites/{id}/versions` (`Site.Write`) a `title`, `slug`, `content`, `publish` és `hideTitle` mellett egy opcionális `sections` tömböt fogad: `[{ theme: "Normal" | "Accented", title, content, imageId, imageAlt, imagePosition: "Left" | "Right" }]`. A tömb sorrendje az oldalon megjelenő sorrend, és minden mentés a teljes listát tárolja az új verzióhoz (a kihagyott vagy üres lista azt jelenti, hogy nincsenek szakaszok).

- A `title`, a `content` (az oldaltartalomhoz hasonlóan szanálva) és az `imageAlt` levágásra és korlátozásra kerül (200, 100000 és 300 karakter). Egy szakaszhoz cím, szöveg vagy `imageId` szükséges; az `imageAlt` kép nélkül eldobásra kerül.
- 30-nál több szakasz, ismeretlen téma vagy képpozíció, üres szakasz, üres `imageId` vagy túl hosszú szövegek `400` választ adnak, és ugyanígy az az `imageId` is, amely nem létező oldalkép (`SiteSaveResult.UnknownImage`).
- Az `imageId` a `POST /sites/images` által visszaadott `id`; a szakaszok válaszai `imageUrl` mezőt tartalmaznak (`/sites/images/<id>`, kép nélkül `null`).
- A `GET /sites/slug/{slug}`, a `GET /sites/index` és a `GET /sites/{id}/versions/{version}` a `sections: [{ id, theme, title, content, imageId, imageUrl, imageAlt, imagePosition }]` alakot adja vissza.
- A webes kliens minden szakaszt `<section class="behind-section behind-section-{theme}">` elemként jelenít meg (képpel kiegészítve: `behind-section-with-image behind-section-image-{left|right}`): a kép és a szöveg 768 px felett kétoszlopos rácson osztozik, alatta egymás alá kerül. Az Accented szakaszok a `--behind-section-accent-bg` (alapértelmezés: `--bs-primary`) és a `--behind-section-accent-color` (alapértelmezés: `--bs-white`) változót használják, így az oldal témája átstílusozhatja őket.

### Backend beállítások

A `backend.json` (kötelező, `Backend` szakasz, `BackendConfiguration`) a frontendhez nem tartozó beállításokat tartalmazza:

```json
{ "Backend": { "DataDirectory": "/app/data", "Downloads": { "MaxFileBytes": 104857600 } } }
```

- `DataDirectory`: a szerver adatainak könyvtára. Minden feltöltött fájl (oldal- és galériaképek, letöltések) tartalma a `<DataDirectory>/vfs` alatt tárolódik, az adatbázis csak a metaadatokat őrzi (lásd az adatbázis-architektúra „Virtual file system” részét). Az alkalmazás és a migrátor induláskor hibával leáll, ha hiányzik, és mindkettőnek ugyanazt a könyvtárat kell használnia.

- `Downloads:MaxFileBytes` (`DownloadsConfiguration`, `Backend:Downloads` szakasz): a letöltésfeltöltés legnagyobb elfogadott fájlja (alapértelmezés 100 MB, a nulla, negatív vagy hiányzó érték az alapértelmezésre esik vissza). A nagyobb fájlt fájlonként `too-large` értékkel jelzi. A webes kliens a korlátot a `GET /downloads/folders` végpontról olvassa, és a nagyobb fájlokat feltöltés előtt elutasítja. A feltöltési végpont kérésekre vonatkozó korlátait ebből az értékből minden kérésnél a `UseDownloadUploadLimit` származtatja (a végpontot a `DownloadUploadLimitMetadata` jelöli; a korlát szorozva a `DownloadEndpoints.MaxUploadFiles` értékkel plusz 1 MB a kéréshez, a korlát plusz 1 MB egyetlen fájlhoz), így a módosítás újraindítás nélkül érvénybe lép; a kérési korlátot meghaladó kérés `413` választ kap.

- `Accounts` (`AccountsConfiguration`, `Backend:Accounts` szakasz): a `SignIn` és a `SignUp` (mindkettő alapértelmezése `true`) megjeleníti vagy elrejti a webes kliens fejlécének **Sign in** és **Sign up** gombjait, például `{ "Backend": { "Accounts": { "SignUp": false } } }`. Ez csak felhasználói felületi kapcsoló: a bejelentkezési és regisztrációs végpontok és oldalak tovább működnek. A `GET /accounts/settings` (nyilvános) a `{ "signIn": ..., "signUp": ... }` értéket adja vissza, és a kliens induláskor beolvassa (`models/accounts.js`).
- `Comments` (`CommentsConfiguration`, `Backend:Comments` szakasz): entitásonként be- vagy kikapcsolja a hozzászólás funkciót, kommentelhető entitásonként egy logikai értékkel (`News`, alapértelmezés `true`), például `{ "Backend": { "Comments": { "News": false } } }`. Amíg egy entitás hozzászólásai ki vannak kapcsolva, annak minden hozzászólás-végpontja (`GET`/`POST /news/{id}/comments`, `POST /news/comments/{id}` és `.../delete`) `404` választ ad (`CommentsEnabledExtensions.RequireCommentsOf`, az engedélyezés után ellenőrizve), a meglévő hozzászólások megmaradnak, és a beállítás újbóli bekapcsolásakor visszatérnek. A `GET /comments/settings` (nyilvános) a `{ "news": true|false }` értéket adja vissza; a webes kliens beolvassa, és elrejti a hírlista és a hírkezelő hozzászólásszámát, valamint a hírek oldalának hozzászólás-szakaszát. Egy új kommentelhető entitás hozzáadja a saját tulajdonságát és `IsEnabled` ágát, és a hozzászólás-útvonalain a `RequireCommentsOf` metódust használja.

### Carousel végpontok

Egy oldalnak legfeljebb egy carouselje van, és használata oldalanként opcionális: a carousel nélküli oldal a korábbiak szerint jelenik meg (lásd az adatbázis-architektúra „Site carousels” részét). A korlátok konfigurációk, nem konstansok, lásd: „Carousel beállítások”.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /carousels/settings` | nyilvános, a ténylegesen érvényes korlátok `{ maxSlides, minIntervalSeconds, maxIntervalSeconds, defaultIntervalSeconds, maxCaptionLength, maxLinkUrlLength, maxImageBytes }` |
| `GET /carousels/sites/{siteId}` | nyilvános, `404`, hacsak az oldal nem élő és nincs carouselje |
| `GET /carousels/images/{id}` | nyilvános, csak a `Carousels` VFS-típusú képek |
| `POST /carousels/images` | `Site.Write` (multipart `file`, `413` a `MaxImageBytes` felett, `415` a PNG, JPEG, GIF, WebP formátumoktól eltérőre) |
| `GET /sites/{siteId}/carousel` | `Site.Read` (a `?deleted=true` a `RawDatabase.Manage` jogosultságot igényli) |
| `POST /sites/{siteId}/carousel` | `Site.Write`, törzs: `{ intervalSeconds, slides: [{ imageId, caption, linkUrl }] }` (1 és `MaxSlides` közötti számú dia, a beállított tartományba eső időköz, legfeljebb `MaxCaptionLength` hosszú felirat, üres, `#/...`, `/...` vagy `http`, `https` vagy `mailto` URL-es hivatkozás), `{ siteId, version }` választ ad, ismeretlen oldalra `404`, érvénytelen piszkozatra vagy ismeretlen képre `400`, verzióütközésre `409` |
| `POST /sites/{siteId}/carousel/delete` | `Site.Delete` (soft delete) |
| `POST /sites/{siteId}/carousel/restore` | `RawDatabase.Manage` |
| `POST /sites/{siteId}/carousel/hard/delete` | `RawDatabase.Manage` |

A nyilvános oldalválaszok (`GET /sites/slug/{slug}`, `GET /sites/index`) tartalmazzák az oldal `id` értékét, a webes kliens ennek segítségével olvassa be a carouselt. A nem biztonságos tárolt hivatkozásokat üres hivatkozásként adja vissza.

### Carousel beállítások

A `Frontend:Carousels` szakasz (`CarouselsConfiguration`, a `frontend.json` fájlban) az oldalak carouseljeinek korlátait tartalmazza. A konfigurációs fájlok minden mezőjéhez hasonlóan a Beállítások oldalon szerkeszthető, és az alkalmazás futása közben érvénybe lép (a végpontok az `IOptionsSnapshot<CarouselsConfiguration>` értéket olvassák), az alapértékeket pedig a behind-core eszköz indítási create parancsa írja:

```json
{ "Frontend": { "Carousels": { "MaxSlides": 20, "MinIntervalSeconds": 2, "MaxIntervalSeconds": 60, "IntervalSeconds": 5, "MaxCaptionLength": 300, "MaxImageBytes": 5242880 } } }
```

- `MaxSlides`: egy carousel legtöbb képe (alapértelmezés 20, a nulla vagy negatív érték az alapértelmezésre esik vissza).
- `MinIntervalSeconds` és `MaxIntervalSeconds`: a kép megjelenítési idejének megengedett tartománya másodpercben (alapértelmezés 2 és 60 között). Az 1 alatti minimum vagy a minimum alatti maximum az alapértelmezett tartományra esik vissza.
- `IntervalSeconds`: az időköz, amellyel egy új carousel indul (alapértelmezés 5), a tartományon belül tartva.
- `MaxCaptionLength`: a leghosszabb felirat (alapértelmezés 300, soha nem több, mint a `CarouselsConfiguration.CaptionCeiling`, 1000, az oszlop mérete).
- `MaxImageBytes`: a legnagyobb elfogadott carouselkép-feltöltés (alapértelmezés 5 MB, a nulla vagy negatív érték az alapértelmezésre esik vissza); a nagyobb feltöltések `413` választ adnak `{ "maxBytes": ... }` törzzsel.

A meglévő carouselek értékei megmaradnak, ha egy korlát megváltozik, csak az új mentéseket ellenőrzi a jelenlegi korlátokkal. A `GET /carousels/settings` a ténylegesen érvényes értékeket adja vissza, és a webes kliens ezeket olvassa be, mielőtt megjeleníti az oldalszerkesztő carousel szakaszát.

### Galéria beállítások

A `Frontend:Galleries` szakasz (`GalleriesConfiguration`) a `MaxImageBytes` értéket tartalmazza, amely a legnagyobb elfogadott galériakép- vagy bélyegkép-feltöltés (alapértelmezés 10 MB, a nulla vagy negatív érték az alapértelmezésre esik vissza). A nagyobb fájlokat a kötegelt feltöltés fájlonként `too-large` értékkel jelzi, a bélyegkép-feltöltésnél pedig `413` választ adnak. A galéria feltöltési végpontjai legfeljebb a `GalleryImageEndpoints.MaxRequestBytes` (128 MB) méretű kérés törzseket fogadják el, ezért a webes kliens kérésenként öt fájlt küld.

### Hírek végpontjai

| Végpont | Hozzáférés |
|----------|--------|
| `GET /news?page&pageSize&category&letter` | nyilvános, a jelenleg futó hír élő verziója, és az aktív kategóriák; a `letter` azokat a híreket tartja meg, amelyek címe vele kezdődik (`A`-`Z`, `#`, egyébként `400`), a válaszban a `letters` a kiválasztott kategória híreinek kezdőbetűi |
| `GET /news/{id}` | nyilvános, az élő verzió, csak amíg a hír fut |
| `GET /news/categories` | nyilvános, aktív kategóriák |
| `GET /news/{id}/comments?page&pageSize` | nyilvános, a `canDelete` a szerzőre és a `News.Delete` jogosultsággal rendelkezőkre, a `canEdit` a szerzőre és a `News.Manage` jogosultsággal rendelkezőkre van beállítva, az `updatedAt` az audit naplóból származik |
| `POST /news/{id}/comments` | bármely bejelentkezett felhasználó |
| `POST /news/comments/{id}` | szerkesztés: a hozzászólás szerzője vagy a `News.Manage`, törzs: `{ "content": ... }`, új hozzászólás-verziót szúr be, `204` |
| `POST /news/comments/{id}/delete` | a hozzászólás szerzője vagy a `News.Delete` |
| `GET /news/manage`, `GET /news/categories/manage` | `News.Read`, a `search`, `letter`, `deleted` paraméterekkel (a `deleted` a `RawDatabase.Manage` jogosultságot igényli) |
| `GET /news/{id}/versions`, `GET /news/{id}/versions/{version}` | `News.Read`, a verziólista és egy teljes verzió |
| `POST /news`, `POST /news/{id}/versions` | `News.Write`, új verzió létrehozása / mentése, a `publish: true` élővé is teszi |
| `POST /news/{id}/versions/{version}/activate` | `News.Write`, egy verzió élővé tétele |
| `POST /news/categories`, `POST /news/categories/{id}`, `POST /news/images` | `News.Write`, egy kategória mentése új verziót szúr be; egy képfeltöltés `{ id, url }` választ ad a `/news/images/{id}` URL-lel |
| `GET /news/images/{id}` | nyilvános, egy hír vagy hírkategória képe `immutable` gyorsítótárazással és `nosniff` értékkel |
| `POST /news/{id}/delete`, `POST /news/categories/{id}/delete` | `News.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

Az URL-ekben szereplő azonosítók az entitás kulcsai, a válaszok a mentett vagy megjelenített `version` értéket jelzik. A kezelt listák a `WithManagedList` (`ManagedListExtensions`) elemet osztják meg, amely a `Sites` és a `News` esetén érvényesíti a betűszűrőt és a törölt kapcsolót. A mentés `409` választ ad `{ "reason": ... }` törzzsel (`unknown-category`, `invalid-schedule`, `version-conflict`, `name-taken`, `unknown-image`).

### Cikkek végpontjai

A cikkek a híreknek a `/articles` alatti végpontjait követik (`ArticleRoutesExtensions.MapArticleEndpoints`), hozzászólások, ütemezés és kiemelés nélkül, saját kategóriákkal.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /articles?page&pageSize&category&search` | nyilvános, a közzétett cikkek élő verziója és az aktív cikkkategóriák; a `search` kis- és nagybetűk megkülönböztetése nélkül egyezik az élő verzió címével vagy tartalmával |
| `GET /articles/{id}` | nyilvános, az élő verzió |
| `GET /articles/categories` | nyilvános, aktív cikkkategóriák |
| `GET /articles/manage`, `GET /articles/categories/manage` | `Article.Read`, a `search` (a cikkeknél cím vagy tartalom), `letter`, `deleted` paraméterekkel (a `deleted` a `RawDatabase.Manage` jogosultságot igényli); a cikk állapota `draft` vagy `published` |
| `GET /articles/{id}/versions`, `GET /articles/{id}/versions/{version}` | `Article.Read`, a verziólista és egy teljes verzió |
| `POST /articles`, `POST /articles/{id}/versions` | `Article.Write`, új verzió létrehozása / mentése, a `publish: true` élővé is teszi |
| `POST /articles/{id}/versions/{version}/activate` | `Article.Write`, egy verzió élővé tétele |
| `POST /articles/categories`, `POST /articles/categories/{id}`, `POST /articles/images` | `Article.Write`, egy kategória mentése új verziót szúr be; egy képfeltöltés `{ id, url }` választ ad a `/articles/images/{id}` URL-lel |
| `GET /articles/images/{id}` | nyilvános, egy cikk vagy cikkkategória képe `immutable` gyorsítótárazással és `nosniff` értékkel |
| `POST /articles/{id}/delete`, `POST /articles/categories/{id}/delete` | `Article.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

A válaszok újrahasznosítják a `NewsCategoryItem`, `NewsSavedResponse` és `NewsConflictResponse` típusokat (okok: `unknown-category`, `version-conflict`, `name-taken`, `unknown-image`).

### Galériák végpontjai

A galériák a cikkekhez hasonlóan verziózottak, és a `/galleries` alatt találhatók (`GalleryRoutesExtensions.MapGalleryEndpoints`). Egy galériaverzió birtokolja az elemeit: egy verzió mentése beszúrja a galériasort és képenként egy `GalleryItem` sort ugyanazzal a verziószámmal, így a régebbi verziók megtartják saját képeiket, sorrendjüket és leírásaikat.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /galleries?page&pageSize&search` | nyilvános, a közzétett galériák élő verziója; a `search` a címmel vagy a leírással egyezik |
| `GET /galleries/{id}` | nyilvános, az élő verzió az elemeivel (`imageUrl`, `thumbnailUrl`, `description`) |
| `GET /galleries/manage` | `Gallery.Read`, a `search`, `letter`, `deleted` paraméterekkel (a `deleted` a `RawDatabase.Manage` jogosultságot igényli); az állapot `draft` vagy `published` |
| `GET /galleries/{id}/versions`, `GET /galleries/{id}/versions/{version}` | `Gallery.Read`, a verziólista és egy teljes verzió |
| `POST /galleries`, `POST /galleries/{id}/versions` | `Gallery.Write`, új verzió létrehozása / mentése; a `publish: true` élővé is teszi; az elemek leírásai opcionálisak |
| `POST /galleries/{id}/versions/{version}/activate` | `Gallery.Write`, egy verzió élővé tétele |
| `GET /galleries/images/{id}` | nyilvános, a `Galleries` VFS-típusú fájl `immutable` gyorsítótárazással és `nosniff` értékkel |
| `POST /galleries/images` | `Gallery.Write`, multipart `files` (legfeljebb `GalleryImageEndpoints.MaxUploadFiles`, kérési korlát: `MaxRequestBytes`), minden eredetit és a szerver által generált WebP bélyegképét (`ThumbnailSide`) VFS-fájlként tárolja, és fájlonként egy bejegyzést ad vissza, a kihagyott fájlokhoz `error` értékkel (`unsupported`, `unreadable`, `too-large`) |
| `POST /galleries/thumbnail` | `Gallery.Write`, egy `file`, a `CoverSide` méretre skálázva VFS-fájlként tárolva, `{ id, url }` választ ad, a korlát felett `413` választ `{ maxBytes }` törzzsel, más fájlokra `415` |
| `POST /galleries/{id}/delete` | `Gallery.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

A galériaképek nem használják az oldalképek végpontjait vagy a `Frontend:Sites` korlátot; a `Galleries` VFS-típus fájljai, az oldalképek pedig a `Sites`, `News`, `Articles`, `NewsCategories` vagy `ArticleCategories` típusok fájljai. A piszkozat `thumbnailImageId: null` értéket küld a generált galéria-bélyegképhez: a `GalleryThumbnails` ekkor az első négy elem bélyegképéből kollázst állít össze (`ImageThumbnailExtensions.ToMosaic`), és VFS-fájlként tárolja; a válasz a `thumbnailGenerated` mezővel jelzi, hogy melyik fajta. A válaszok újrahasznosítják a `NewsSavedResponse` és `NewsConflictResponse` típusokat (okok: `unknown-image`, `version-conflict`).

### Letöltések végpontjai

A letöltések mappák (`DownloadFolder`, egy `Slug` és egy `DisplayName`, tetszőleges mélységben) és a bennük lévő fájlok (`Download`, egy `DisplayName`) a `/downloads` alatt (`DownloadRoutesExtensions.MapDownloadEndpoints`). A böngészés nyilvános. A webes kliens a `#/downloads/<folder path>` címet jeleníti meg, ahol az útvonal a mappák slug-jai felülről lefelé, például `#/downloads/manuals/drivers`, a kliensen a mappafából feloldva (a puszta `#/downloads` a kezdőpont). A kezelőoldal különálló, `#/manage/downloads/<folder path>` (`downloadmanager` kontroller, nézet és viewmodel); mindkét oldal osztozik a fa és a fájlok betöltésén a `models/downloadbrowser.js` fájlban és a `download-tree` komponensben.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /downloads/folders` | nyilvános, minden elérhető mappa (nem törölt és törölt szülő nélküli) lapos listaként (`id`, `parentId`, `slug`, `displayName`) a `maxFileBytes` értékkel együtt |
| `GET /downloads/folders/{id}/files` | nyilvános, egy mappa fájljai (`id`, `displayName`, `fileName`, `contentType`, `length`, `uploadedBy`, `uploadedAt` az audit naplóból, `url`), ismeretlen, törölt vagy rejtett mappára `404` |
| `GET /downloads/files/{id}` | nyilvános, a letöltés tartalma mellékletként az eredeti fájlnevével, `nosniff` értékkel és újraellenőrzéssel; az `{id}` a letöltés azonosítója, nem a VFS-fájlé |
| `GET /downloads/deleted` | `RawDatabase.Manage`, a törölt mappák és fájlok |
| `POST /downloads/folders`, `POST /downloads/folders/{id}/folders` | `Download.Write`, felső szintű mappa / almappa létrehozása a `displayName` és egy opcionális `slug` alapján (alapértelmezés: a megjelenített név slug-ja); `409` `slug-taken` értékkel, ha egy testvér használja a slug-ot |
| `POST /downloads/folders/{id}` | `Download.Write`, a mappa következő verziójának mentése (megjelenített név és slug) |
| `POST /downloads/folders/{id}/files` | `Download.Write`, multipart `files` (legfeljebb `DownloadEndpoints.MaxUploadFiles`) és egy ismétlődő `displayNames` mező ugyanabban a sorrendben (üres vagy hiányzó: a fájlnév); minden fájlról `{ fileName, id, error }` jelentés készül, az `empty`, `too-large` vagy `version-conflict` hibával |
| `POST /downloads/files/{id}` | `Download.Write`, egy letöltés átnevezése (`displayName`) |
| `POST /downloads/folders/{id}/delete`, `POST /downloads/files/{id}/delete` | `Download.Delete`, soft delete; a törölt mappa elrejti az almappáit és fájljait |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage`; a visszaállítás `409` választ ad `parent-deleted`, `folder-deleted` vagy `slug-taken` értékkel; egy mappa végleges törlése eltávolítja az almappáit, fájljait és azok tartalmát |

A fájlok a `Downloads` VFS-típus fájljai, `vfs/Downloads/<download uuid>/<file uuid>` formában tárolva; kizárólag a letöltési végpont szolgálja ki őket (soha nem más típusú tartalom URL-jén keresztül), és soha nem inline módon, így egy feltöltött HTML-fájl nem futhat az alkalmazás origójában.

### Menük végpontjai

A menük a `/menus` alatt találhatók (`MenuRoutesExtensions.MapMenuEndpoints`). Az oldal ezekből építi a navigációját: a fejléc a `main` menüt mutatja (a `Link` elemek fülekként, a `User` elemek a felhasználói menüben), a `menu-nav` frontend komponens pedig bármely más menüt név szerint, például a `footer` menüt.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /menus?name=main&name=footer` | nyilvános, egy kérésben legfeljebb 10 menü név szerint (név nélkül vagy túl soknál `400`); az ismeretlen nevek kimaradnak. Az elemek a bejelentkezett felhasználóra szűrtek: a szerepkör nélküli elem mindenkinek megjelenik, a szerepkörökkel rendelkező elem csak azoknak a felhasználóknak, akik birtokolják valamelyiket. Az almenüpontok a szülőjük alatt beágyazva szerepelnek, a `kind` értéke `Link` vagy `User`, a `target` értéke `CurrentPage`, `NewTab` vagy `NewWindow`. Nem gyorsítótárazott |
| `GET /menus/manage`, `GET /menus/{id}/items/manage` | `Menu.Read`, mindkettő lapozott (`page`, `pageSize`) a `search`, `letter` (a cím első betűje) és `deleted` paraméterekkel (a `deleted` a `RawDatabase.Manage` jogosultságot igényli), valamint az elérhető `letters` értékekkel; az elemek válasza felsorolja a lehetséges `parents` elemeket (aktív felső szintű hivatkozások), a választható szerepköröket, és hogy a `User` elemek engedélyezettek-e |
| `POST /menus`, `POST /menus/{id}` | `Menu.Write`, a mentés új verziót szúr be; a név slug-gá alakul |
| `POST /menus/{id}/items`, `POST /menus/items/{itemId}` | `Menu.Write`, a mentés új verziót szúr be a saját szerepköreivel; üres címre vagy nem biztonságos URL-re `400` (csak relatív URL-ek és `http`, `https`, `mailto`, `tel`) |
| `POST /menus/{id}/delete`, `POST /menus/items/{itemId}/delete` | `Menu.Delete` |
| `POST .../restore`, `POST .../hard/delete` | `RawDatabase.Manage` |

A mentés `409` választ ad `{ "reason": ... }` törzzsel: `name-taken`, `built-in` (a főmenü átnevezése vagy törlése), `unknown-menu`, `invalid-parent`, `kind-not-allowed`, `unknown-role`, `version-conflict`.

### Útvonal-alias végpontok

Az útvonal-aliasok a `/route-aliases` alatt találhatók (`RouteAliasRoutesExtensions.MapRouteAliasEndpoints`). A frontend útválasztó alias után érdeklődik, mielőtt ismeretlen `#/<slug>` esetén oldalt jelenítene meg; ha az alias létezik, az útválasztó a cél útvonalát futtatja a cím megváltoztatása nélkül.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /route-aliases/resolve/{alias}` | nyilvános, aktív aliasra `{ "alias", "target" }` választ ad (az alias levágva és kisbetűsítve) vagy `404`-et. Nem gyorsítótárazott |
| `GET /route-aliases/manage` | `RouteAlias.Read`, lapozott (`page`, `pageSize`) a `search` (alias vagy cél), `letter` (az alias első betűje) és `deleted` paraméterekkel (a `deleted` a `RawDatabase.Manage` jogosultságot igényli), valamint az elérhető `letters` értékekkel |
| `POST /route-aliases`, `POST /route-aliases/{id}` | `RouteAlias.Write`, a mentés új verziót szúr be; az alias slug-gá alakul, üres aliasra vagy olyan célra, amely nem `#/` karakterekkel kezdődik, 2000 karakternél hosszabb, vagy szóközt, vezérlőkaraktert vagy fordított perjelet tartalmaz, `400` |
| `POST /route-aliases/{id}/delete` | `RouteAlias.Delete` |
| `POST /route-aliases/{id}/restore`, `POST /route-aliases/{id}/hard/delete` | `RawDatabase.Manage` |

A mentés `409` választ ad `{ "reason": ... }` törzzsel: `alias-taken`, `version-conflict`.

### Hozzáférés végpontok

A hozzáférési beállítások az `/access` alatt találhatók (`AccessRoutesExtensions.MapAccessEndpoints`). Az `AccessGateMiddleware` (az `UseAuthorization` után) érvényesíti a korlátozást: amíg be van kapcsolva, az olyan látogató kérése, aki sem a szükséges szerepkörrel, sem az `Access.Manage` jogosultsággal nem rendelkezik, `403` és `{ "code": "restricted" }` választ kap, kivéve azokat az útvonalakat, amelyek mindig nyitva maradnak (`AccessGateExtensions`): `/sign`, `/session`, `/accounts`, `/captcha`, `/access/images`, `/locales`, `/about`, `/profile` és `/access/restriction`.

| Végpont | Hozzáférés |
|----------|--------|
| `GET /access/restriction` | nyilvános, a hívóra `{ "restricted", "title", "message" }` választ ad (a cím és az üzenet csak akkor, ha a hívó korlátozott; az üzenet szanált HTML, a `POST /access` lefuttatja rajta a `SanitizeRichText` metódust). Nem gyorsítótárazott |
| `GET /access/manage` | `Access.Read`, az aktuális `settings` értéket (ha semmi nincs mentve, az alapértékeket) és a szerepkörneveket adja vissza; a `?deleted=true` (`RawDatabase.Manage`) a törölt beállításokat adja vissza vagy `404`-et |
| `POST /access` | `Access.Write`, a mentés a singleton új verzióját szúrja be; `409` `unknown-role` vagy `version-conflict` értékkel |
| `GET /access/images/{id}` | nyilvános (a korlátozási oldal megjeleníti a képeket), csak az `AccessSettings` VFS-fajtájú fájlokat szolgálja ki immutable gyorsítótár-fejléccel |
| `POST /access/images` | `Access.Write`, képet tölt fel (korlát: `Frontend:Sites:MaxImageBytes`), és `{ "id", "url" }` választ ad a `/access/images/<id>` címmel |
| `POST /access/delete` | `Access.Delete` |
| `POST /access/restore`, `POST /access/hard/delete` | `RawDatabase.Manage` |
| `GET /access/pending-users` | `Access.Read`, lapozott (`page`, `pageSize`) a `search` (név vagy e-mail) paraméterrel |
| `POST /access/pending-users/{id}/approve` | `Access.Write`, a `UserManagementGate` (`CanModerate`) átjárón fut keresztül, `404`, ha a felhasználó nem vár jóváhagyásra |
| `POST /access/pending-users/{id}/reject` | `Access.Write`, a `UserManagementGate` (`CanDelete`, tehát a `User.Delete` is szükséges) átjárón fut keresztül, és soft delete-tel törli a felhasználót |

A jóváhagyásra váró felhasználó minden bejelentkezési útvonalon (`SignInEndpoints.StartSession`) `403` és `{ "code": "approvalPending" }` választ kap, a `POST /sign/up/password` pedig ugyanazzal a törzzsel `202` választ ad, amíg a moderálás be van kapcsolva.

### Nyers adatbázis végpontok

A nyers adatbázis szerkesztő a `/raw-database` alatt található (`RawDatabaseRoutesExtensions.MapRawDatabaseEndpoints`). Minden útvonalhoz `RawDatabase.Manage` szükséges. A táblák az EF Core relációs modelljéből származnak (`DbContext.RawTables`, `Behind.Common.Database.Raw`), így minden entitás, összekapcsoló tábla és elsődleges kulccsal rendelkező verziózó tábla lefedett entitásonkénti kód nélkül.

| Végpont | Leírás |
|----------|-------------|
| `GET /raw-database/tables` | minden tábla a `name`, `entityName` (az audit entitásneve), `isReadOnly`, `rows` és a `columns` (`name`, `storeType`, `kind`: `text`, `integer`, `number`, `boolean`, `binary`, `guid`, `datetime`, `isNullable`, `isKey`, `isSensitive`) értékekkel |
| `GET /raw-database/tables/{table}/rows?page&pageSize&search&orderBy&descending` | sorok egy lapja oszlopnév–érték objektumokként (ismeretlen táblára `404`), a bináris értékek Base64 kódoltak, a `[Sensible]` oszlopok értéke `***`, a `search` minden kereshető oszlopot vizsgál |
| `POST /raw-database/tables/{table}/rows` | törzs: `{ "values": { column: value } }`, beszúr egy sort, és visszaadja; a hiányzó Guid kulcs generálódik |
| `POST /raw-database/tables/{table}/rows/update` | törzs: `{ "key": { column: value }, "values": { column: value } }`, csak a megadott nem kulcs oszlopokat írja; az érzékeny oszlop `***` értékét figyelmen kívül hagyja |
| `POST /raw-database/tables/{table}/rows/delete` | törzs: `{ "key": { column: value } }`, véglegesen törli a sort, `204` |

Válaszok: `400` `{ "errors": [...] }` törzzsel ismeretlen oszlopokra, a `values` között szereplő kulcsoszlopokra, hiányos kulcsra vagy az oszlopukba nem illő értékekre; `404` ismeretlen táblára vagy sorra; `409` `{ "reason": "read-only" }` értékkel a csak olvasható táblára (`AuditLogEntries`), vagy `{ "reason": "rejected", "message": ... }` értékkel, ha az adatbázis elutasítja a módosítást. Minden írás egyetlen tranzakcióban fut az audit bejegyzésével (`Created`, `Updated` a különbségekkel, `HardDeleted`, audit entitásnév és kulcs az EF audithoz hasonlóan).

### Indítási példa

```csharp
var builder = WebApplication.CreateBuilder(args);

builder
    .AddOpenApi()
    .AddOpenTelemetry()
    .Services
        .AddDatabaseServices(builder.Configuration)
        .AddAuthenticationServices(builder.Configuration)
        .AddApiServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app
    .UseHttpsRedirection()
    .UseAuthentication()
    .UseAuthorization()
    .MapControllers()
    .Run();
```

## Modulok a hostban

Egy modul webes API projektje `Add<Module>()`/`Use<Module>()` metódusokat tesz elérhetővé, mint az `AddCore()`/`UseCore()`:

- Az `Add<Module>()` meghívja a `services.AddModule(<Module>Module.Descriptor)` metódust, regisztrálja a modul parancsait/lekérdezéseit és a `services.AddModuleContext<I<Module>Context>()`, az `IAuditLabelSource` és az `IModuleSeed` elemeket.
- A végpontokat a modul engedélyeinek szabályzatai védik (`RequireAuthorization(GrantName.Of("<Area>", GrantAction.Read))`); a szabályzatok azért léteznek, mert az `AddGrantAuthorization()` az `AccessCatalog` alapján építi fel őket.
- A modul beágyazott frontendje a `Frontend/modules/<key>/` könyvtárban él (controllers, viewmodels, views, models, components, `css`, `locales/<code>.json`), és `modules/<key>/controllers/<name>` formájú RequireJS azonosítókkal töltődik be; a Core fájlok mindig elsőbbséget élveznek, a modul fájljai soha nem váltják le őket. A modul nyelvi fájljainak formátuma megegyezik a Core fájlokéval (`{"name": ..., "messages": {...}}`), üzeneteik a kiválasztott nyelvhez a Core üzenetek alatt egyesülnek, és minden üzenetkulcsnak a modul kulcsával kell kezdődnie.
- A `GET /modules` felsorolja a frontend számára az útvonalakat, az adminisztrációs oldalakat és a stílusokat; a frontend a modul útvonalait az útvonal-alias tartalék előtt regisztrálja, és az adminisztrációs oldalakat hozzáadja az Adminisztráció oldalhoz az engedélyeik birtokosai számára.

### Példa: a Quotes végpontok

A `Behind.Quotes.WebApi` a végpontjait az `UseQuotes()` hívással képezi le; a lista segédek (`ManagedListExtensions`, `HoldsGrantAsync`) a `Behind.Common.WebApi` projektből megosztottak, így egy modulnak nincs szüksége Core hivatkozásra:

| Végpont | Hozzáférés |
|---|---|
| `GET /quotes` | nyitott; az aktív idézetek, lapozva |
| `GET /quotes/manage` | `Quotes.Read` (a `deleted=true` a `RawDatabase.Manage` jogosultságot igényli) |
| `POST /quotes`, `POST /quotes/{id}` | `Quotes.Write`; `{ id, version }` választ ad, verzióütközés esetén `409` `{ reason: "version-conflict" }` |
| `POST /quotes/{id}/delete` | `Quotes.Delete`; soft delete |
| `POST /quotes/{id}/restore`, `POST /quotes/{id}/hard/delete` | `RawDatabase.Manage` |

### Példa: a Sport végpontok

A `Behind.Sport.WebApi` erőforrásonként egy általános végpontkészletet (`ResourceEndpoints`) képez le: `disciplines`, `positions`, `stat-types`, `seasons`, `teams`, `players`, `player-seasons` és `stats`.

| Végpont | Hozzáférés |
|---|---|
| `GET /sport/<erőforrás>` | nyitott; az aktív sorok lapozva, szűrhetők a `discipline`, `season`, `team`, `player`, `position`, `statType`, `scope`, `search` és `letter` paraméterekkel |
| `GET /sport/<erőforrás>/manage` | `Sport.Read` (a `deleted=true` a `RawDatabase.Manage` engedélyt igényli) |
| `POST /sport/<erőforrás>`, `POST /sport/<erőforrás>/{id}` | `Sport.Write`; `{ id, version }` a válasz, `409` `{ reason }` a `version-conflict`, `unknown-reference`, `mismatch` (másik sportág), `duplicate`, `unknown-image` vagy `invalid-range` értékekkel |
| `POST /sport/<erőforrás>/{id}/delete` | `Sport.Delete`; logikai törlés |
| `POST /sport/<erőforrás>/{id}/restore`, `POST /sport/<erőforrás>/{id}/hard/delete` | `RawDatabase.Manage`; a végleges törlés `409`-et ad, amíg a sorra hivatkoznak |
| `GET /sport/disciplines/{id}`, `/sport/teams/{id}`, `/sport/players/{id}`, `/sport/seasons/{id}` | nyitott; az entitás a kapcsolódó entitásaival, szezononként |
| `GET /sport/leaderboards/{seasonId}[/{statTypeId}]` | nyitott; egy szezon ranglistái (`limit`, alapértelmezés 10, legfeljebb 100) vagy egy statisztikatípus teljes rangsora |
| `POST /sport/teams/images`, `POST /sport/players/images` | `Sport.Write`; multipart `file`, `{ id, url }` a válasz, `413`, `415` |
| `GET /sport/teams/images/{id}`, `GET /sport/players/images/{id}` | nyitott; megváltoztathatatlan, gyorsítótárazott képek, minden fajta a saját URL-je alatt |

### Példa: a Markdown Viewer végpontok

A `Behind.MarkdownViewer.WebApi` az `UseMarkdownViewer()` hívással térképezi fel a végpontjait. A könyvtárak a `markdownviewer.json` fájlból jönnek (`IOptionsMonitor<MarkdownViewerConfiguration>`, a `MarkdownWorkspaceProvider` oldja fel):

| Végpont | Hozzáférés |
|---|---|
| `GET /markdownviewer/documents?locale=` | nyilvános; `{ configured, title, defaultDocument, locales, locale, home, entries: [{ path, title }], sections: [{ title, path, entries }] }` (a `sections` a bejegyzéseket a legfelső szintű mappák szerint csoportosítja, a kezdődokumentum nélkül); a `locale` a kiválasztott nyelvi mappa: a kért, különben az alapértelmezett (`Frontend:Site:DefaultLocale`), különben az első; a `locales` nyelvi mappák nélkül üres |
| `GET /markdownviewer/documents/{**path}?locale=` | nyilvános; egy dokumentum megtisztított HTML-je `{ path, title, html, locale }` (ha a kért nyelven hiányzik, az alapértelmezett, majd az első nyelvre vált), `404` mindenre, ami nem felsorolt Markdown fájl |
| `GET /markdownviewer/files/{**path}?locale=` | nyilvános; a forráskönyvtár egy képe (`nosniff`, `no-cache`, a vektoros képek elszigetelő `Content-Security-Policy`-val), `404` minden másra |
| `POST /markdownviewer/generate` | `MarkdownViewer.Write`; `{ outputDirectory, pages, assets, removed }`, `400` `{ errors: [kód] }` (`source-not-configured`, `source-missing`, `output-not-configured`, `output-overlaps-source`), `500` `{ errors: ["generation-failed"] }` |

## Controller felépítése

### Alapminta

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;
    private readonly ILogger<UsersController> logger;

    public UsersController(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UsersController> logger)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
        this.logger = logger;
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserAsync(Guid id)
    {
        var userOption = await unitOfWork.Users.TryFindAsync(id);

        return userOption.Match(
            none: () =>
            {
                logger.LogWarning("User {UserId} not found", id);
                return NotFound(new ApiErrorResponse("USER_NOT_FOUND", "User not found"));
            },
            some: user =>
            {
                var dto = mapper.Map<UserDto>(user);
                return Ok(dto);
            }
        );
    }

    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
    public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
    {
        var validation = ValidateCreateUser(dto);

        return await validation.Match(
            invalid: errors =>
            {
                logger.LogWarning("User creation validation failed: {@Errors}", errors);
                return BadRequest(new ApiValidationResponse(errors));
            },
            valid: async registration =>
            {
                var user = new HumanUser(
                    displayName: registration.DisplayName,
                    emails: new[] { registration.Email },
                    roles: None
                );

                await unitOfWork.Users.AddAsync(user);
                await unitOfWork.CommitAsync();

                var responseDto = mapper.Map<UserDto>(user);
                return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, responseDto);
            }
        );
    }
}
```

## Kérés/válasz modellek

### DTO minta

**Kérés DTO-k** - bemeneti validációs megszorítások:

```csharp
public record CreateUserDto(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [StringLength(100, MinimumLength = 1)]
    string DisplayName,

    [Required]
    [StringLength(128, MinimumLength = 8)]
    string Password
);

public record UpdateUserDto(
    [StringLength(100)]
    string? DisplayName,

    [StringLength(128, MinimumLength = 8)]
    string? NewPassword
);
```

**Válasz DTO-k** - szerializálható vetületek:

```csharp
public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IEnumerable<RoleDto> Roles,
    DateTime CreatedAt
);

public record RoleDto(
    Guid Id,
    string Name,
    IEnumerable<GrantDto> Grants
);

public record GrantDto(
    Guid Id,
    string Name,
    string Description
);
```

### Hibaválasz modellek

```csharp
public record ApiErrorResponse(
    string Code,
    string Message,
    string? Details = null
);

public record ApiValidationResponse(
    IEnumerable<ValidationError> Errors,
    string Message = "Validation failed")
{
    public ApiValidationResponse(IEnumerable<Error> errors)
        : this(errors.Select(e => new ValidationError(e.Code, e.Message))) { }
}

public record ValidationError(
    string Code,
    string Message
);

public record PaginatedResponse<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages)
{
    public PaginatedResponse(PagedResult<T> result)
        : this(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages) { }
}
```

## HTTP konvenciók

### HTTP metódusok

| Metódus | Cél | Visszatérés |
|--------|---------|---------|
| GET | Erőforrás(ok) lekérése | 200 OK vagy 404 Not Found |
| POST | Új erőforrás létrehozása | 201 Created Location fejléccel |
| PUT | A teljes erőforrás cseréje | 200 OK vagy 204 No Content |
| PATCH | Részleges módosítás | 200 OK vagy 204 No Content |
| DELETE | Erőforrás eltávolítása | 204 No Content vagy 200 OK |

### Állapotkódok

| Kód | Felhasználás |
|------|-------|
| **2xx Siker** | |
| 200 OK | A kérés sikeres volt |
| 201 Created | Az erőforrás létrejött (POST) |
| 204 No Content | Siker törzs nélkül |
| **4xx Kliens hiba** | |
| 400 Bad Request | Érvénytelen bemenet/validációs hiba |
| 401 Unauthorized | Hitelesítés szükséges |
| 403 Forbidden | Az engedélyezés megtagadva |
| 404 Not Found | Az erőforrás nem található |
| 409 Conflict | Üzleti logikai ütközés |
| **5xx Szerverhiba** | |
| 500 Internal Server Error | Váratlan szerverhiba |

### Példa végpontok

**GET - Lekérés**
```csharp
[HttpGet("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    return userOption.Match(
        () => NotFound(),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

**POST - Létrehozás**
```csharp
[HttpPost]
[AllowAnonymous]
[ProducesResponseType(StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> CreateUserAsync(CreateUserDto dto)
{
    var validation = ValidateAndCreate(dto);
    
    return await validation.Match(
        errors => BadRequest(new ApiValidationResponse(errors)),
        async user =>
        {
            await unitOfWork.Users.AddAsync(user);
            await unitOfWork.CommitAsync();
            
            var dto = mapper.Map<UserDto>(user);
            return CreatedAtAction(nameof(GetUserAsync), new { id = user.Id }, dto);
        }
    );
}
```

**PUT - Teljes csere**
```csharp
[HttpPut("{id:guid}")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public async Task<IActionResult> UpdateUserAsync(Guid id, UpdateUserDto dto)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            var updatedUser = user with
            {
                DisplayName = dto.DisplayName ?? user.DisplayName
            };
            
            unitOfWork.Users.Update(updatedUser);
            await unitOfWork.CommitAsync();
            
            return Ok(mapper.Map<UserDto>(updatedUser));
        }
    );
}
```

**DELETE - Eltávolítás**
```csharp
[HttpDelete("{id:guid}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> DeleteUserAsync(Guid id)
{
    var userOption = await unitOfWork.Users.TryFindAsync(id);
    
    return await userOption.Match(
        async () => NotFound(),
        async user =>
        {
            await unitOfWork.Users.DeleteAsync(id);
            await unitOfWork.CommitAsync();
            return NoContent();
        }
    );
}
```

**GET - Lista szűréssel és lapozással**
```csharp
[HttpGet]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> ListUsersAsync(
    [FromQuery] string? email,
    [FromQuery] string? role,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    // Szűrő összeállítása
    var filter = BuildUserFilter(email, role);
    
    // Lapozott lekérdezés végrehajtása
    var pagedResult = await unitOfWork.Users
        .FindPagedAsync(filter, page, pageSize);
    
    var dtos = pagedResult.Items
        .Select(u => mapper.Map<UserDto>(u));
    
    return Ok(new PaginatedResponse<UserDto>(
        Items: dtos,
        Page: pagedResult.Page,
        PageSize: pagedResult.PageSize,
        TotalCount: pagedResult.TotalCount,
        TotalPages: pagedResult.TotalPages
    ));
}
```

## Hitelesítés és engedélyezés

### JWT konfiguráció

```csharp
extension(IServiceCollection)
{
    public IServiceCollection AddAuthenticationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration
            .GetSection("Authentication:Jwt")
            .Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT settings required");

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey)
                    ),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy =>
                policy.RequireRole("Admin"));
            
            options.AddPolicy("ModerateContent", policy =>
                policy.RequireClaim("permissions", "moderate_content"));
        });

        return services;
    }
}
```

### Végpontvédelem

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // A teljes controllerhez hitelesítés szükséges
public class AdminController : ControllerBase
{
    [HttpPost("users/{id}/ban")]
    [Authorize(Policy = "Admin")]  // Meghatározott engedélyezés szükséges
    public async Task<IActionResult> BanUserAsync(Guid id)
    {
        // Csak az admin tilthat ki felhasználókat
    }
    
    [HttpGet("settings")]
    [AllowAnonymous]  // Felülírja az osztályszintű [Authorize] attribútumot
    public IActionResult GetPublicSettings()
    {
        // Nyilvános végpont
    }
}
```

### Szerepkör- és engedély-ellenőrzések

```csharp
public static class AuthorizationExtensions
{
    extension(User)
    {
        public bool HasPermission(Grant grant) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Grants.Match(
                    () => false,
                    grants => grants.Contains(grant)
                ))
            );
        
        public bool HasRole(string roleName) =>
            Roles.Match(
                () => false,
                roles => roles.Any(r => r.Name == roleName)
            );
    }
}

// A controllerben
if (!currentUser.HasPermission(new Grant("moderate_content")))
{
    return Forbid("Insufficient permissions");
}
```

## Hibakezelés

### Globális kivételkezelő

```csharp
extension(WebApplication)
{
    public WebApplication UseGlobalExceptionHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var logger = context.RequestServices.GetRequiredService<ILogger>();
                
                logger.LogError(exception, "Unhandled exception");
                
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = exception switch
                {
                    ValidationException => StatusCodes.Status400BadRequest,
                    NotFoundException => StatusCodes.Status404NotFound,
                    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                    _ => StatusCodes.Status500InternalServerError
                };
                
                var response = new ApiErrorResponse(
                    Code: GetErrorCode(exception),
                    Message: exception?.Message ?? "Internal server error",
                    Details: app.Environment.IsDevelopment() ? exception?.StackTrace : null
                );
                
                await context.Response.WriteAsJsonAsync(response);
            });
        });
        
        return app;
    }
}
```

### Funkcionális hibakezelés

```csharp
public async Task<IActionResult> ProcessAsync(UserCommandDto dto)
{
    var result = await ValidateCommand(dto)
        .Bind(cmd => ExecuteCommand(cmd))
        .Run();  // Végrehajtás és az esetleges kivételek elkapása
    
    return result.Match(
        exception => HandleException(exception),
        user => Ok(mapper.Map<UserDto>(user))
    );
}

private Exceptional<ExecutedCommand> ExecuteCommand(ValidatedCommand cmd)
{
    try
    {
        // Üzleti logika
        return new ExecutedCommand(/* ... */);
    }
    catch (Exception ex)
    {
        return ex;  // Implicit konverzió Exception -> Exceptional<T>
    }
}
```

## OpenAPI dokumentáció

### Végpontok dokumentálása

```csharp
[HttpPost("register")]
[AllowAnonymous]
[Tags("Authentication")]
[Summary("Register a new user")]
[Description("Creates a new user account with the provided details")]
[ProducesResponseType(StatusCodes.Status201Created, Type = typeof(UserDto))]
[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ApiValidationResponse))]
[ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ApiErrorResponse))]
public async Task<IActionResult> RegisterAsync(
    [FromBody]
    [Description("User registration data")]
    CreateUserDto dto)
{
    // Megvalósítás
}
```

### Biztonsági séma

```csharp
public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components = new OpenApiComponents
        {
            SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
            {
                ["bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the ******"
                }
            }
        };
        
        return Task.CompletedTask;
    }
}
```

### Scalar UI beállítása

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Behind API")
            .WithTheme(ScalarTheme.BluePlanet)
            .WithDarkMode(true)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}
```

## Kérés/válasz folyamat

### Tipikus folyamat

```
1. Megérkezik a HTTP kérés
   ↓
2. Útvonal-illesztés (Controller/Action)
   ↓
3. Modellkötés (DTO deszerializálás)
   ↓
4. Validációs szűrők (DataAnnotations)
   ↓
5. Engedélyezési ellenőrzések
   ↓
6. Az action végrehajtása
   ├─ Üzleti logika (monadikus)
   ├─ Adatelérés
   └─ Hibakezelés (Railway-Oriented)
   ↓
7. Eredmény leképezése (válasz DTO-ra)
   ↓
8. HTTP válasz szerializálása (JSON)
   ↓
9. A válasz elküldése a kliensnek
```

## Middleware konfiguráció

### Szabványos sorrend

```csharp
var app = builder.Build();

// Hibakezelés (az elsőnek kell lennie)
app.UseGlobalExceptionHandler();

// HTTPS/Biztonság
app.UseHttpsRedirection();
app.UseHsts();

// CORS
app.UseCors("AllowFrontend");

// Hitelesítés/Engedélyezés
app.UseAuthentication();
app.UseAuthorization();

// Naplózás
app.UseStructuredLogging();

// API dokumentáció
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Útválasztás
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

## Tartalomegyeztetés

### Csak JSON API-k

```csharp
services.AddControllers(options =>
{
    options.OutputFormatters.RemoveType<XmlDataContractSerializerOutputFormatter>();
});

services.AddMvc(options =>
{
    options.FormatterMappings.SetMediaTypeMappingForFormat(
        "json",
        MediaTypeHeaderValue.Parse("application/json")
    );
});
```

### Típusos JSON-kezelés

```csharp
services.Configure<JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonSerializerOptions.Converters.Add(new OptionJsonConverterFactory());
    options.JsonSerializerOptions.Converters.Add(new ExceptionalJsonConverterFactory());
});
```

## Legjobb gyakorlatok

### ✓ Tegye

- Használjon DTO-kat a kérésekhez/válaszokhoz
- A szolgáltatásokból monadikus típusokat adjon vissza
- Dokumentálja a végpontokat OpenAPI attribútumokkal
- Használjon egységes hibaválasz-formátumot
- Valósítson meg lapozást a listavégpontokhoz
- Használja az ILogger felületet strukturált naplózáshoz
- A kivételeket a határon (controller) kezelje
- Mindenhol használjon CancellationToken értéket

### ✗ Ne tegye

- Ne adjon vissza közvetlenül domain entitásokat
- Ne keverje a validációs hibákat a HTTP hibákkal
- Ne használjon puszta kivételeket vezérlési folyamatként
- Ne hagyja ki az engedélyezési ellenőrzéseket
- Ne tegyen közzé belső kivételrészleteket éles környezetben
- Ne használja a puszta `HttpContext.Request` objektumot segédek nélkül
- Ne keverje a felelősségeket (üzleti logika a controllerben)

## Gyakori minták

### Eredményalapú válasz

```csharp
public async Task<IActionResult> GetAsync(Guid id)
{
    var result = await service.GetAsync(id);
    
    return result.Match<IActionResult>(
        ex => StatusCode(StatusCodes.Status500InternalServerError,
            new ApiErrorResponse("ERROR", ex.Message)),
        user => Ok(mapper.Map<UserDto>(user))
    );
}
```

### Validációalapú válasz

```csharp
public async Task<IActionResult> CreateAsync(CreateDto dto)
{
    var result = await service.ValidateAndCreateAsync(dto);
    
    return await result.Match<Task<IActionResult>>(
        errors => Task.FromResult<IActionResult>(
            BadRequest(new ApiValidationResponse(errors))
        ),
        async entity =>
        {
            await service.SaveAsync(entity);
            var dto = mapper.Map<EntityDto>(entity);
            return CreatedAtAction(nameof(GetAsync), new { id = entity.Id }, dto);
        }
    );
}
```

## Összefoglalás

| Szempont | Konvenció |
|--------|-----------|
| Alap útvonal | `/api/[controller]` |
| Metódusnevezés | Ige + ResourceAsync (GetUserAsync, CreateUserAsync) |
| Válasz DTO-k | ProjectedData, nem domain modellek |
| Hibák | ApiErrorResponse Code és Message mezőkkel |
| Validáció | Validation<T> felhalmozott hibákkal |
| Hitelesítés | JWT ****** OpenAPI dokumentáció |
| Lapozás | PagedResult Items, Page, TotalCount mezőkkel |
| Állapotkódok | 2xx siker, 4xx kliens hiba, 5xx szerverhiba |
| Dokumentáció | OpenAPI Scalar UI-val |

---

**Következő lépések**:
- Az entitástervezéshez tekintse át a DOMAIN_MODEL.md fájlt
- A hibakezelési mintákhoz nézze meg a FUNCTIONAL_PROGRAMMING.md fájlt
- Az adatelérésről lásd a DATABASE_ARCHITECTURE.md fájlt
- Az e-mail szolgáltatásokat a felhasználói értesítésekhez és fiókkezeléshez az [EMAIL_SENDER_IMPLEMENTATION.md](./EMAIL_SENDER_IMPLEMENTATION.md) segítségével konfigurálja
