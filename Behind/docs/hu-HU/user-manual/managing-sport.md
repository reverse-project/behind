# Sport

A **Sport** a csapatok, játékosok és statisztikáik modulja. Elsősorban focira készült, de minden csapat, játékos, szezon és statisztika egy sportághoz tartozik, így később más sportok is hozzáadhatók változtatás nélkül.

A **Sport** pontot mindenki böngészheti a főmenüben (`#/sport`). Az adatokat az **Adminisztráció > Sport** alatt kezelheti (`#/manage/sport`).

| Művelet | Ehhez kell |
|---------|------------|
| A kezelőoldalak megtekintése | a `Sport.Read` engedély |
| Bármi létrehozása és szerkesztése, logók és képek feltöltése | a `Sport.Write` engedély |
| Sorok törlése | a `Sport.Delete` engedély |
| A törölt sorok megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapból minden engedéllyel rendelkeznek, a felhasználók olvashatnak.

## Böngészés

- **Sportágak**: a sportág felsorolja a pozícióit, statisztikatípusait és szezonjait.
- **Csapatok**: a csapatnak opcionális logója, helye és alapítási éve van, szezononként pedig játékosai és statisztikái.
- **Játékosok**: a játékosnak képe, kora, pozíciója, nemzetisége, magassága, testsúlya, erősebb oldala és életrajza van. A szezontörténet minden szezonhoz más képet, csapatot, mezszámot és statisztikát mutat.
- **Szezonok**: a szezonban játszó csapatok, hivatkozásokkal a ranglistákra és statisztikákra.
- **Ranglisták**: egy szezon legjobb játékosai vagy csapatai minden statisztikatípusra (első 10), és egy statisztikatípus teljes rangsora. Az azonos értékek azonos helyezést kapnak, az olyan statisztikatípusokat, ahol a kevesebb a jobb (például kapott gólok), növekvő sorrendben rangsorolja a rendszer.
- **Statisztikák**: egy szezon összes statisztikája, szükség esetén egy statisztikatípusra szűkítve.

A csapatok, játékosok, szezonok, sportágak, pozíciók és statisztikatípusok mindenhol hivatkozások, ahol megjelennek.

## Kezelés

A kezelőközpont nyolc oldalra mutat: sportágak, pozíciók, statisztikatípusok, szezonok, csapatok, játékosok, szezontagságok és statisztikák. Először egy sportágat hozzon létre (az új telepítés a **Focival**, annak pozícióival és statisztikatípusaival indul), majd szezonokat, csapatokat és játékosokat. Minden hivatkozott elemnek ugyanahhoz a sportághoz kell tartoznia.

- A **szezontagság** egy játékost egy csapatba helyez egy szezonra mezszámmal és az adott szezonban használt képpel. Egy játékos szezononként egy csapatban lehet.
- A **statisztika** egy statisztikatípus egy értéke egy játékosra vagy csapatra (a típus dönti el, melyikre) egy szezonban; statisztikatípusonként, alanyonként és szezononként egy érték lehet.
- A **statisztikatípus** nem válthat játékos- és csapatstatisztika között, amíg statisztikák használják.

A keresés a nevekben keres, a betűsor az első betű szerint szűr, a listák lapozottak. A **Törlés** csak elrejti a sort. A fejlesztők bekapcsolják a törölt sorokat, majd **Visszaállítják** a sort vagy **Véglegesen törlik**, ami elutasításra kerül, amíg másik sor használja. Minden változás megjelenik az [auditnaplóban](./audit-log.md), a táblák pedig szerkeszthetők a [nyers adatbázisban](./managing-raw-database.md).
