# Üzenetek

Az **üzenet** rövid értesítés, például karbantartási figyelmeztetés vagy közlemény, amelyet widgetként helyezhet el egy [elrendezésben](./managing-sites.md). Minden üzenetnek van szintje, címe, formázott tartalma és választható ütemezése, így magától jelenik meg és tűnik el.

Az eszközt az **Adminisztráció > Üzenetek** oldalon találja (`#/manage/messages`).

| Művelet | Szükséges |
|---------|-----------|
| Üzenetek megtekintése | a `Message.Read` engedély |
| Üzenetek létrehozása és szerkesztése | a `Message.Write` engedély |
| Üzenetek törlése | a `Message.Delete` engedély |
| Törölt üzenetek megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapértelmezés szerint megkapják az első három engedélyt. A felhasználók nem.

## Az üzenet mezői

- a **név**, amely az elrendezésszerkesztőben jelenik meg, hogy kiválaszthassa az üzenetet. A látogatók nem látják,
- a **szint**: **Információ**, **Siker**, **Figyelmeztetés** vagy **Veszély**. A szint határozza meg az értesítés színét,
- az opcionális **cím**, az értesítés fejlécében jelenik meg,
- a **tartalom**, amelyet a formázott szövegszerkesztőben ír. A szkriptek és a nem biztonságos HTML mentéskor eltávolításra kerülnek,
- a **Látható ettől** és a **Látható eddig** mező, mindkettő választható. Az időpontokat helyi időben adja meg. Hagyja üresen a **Látható ettől** mezőt, ha az üzenetnek azonnal meg kell jelennie, a **Látható eddig** mezőt pedig, ha a törléséig láthatónak kell maradnia. A befejezésnek későbbinek kell lennie a kezdetnél.

A listán van **Keresés** mező (a nevet és a címet vizsgálja), a név első betűjéhez tartozó **betűgombok** és oldalgombok.

## Üzenet megjelenítése az oldalon

1. Nyissa meg az **Adminisztráció > Elrendezések** oldalt.
2. Vegyen fel egy widgetet egy szakaszba, és válassza az **Üzenet** widgettípust.
3. Válassza ki az üzenetet az **Üzenet** listából, majd mentse az elrendezéseket.

Az értesítés csak addig látszik, amíg az üzenet az ütemezés szerint él és nincs törölve; egyébként a widget nem jelenít meg semmit. A már megnyitott oldalak körülbelül egy percen belül követik az ütemezést, újratöltés nélkül.

A látogatók a bezárás gombbal elrejthetik az értesítést. A böngésző megjegyzi ezt, így az értesítés az adott látogatónak rejtve marad, amíg módosítja az üzenetet: a módosítás mentése újra megjeleníti.

## Törlés és visszaállítás

- A **Törlés** helyreállíthatóan törli az üzenetet. Az értesítés eltűnik az oldalról. Semmi sem vész el.
- A fejlesztők **Törölt elemek megjelenítése** kapcsolót látnak. A törölt elemnél **Visszaállítás** és **Végleges törlés** gomb jelenik meg.

Minden itt végzett módosítás bekerül az [auditnaplóba](./audit-log.md), azonosítók helyett az üzenet nevével.
