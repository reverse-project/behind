# Útvonal-álnevek

Az **útvonal-álnév** rövid útvonalat ad egy oldalnak. Ha egy letöltési könyvtár a `#/downloads/products/manuals` címen érhető el, a `manuals` nevű álnév lehetővé teszi a látogatóknak, hogy helyette a `#/manuals` címen nyissák meg. A rövid útvonal megnyitja a céloldalt, és a böngésző címsorában marad.

Az eszközt az **Adminisztráció > Útvonal-álnevek** alatt találja (`#/manage/route-aliases`).

| Művelet | Ehhez kell |
|---------|------------|
| Az útvonal-álnevek megtekintése | a `RouteAlias.Read` engedély |
| Útvonal-álnevek létrehozása és szerkesztése | a `RouteAlias.Write` engedély |
| Útvonal-álnevek törlése | a `RouteAlias.Delete` engedély |
| A törölt útvonal-álnevek megtekintése, visszaállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem. A rövid útvonal megnyitásához nincs szükség engedélyre: a mögötte lévő oldal a szokásos módon ellenőrzi a látogató hozzáférését.

## Útvonal-álnevek

Egy útvonal-álnévnek két mezője van:

- a **rövid útvonal**, a `#/` után beírva, például `manuals`. Kisbetűkké, számokká és kötőjelekké alakul, és egyedinek kell lennie az útvonal-álnevek között, a kis- és nagybetűk megkülönböztetése nélkül,
- a **célútvonal**, az az oldal, amelyet a rövid útvonal megnyit. Az alkalmazás egy oldalának kell lennie, és `#/` előtaggal kell kezdődnie, például `#/downloads/products/manuals`. A szóközöket és az egyéb típusú címeket (például `https://` vagy `javascript:`) a rendszer elutasítja.

Egy letöltési könyvtár célútvonalának megtalálásához nyissa meg a **Letöltések** alatt, és másolja ki a `#` utáni címet.

A listán van **Keresés** mező (a rövid útvonalban és a célban keres), a rövid útvonal első betűjéhez **betűgombok** és lapozógombok. Minden bejegyzés rövid útvonala kattintható hivatkozás, amellyel kipróbálhatja.

## Melyik oldal nyer

- A beépített oldalak (`#/news`, `#/downloads`, `#/administration` stb.) mindig nyernek. A beépített oldallal azonos nevű álnév soha nem nyílik meg.
- Az útvonal-álnév erősebb az azonos címen (`#/<slug>`) lévő [oldalnál](./managing-sites.md).
- Ha egyetlen útvonal-álnév sem illeszkedik, a cím a korábbiak szerint oldalként jelenik meg.
- Az útvonal-álnév a célját egyszer nyitja meg. Ha a cél egy másik rövid útvonal, oldalként jelenik meg, és nem követi tovább.

## Törlés és visszaállítás

- A **Törlés** logikailag töröl egy útvonal-álnevet. A rövid útvonal működése megszűnik, és egy másik álnév használhatja. Semmi sem vész el.
- A fejlesztők látnak egy **Törölt útvonal-álnevek megjelenítése** kapcsolót. A törölt bejegyzésen **Visszaállítás** és **Végleges törlés** jelenik meg. Egy útvonal-álnév csak akkor állítható vissza, ha nincs másik, amely a rövid útvonalát használja.

Minden itt végzett módosítás bekerül az [auditnaplóba](./audit-log.md), azonosítók helyett a rövid útvonallal.
