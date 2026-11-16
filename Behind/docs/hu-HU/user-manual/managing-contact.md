# Kapcsolatűrlapok és beérkezett kérések

A kapcsolatkezelés telepített Contact modullal érhető el. A fő Behind telepítés tartalmazza; a meglévő önálló webhelytelepítések saját modulválasztása változatlan.

## Kérés küldése

Nyissa meg a `#/contact` oldalt, vagy használjon egy elrendezésbe helyezett kapcsolatűrlap-widgetet. Válasszon aktív űrlapot, majd adja meg nevét, e-mail-címét, a tárgyat, üzenetet és a kiegészítő mezőket. Az űrlap rögzített tárgyakat kínálhat vagy saját tárgyat engedhet. A kötelező mezőket ki kell tölteni, a kötelező jelölőnégyzeteket be kell jelölni. Az opcionális jelölőnégyzetek kezdetben nincsenek bejelölve, és üresen hagyhatók.

Az alkalmazás menti a kérést az adminisztrátori beérkezett listába. Ha az űrlaphoz e-mail-címzett tartozik, ezután megpróbálja az e-mail-küldést. Kézbesítési hiba esetén a mentett kérés megmarad újrapróbáláshoz. Ne küldje be újra az elfogadott kérést csak azért, mert az e-mail-küldés függőben van vagy sikertelen.

## Űrlapok beállítása

Nyissa meg az **Adminisztráció > Kapcsolat** (`#/administration/contact`) oldalt és az űrlapkezelési részt. Tetszőleges számú űrlapot hozhat létre; mindegyiknél beállítható:

- A megjelenítendő név és leírás.
- Az opcionális cél e-mail-címe, amelyet csak jogosult adminisztrátor lát. Hagyja üresen az adminisztrátori beérkezett lista e-mail-küldés nélküli használatához.
- Rögzített tárgyak vagy szabad szöveges tárgy.
- A kiegészítő mezők kulcsa, felirata, szöveg/többsoros szöveg/e-mail/logikai típusa, kötelezősége és a szöveges mezők legnagyobb hossza.

A logikai mezők jelölőnégyzetként jelennek meg a kapcsolatoldalon és a widgetben is. Kötelező jelölőnégyzettel kifejezett hozzájárulást kérhet, opcionálissal pedig például hírlevélre jelentkezést. A jelölőnégyzet csak a kérésben rögzíti a választást; nem iratkoztatja fel a feladót levelezőlistára.

Csak a logikai mezők felirata támogatja a BBCode-ot; a többi mező felirata egyszerű szöveg marad. Például az `Elfogadom a [url=https://example.org/terms]feltételeket[/url]` hivatkozást ad a hozzájárulás feliratához. A logikai mezők felirata legfeljebb 2000 karakteres lehet a BBCode-dal együtt, a többi felirat legfeljebb 200 karakteres. A hivatkozások a meglévő BBCode-hivatkozásmegerősítést használják. A logikai értékek `true` vagy `false` szövegként kerülnek mentésre; a kihagyott opcionális érték `false`, a kötelező logikai értéknek pedig `true`-nak kell lennie. A legnagyobb hossz nem vonatkozik a logikai mezők beküldött értékére.

A törölt űrlap nem fogad nyilvános kérést. Az űrlap módosítása nem változtatja meg a korábbi beküldést: az eredeti űrlapnév, mezőfelirat, érték és címzett megmarad.

Az e-mail-kézbesítés opcionális. Célcímmel rendelkező űrlapokhoz az **Adminisztráció > Beállítások > E-mail** részen állítson be egy e-mail-kézbesítő szolgáltatót. A Logger szolgáltató a naplózás után elküldöttnek jelöli a beküldést, de nem küld e-mailt postaládába; az üzenet törzsét csak fejlesztői környezetben naplózza. Az üzenet szolgáltató általi elfogadása nem igazolja, hogy a címzett elolvasta azt.

## Elrendezéswidget

A meglévő elrendezésszerkesztőben adja hozzá a **Kapcsolatűrlap** widgetet. Ugyanazokat az aktív űrlapokat és ellenőrzést használja, mint a kapcsolatoldal, és örökli a telepítés Bootstrap-témáját. Csak telepített modul widgetje választható.

## Adminisztrátori beérkezett lista

A lista a mentett kéréseket, létrehozási idejüket, valamint kézbesítési és olvasási állapotukat mutatja. Kereshet az űrlap neve, a feladó neve vagy e-mail-címe, a tárgy és az üzenet alapján; a találatok között a lapozóval navigálhat. Az **Olvasottak elrejtése** kapcsolóval csak az olvasatlan kérések jelennek meg; alapértelmezés szerint minden kérés látható. A vezérlők a törölt kérések listájára is érvényesek. Nyisson meg egy kérést az összes beküldött adat megtekintéséhez. Kezelés után külön jelölje olvasottnak; a közös állapot rögzíti az olvasottnak jelölő adminisztrátort, akinek megjelenített neve a részletekben a profiljára mutat. A puszta megtekintés nem jelöl olvasottnak.

A jogosult adminisztrátor újrapróbálhatja a sikertelen küldést. Az újrapróbálás az eredeti mentett címzettet és adatokat használja akkor is, ha az űrlapot később módosították. A csak a beérkezett listába mentett kérések kézbesítési állapota `notRequested`; ezek akkor sem próbálhatók újra, ha később címzettet adnak az űrlaphoz. A sikeresen elküldött üzenet nem próbálható újra sikertelenként.

A `sending` állapot megakadályozza a párhuzamos ismételt kézbesítést. Ha az alkalmazás küldés közben leáll, ez az állapot megmaradhat, mert a kézbesítés eredménye ismeretlen. További lépés előtt ellenőrizze a célpostaládát; az alkalmazás nem próbálja újra automatikusan. A rekord továbbra is törölhető.

Az űrlapok és kérések átmenetileg törölhetők. A `RawDatabase.Manage` engedéllyel rendelkező fejlesztők listázhatják a törölt rekordokat, visszaállíthatják vagy végleg törölhetik őket. A beérkezett elem törlése nem törli a postaládába már megérkezett e-mailt. Csak a telepítéshez szükséges személyes adatokat tartsa meg, és a közös adatbázisról készítsen biztonsági mentést.
