# Beállítások felfedezése és módosítása 1.1.0

Állapot: aktuális. Ez a verzió kiegészíti a [konfiguráció 1.7.0](./configuration-json-1.7.0.md)
szerződését, és felváltja annak 1.0.0 verziójú beállítás-felfedezését.
A konfigurációs fájl sémája változatlan.

## Felfedezés

A `GET /settings` `Configuration.Read` jogosultságot igényel, és a
[beállításválasz sémáját](../../schemas/settings-response-1.1.0.schema.json)
adja vissza. A létező konfigurációs fájlokat és mezőiket sorolja fel, a hiányzó
ismert értékek alapértékeivel együtt. A mezőértékek szövegek; az `array` és
`object` értékek JSON-szöveget tartalmaznak. A megjelenített alapérték csak
módosításkor íródik ki.

Az `appsettings.json` fájlban a `Serilog:Using`, `Serilog:Enrich`,
`Serilog:WriteTo`, `Serilog:AuditTo`, `Serilog:Filter` és
`Serilog:Destructure` tömbmezők, nem indexelt skaláris levelek. Hiányzó vagy
null tömb esetén a beágyazott alapérték, ennek hiányában üres tömb jelenik meg.
A nem tömb szakasz
meglévő skaláris levelei továbbra is elérhetők. Az ismeretlen tulajdonságok
megmaradnak. Az érzékeny nevű beágyazott tulajdonságok maszkoltak; a mező
`sensitive` jelzője nem írja le az összes gyermekét. A naplózási és telemetriai
szerkesztő továbbra is a saját szakaszáért felel.

## Módosítás

A `PUT /settings/{file}` `Configuration.Manage` jogosultságot igényel;
kérése a [beállításmódosítás sémáját](../../schemas/settings-update-1.1.0.schema.json)
követi. A `values` térkép kettősponttal elválasztott útvonalakhoz adja a
tipizált módosításokat. Az appsettings esetén a `logging` és `openTelemetry` is
kötelező. A teljes fájl sémaellenőrzése, a csak olvasható védelem és az
atomi fájlcsere megmarad.

A Serilog-tömbök elküldött ismeretlen elemei, tulajdonságai és JSON-típusai
megmaradnak. A változatlan maszkolt titkokat a rendszer az érzékeny tulajdonság
neve és maszkja alapján állítja vissza az előző fájlból, átrendezés esetén is.
Az üres titokcsere csak azonos helyű, változatlan és egyedi nevű eredeti elem
esetén marad meg; az egyértelműtlen esetek elutasítottak.
Ha különböző meglévő titkok neve és maszkja azonos, a kérés találgatás helyett
elutasított; a gyűjtemény szerkesztése előtt kifejezetten cserélje le az ütköző
értékeket. Egy elem vagy tulajdonság eltávolítása a titkát is eltávolítja.
A kapcsolati karakterláncok térképe az egyedi maszkú értéket kulcsának
átnevezésekor is megőrzi, az egyértelműtlen maszkot elutasítja. Egy elem
eltávolítása vagy null értéke kifejezetten törli azt.

## Kompatibilitás és migráció

A válasz szerkezete és mezőtípusai nem változtak, de a klienseknek a
Serilog-tömböt kell kezelniük indexelt skaláris útvonalak helyett. A backend
és frontend megfelelő verzióit együtt telepítse. A meglévő konfigurációs
fájlok nem igényelnek migrációt. A naplószintek korábbi újratöltése megmarad;
sinkek, gazdagítók, szűrők, strukturálás vagy assemblyk módosítása után az
alkalmazást újra kell indítani.
