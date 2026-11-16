# Privát üzenetek és jelentések

Nyissa meg a fiókmenü **Privát üzenetek** pontját. A beérkezett, olvasott, elküldött és törölt mappa lapozható. A beszélgetés olvasottnak jelölése csak a saját postaládáját módosítja; törléskor az a törölt mappába kerül, más résztvevők üzenetei megmaradnak. Visszaállítással újra elérhető a postaládában.

A kapszula alakú navigációval válthat mappát, írhat üzenetet vagy kezelheti a tiltólistát. A kiválasztott nézet kiemelve jelenik meg; a BBCode és a vizuális szerkesztő módja ugyanilyen stílusú.

Az üzenetírás és válasz alapértelmezetten a vizuális szerkesztőben nyílik meg. A BBCode-forrás szerkesztése másodlagos mód, a tárolt üzenetformátum módosítása nélkül.

Más felhasználó nyilvános profilján a **Privát üzenet küldése** megnyitja az üzenetírást a kiválasztott címzettel. Privátüzenet-olvasási és -írási jogosultság szükséges; saját és beépített fióknál a gomb nem jelenik meg.

A beszélgetéshez tárgy, BBCode-tartalom és legalább egy címzett szükséges. Minden beszélgetés csoportos, a meglévő kétszemélyes beszélgetés is; nincs csoportkapcsoló. Szerepkörnek csak **PrivateMessageModeration.Manage** jogosult adminisztrátor írhat, rejtett címzés és meghívás esetén is. A szerepkör aktuális tagjai a korábbi üzenetekhez is hozzáférnek; a szerepkör elhagyásával ez megszűnik. A titkos címzettek nem jelennek meg a többi címzettnek. Válaszuk csak az eredeti feladóhoz jut, nem fedi fel csoportbeli részvételüket.

A beszélgetés legalább két résztvevővel (feladó és egy címzett) indul, és támogatja a tulajdonos meghívásait, a kilépést és visszacsatlakozást. Meghívás nélküli kívülálló nem csatlakozhat. A tiltólista mindkét irányban megakadályozza a szokásos közvetlen üzeneteket. A rendszerértesítések kivételek. A rendszerüzenet válaszát csak a szerző, az eredeti feladó és a rendszerüzenetek moderálására jogosult adminisztrátor látja.

A profil, fórumbejegyzés, nyilvános csevegőüzenet, hírhozzászólás vagy privát üzenet **Jelentés** gombjával adjon indoklást; általános jelentés több felhasználót is megadhat. A kiszolgáló ellenőrzi a forráshoz való hozzáférést és bizonyítékként megőrzi annak tartalmát. Névtelen tartalom jelentése nem fedi fel a szerzőt. A jelentés privát beszélgetést nyit a moderátorokkal, nem a jelentett személyekkel.

A **Report.Manage** jogosult adminisztrátor a Jelentések oldalon ellenőrzi a bizonyítékot és beszélgetést. Elutasíthat, lezárhat, illetve véglegesen vagy adott időpontig kitilthatja a jelentett felhasználókat. Opcionális értesítés privát választ küld a bejelentőnek, és e-mailt a megerősített címmel rendelkező jelentett személyeknek. A kézbesítési hiba látható; csak függő vagy sikertelen kézbesítés próbálható újra. A megszakadt, küldés alatt álló kézbesítés eredménye bizonytalan, nem ismétlődik automatikusan.

A jelentés törlése átmeneti. A fejlesztő listázhatja a törölteket, visszaállíthatja vagy véglegesen eltávolíthatja őket. A jelentés ellenőrzése nem ad hozzáférést más privát beszélgetésekhez.

A tiltólista külön lapozóval rendelkezik. A lezárt és elutasított jelentések részletei megjelenítik a kezelő adminisztrátort, a nyilvános profiljára mutató hivatkozással.

Csak emberi fiók lehet jelentés célpontja. A célpontok böngészése és keresése kizárja a beépített System és Guest fiókot.

A privát üzenetek címzettjeinek böngészése és keresése is kizárja a System, Guest és saját fiókot, résztvevők meghívásakor is.

A címzettkeresés legfeljebb 10 felhasználót, jogosult adminisztrátornak pedig 10 szerepkört mutat. Más címzettek megtalálásához pontosítsa a keresést.

Az új üzenet és válasz a címzettek megerősített e-mail-címére közvetlen beszélgetéshivatkozást küld. Az e-mail nem tartalmaz tárgyat, üzenettartalmat vagy résztvevőazonosságot. A rejtett címzett és aktuális szerepkörtag csak a számára látható üzenetről kap értesítést; a szerző és kilépett résztvevő nem. A moderátor bejelentőnek küldött válasza is értesíti a bejelentőt. A tagságot kézbesítés előtt újra ellenőrzi a rendszer.

Az értesítés az üzenettel együtt mentődik, és percenként kerül feldolgozásra. Ismert SMTP-elutasítás legfeljebb három próbálkozást enged az eredeti megerősített címmel. Megszakadt vagy bizonytalan küldést nem ismétel automatikusan. Az adminisztrátor a Nyers adatbázis oldalon ellenőrizheti az állapotot; a levelezési hiba naplózott. Kézbesítés előtt állítsa be a **Frontend:Url** értéket és a levelezési szolgáltatót. A Logger szolgáltató e-mail helyett naplózza az értesítést.

A jelentés alapján történő kitiltás csak az általános Szabályszegés indokot teszi közzé, a megtekintő felületének nyelvén. Az egyedi kitiltási indok változatlan marad; a privát jelentés indoka nem nyilvános.
