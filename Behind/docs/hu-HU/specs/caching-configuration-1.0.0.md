# Gyorsítótár-konfiguráció 1.0.0

Állapot: aktuális. Séma: [caching-configuration-1.0.0.schema.json](../../schemas/caching-configuration-1.0.0.schema.json).

## Szerződés

Az opcionális `caching.json` `Caching` objektumának pontosan két beállítása van:

- `Provider`: `Memory` (alapérték) vagy `RedisCompatible`. A futásidejű típusok
  `CachingProvider` rekordváltozatok típus- és JSON-konverterekkel, az adatbázis
  szolgáltatóihoz hasonlóan.
- `Key`: opcionális karakterlánc vagy null; egy `ConnectionStrings` bejegyzés
  neve, nem végpont, hitelesítési adat vagy kapcsolati karakterlánc.

A mellette található `ConnectionStrings` neveket rendel karakterláncokhoz.
A `startup create` `Memory` szolgáltatót, `CachingConnection` kulcsot és üres
`ConnectionStrings:CachingConnection` értéket készít. A `RedisCompatible` a
kiválasztott kapcsolatot használja, Valkey támogatással. Hiányzó fájl, szolgáltató
vagy kapcsolat esetén csak memória használható; hiányzó kapcsolatú
RedisCompatible esetén induláskori figyelmeztetés jelenik meg. Érvénytelen
szolgáltató megakadályozza az indulást. A megadott, de elérhetetlen végpontnál a
kliens és a gyorsítótár szokásos hibadiagnosztikája érvényesül, nem váltunk
csendben másik szolgáltatóra.

## Beállítások és biztonság

A meglévő Beállítások API és űrlap külön `caching.json` lapot jelenít meg.
A szolgáltató ellenőrzött választólista, a Key szerkeszthető. A névvel ellátott
kapcsolatok jelszómezők, válaszban csak maszkolva szerepelnek. Az üres jelszómező
megtartja a titkot; a Key törlése újraindítás után kikapcsolja az elosztott
háttértárat. A konfigurációolvasási és -kezelési jogosultságok és az írásvédelem
változatlanok. A módosítás újraindítást igényel, nincs futásidejű szolgáltatócsere.

Személyes adatok, hitelesítési adatok, felhasználói szövegek, munkamenetek és
jogosultsági eredmények nem gyorsítótárazhatók. A parancsok mindig végrehajtódnak.
Az összesítések véglegesített auditrevízióval, tranzakciók megkerülésével és
adatbázis/modell elkülönítéssel működnek; a lejárat nem elegendő érvénytelenítés.
A Valkey legyen privát, hitelesített és TLS-sel védett.

## Kompatibilitás és áttérés

Ez az első publikált gyorsítótár-konfigurációs szerződés. Fájl nélkül a meglévő
telepítések memóriaalapú működése megmarad. A `startup create --skip-existing`
parancs létrehozza a fájlt; csatolja a futtatható állomány mellé és indítsa újra.
A compose példa csatolja a fájlt a webes gazdához.

A korábbi fejlesztői `Backend:Caching` alakot már nem olvassuk. A kapcsolatot
helyezze a `caching.json` `ConnectionStrings` szakaszába, válassza ki a
`Caching:Key` mezővel, és törölje a régi `ConnectionString`, `InstanceName` és
`QueryExpiration` mezőket. A `CachingConfiguration` nem kaphat közvetlen
kapcsolatot vagy gyorsítótár-szabályzatot tartalmazó mezőket.
