# Auditnapló

Az **Auditnapló** oldal felsorolja, hogy mi változott az alkalmazásban, mikor, és ki végezte a módosítást. Csak olvasható: a bejegyzések nem szerkeszthetők és nem távolíthatók el.

Az `AuditLog.Read` engedélyre van szükség. Az [Adminisztráció](./administration.md) oldal **Auditnapló** csempéjéről nyithatja meg, vagy lépjen a `#/audit-log` címre. Az engedély nélkül ezt látja: „Az auditnapló megtekintéséhez auditnapló-hozzáférés szükséges.”

## Amit lát

Minden sor a következőket mutatja:

- **Mikor** - a módosítás időpontja, a helyi idő szerint.
- **Ki** - a módosítást végző személy, az e-mail-címei, és ha valaki másért tette, akkor a **Megbízó** az a személy. Az alkalmazás saját maga által végzett módosításai **Rendszer**, a bejelentkezett személy nélküli módosítások **Vendég** megjelöléssel jelennek meg.
- **Művelet** - `Created`, `Updated`, `Deleted`, `HardDeleted`, `Restored`, `ChangeOwnership`, `Moved`, `ImpersonationStarted` vagy `ImpersonationEnded`.
- **Entitás** - a rekord lefordított típusa és egy olvasható név hozzá (például egy felhasználó megjelenítendő neve vagy egy szerepkör neve). Ha a típushoz nincs fordítás, a rögzített neve jelenik meg. Vigye az egérmutatót a névre a nyers kulcs megtekintéséhez. A már nem létező azonosítókat változatlanul jelenítjük meg.
- **Változások** - módosításoknál minden megváltozott tulajdonság a régi és az új értékével. Az érzékeny értékek, például a jelszavak, mindig `***` formában jelennek meg.

## Bejegyzések keresése

- A **Kezdete** és a **Vége** határozza meg a megjelenítendő dátumokat. Az alapértelmezés az utolsó hét nap. A kezdődátum nem lehet későbbi a befejező dátumnál, és a dátumok UTC-napok.
- A **Keresés** az entitások rögzített típusnevében, kulcsában és feloldott nevében, a módosító és a megbízó nevében és e-mail-címében, valamint a változások tulajdonságneveiben és értékeiben keres, a kis- és nagybetűk megkülönböztetése nélkül.
- Az **Entitás típusa**, **Művelet**, **Módosító** és **Megbízó** szűrők együtt szűkítik az eredményeket. Válasszon egy pontos típust az auditnaplóban rögzített típusok közül, vagy válassza az összes típust. A személyek neve és e-mail-címe alapján kereshetők.
- A **Rendezés alapja** lehet az időpont, az entitás típusa vagy kulcsa, a művelet vagy a módosító. A **Sorrend** növekvő vagy csökkenő; alapértelmezés szerint a legújabb van elöl.
- A **Visszaállítás** visszaállítja az alapértelmezéseket.

A részletes keresés és a nem időpont szerinti rendezés lapozás előtt vizsgálja a kiválasztott dátumtartományt; nagy auditnaplónál érdemes szűkebb tartományt választani.

## Megszemélyesítés az auditnaplóban

Amíg egy adminisztrátor [megszemélyesít](./impersonation.md) valakit, minden módosítás a **Ki** mezőben az adminisztrátorral, a **Megbízó** mezőben pedig a megszemélyesített személlyel kerül rögzítésre. A megszemélyesítés megkezdése és befejezése külön bejegyzés (`ImpersonationStarted` és `ImpersonationEnded` egy `ValidSession` entitáson), így az adminisztrátor nevére rákeresve láthatja mindazt, ami ez idő alatt történt.

A bejegyzések oldalanként 20-asával jelennek meg, az oldalgombokkal a táblázat alatt.
