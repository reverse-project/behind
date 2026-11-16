# Gyorsítótár-konfiguráció 1.1.0

Állapot: aktuális. Az [1.0.0](./caching-configuration-1.0.0.md) dokumentációs
sémájának kompatibilitási javítása; a futásidejű szintaxis változatlan.

[JSON Schema](../../schemas/caching-configuration-1.1.0.schema.json).

A `CachingConfiguration` továbbra is csak `Provider` és `Key` mezőt tartalmaz.
Az alapérték Memory; a RedisCompatible a Key alapján a testvér ConnectionStrings
szakaszból választ kapcsolatot. Hiányzó konfiguráció, null Caching, hiányzó/null/üres
Key, illetve hiányzó/null/üres névvel ellátott kapcsolat esetén memória használható.
Kapcsolat nélküli RedisCompatible indulási figyelmeztetést naplóz.
A szolgáltatásmódosítások újraindítást igényelnek.

A korábbi dokumentációs sémával ellentétben a null Caching, null kapcsolatérték
és megőrzött bővítési mezők elfogadottak, a meglévő kötésnek és Beállításoknak
megfelelően. Ez nem támogat további CachingConfiguration mezőket vagy beágyazott
kapcsolati karakterláncot. A szolgáltatók Memory/RedisCompatible értékek maradnak;
a szerkesztő megtartja mező-/útvonalellenőrzését és titokkezelését.

A [konfiguráció 1.0.0 futásidejű sémája](./configuration-json-1.0.0.md) ugyanazt
a javított alakot ágyazza be és mentés előtt ellenőrzi a tervezett dokumentumot.
Az 1.0.0 személyesadat- és érvénytelenítési szabályai megmaradnak. Nincs migráció.
