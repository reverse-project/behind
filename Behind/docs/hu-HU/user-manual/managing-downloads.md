# Letöltések

A **Letöltések** oldal egy fájltár. A fájlok **mappákban** vannak, a mappák pedig más mappákat is tartalmazhatnak.

## Böngészés és letöltés

- A menü **Letöltések** pontja megnyitja az oldalt. Bal oldalon a **mappafa** látható: a zárt mappának zárt mappaikonja van, a mappának, amelyben éppen jár, és a megnyitott mappáknak nyitott. A mappa melletti nyíl megnyitja vagy bezárja az almappáit.
- Egy mappa kiválasztása táblázatban mutatja a fájljait: a **nevet** (a fájl típusát jelző ikonnal, ismeretlen típus esetén egyszerű fájlikonnal), a **méretet**, azt, hogy mikor lett **feltöltve**, és hogy **ki töltötte fel**. Ha a megjelenített név eltér az eredeti fájlnévtől, a fájlnév alatta látható.
- Kattintson a fájl nevére a letöltéséhez.
- A mappa címe követi a fa szerinti helyét, például a `#/downloads/manuals/drivers` (a kezelőoldalon `#/manage/downloads/manuals/drivers`) a `drivers` slugú mappa a `manuals` slugú mappában. Könyvjelzőzheti vagy megoszthatja.

## Letöltések kezelése

A nyilvános Letöltések oldal csak böngészésre és letöltésre szolgál. A letöltéseket egy külön oldalon kezelheti, az **Adminisztráció > Letöltések** alatt (`#/manage/downloads`), amelyen ugyanaz a mappafa és fájltáblázat található, kiegészítve az alábbi eszközökkel. A `Download.Read` engedéllyel rendelkező felhasználók a nyilvános oldalon egy **Letöltések kezelése** gombot is látnak, amely ugyanazt a mappát nyitja meg a kezelőoldalon.

| Megteheti | Szükséges |
|-----------|-----------|
| Az eszköz megtekintése az adminisztrációs oldalon | a `Download.Read` engedély |
| Létrehozás, szerkesztés és feltöltés, fájlok átnevezése | a `Download.Write` engedély |
| Mappák és fájlok törlése | a `Download.Delete` engedély |
| A törölt mappák és fájlok megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztőknek) |

### Mappák

- Az **Új mappa** egy mappát hoz létre abban a mappában, amelyben éppen jár (legfelül, ha nincs mappa kiválasztva). Töltse ki a fában megjelenő **megjelenítendő nevet** és a címben használt **URL-azonosítót (slug)**. A slug a megjelenítendő névből készül, amíg Ön be nem ír sajátot; csak kisbetűket, számjegyeket és kötőjeleket tartalmazhat, és egyedinek kell lennie a mappán belül.
- A **Mappa szerkesztése** módosítja annak a mappának a megjelenítendő nevét és slugját, amelyben éppen jár. A mappa (és almappái) címe a sluggal együtt változik.
- A **Mappa törlése** törli azt a mappát, amelyben éppen jár. Az almappái és fájljai vele együtt el lesznek rejtve.

### Fájlok feltöltése

1. Válasszon egy mappát.
2. A **Fájlok feltöltése** alatt válasszon ki egy vagy több fájlt. Egyszerre többet is kiválaszthat.
3. Minden kiválasztott fájl kap egy **megjelenítendő nevet**. Ez a fájlnév, amíg Ön meg nem változtatja. A nem kívánt fájlokat az **Eltávolítás** gombbal távolíthatja el.
4. A **Feltöltés** egymás után elküldi a fájlokat. Az üres vagy a megengedett maximumnál nagyobb fájlt (a maximum a fájlválasztó alatt látható, és a kiszolgáló adminisztrátora állítja be a `backend.json` fájlban) megjelöljük, és nem töltjük fel; a többit igen.

A táblázatban az **Átnevezés** gombbal később módosíthatja a feltöltött fájl megjelenítendő nevét, a **Törlés** gombbal pedig törölheti.

### Törölt mappák és fájlok

- A **Törlés** átmeneti: semmi nem vész el.
- A fejlesztők egy **Törölt mappák és fájlok megjelenítése** kapcsolót látnak. A törölt mappák és fájlok **visszaállíthatók** vagy **véglegesen törölhetők**. Egy mappa csak akkor állítható vissza, ha az a mappa, amelyben volt, nincs törölve, és egyetlen másik mappa sem használja benne a slugját; egy fájl csak akkor állítható vissza, ha a mappája nincs törölve. Egy mappa végleges törlése az almappáit és fájljait, valamint a fájlok tartalmát is végleg eltávolítja.

Minden itt végzett módosítás rögzítésre kerül az [auditnaplóban](./audit-log.md), a mappák és fájlok megjelenítendő nevével.

## Rövid útvonalak mappákhoz

Egy mély mappa, például a `#/downloads/products/manuals`, kaphat egy `#/manuals` formájú rövid útvonalat egy [útvonal-álnévvel](./managing-route-aliases.md).
