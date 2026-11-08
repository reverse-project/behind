# Szerepkörök és hozzáférés kezelése

Nyissa meg az **Adminisztráció > Szerepkörök** oldalt. Ehhez a `Role.Manage` engedély kell.

## Fogalmak

- Az **engedély** valamit lehetővé tesz. Az engedélyek nevei `Terület.Művelet` alakúak, például `User.Manage`. A telepített modulok saját területeket adnak hozzá (például `Notes.Read`); ezek a beépítettek mellett jelennek meg, és a beépített szerepkörök automatikusan megkapják őket, anélkül hogy elveszítenék a szerepkörökön végzett módosításait.
  - Területek: Session, AuditLog, Role, User, Configuration, Impersonation, RawDatabase.
  - Műveletek: Read, Write, Delete, Manage (az Impersonation és a RawDatabase csak a Manage műveletet ismeri).
- A **korlátozás** elvesz egy engedélyt. Ugyanazokat a neveket használja, mint az engedélyek.
- A **szerepkör** engedélyek és korlátozások elnevezett halmaza. A felhasználóknak több szerepkörük is lehet.
- A felhasználónak lehetnek **közvetlen** engedélyei és korlátozásai is, amelyek csak rá vonatkoznak.

### Beépített szerepkörök

| Szerepkör | Alapértelmezett engedélyek |
|-----------|----------------------------|
| Users | Read a Session, Role és User területeken |
| Administrators | minden művelet a Session, AuditLog, Role és User területeken, valamint az Impersonation.Manage |
| Developers | csak a fejlesztői engedélyek: Configuration.Read, Configuration.Manage és RawDatabase.Manage (minden felhasználó rendelkezik a Users szerepkörrel is) |

A beépített szerepkörök szerkeszthetők, de nem törölhetők. A rajtuk végzett módosítások megmaradnak az alkalmazás újraindításakor, így az az adatbázis, amely az `AuditLog.Read` eltávolítása előtt jött létre, megtartja azt a Users szerepkörben, amíg egy adminisztrátor el nem távolítja onnan.

### Fejlesztői engedélyek és fejlesztői szerepkörök

A `Configuration.*` és a `RawDatabase.Manage` **fejlesztői engedélyek**. Csak a beépített Developers szerepkörhöz vagy egy **fejlesztői szerepkörként** megjelölt egyéni szerepkörhöz tartozhatnak. Az adminisztrátorok soha nem kaphatnak fejlesztői engedélyeket vagy fejlesztői szerepköröket, és nem adhatják tovább őket, nem rendelhetik felhasználóhoz, és nem szerkeszthetnek fejlesztői szerepkört. Csak a fejlesztők hozhatnak létre fejlesztői szerepkört, vagy kapcsolhatják be vagy ki a jelölést egy egyéni szerepkörnél (válassza a **Fejlesztői szerepkör beállítása** / **Fejlesztői jelölés törlése** lehetőséget). Egy szerepkör nem veszítheti el a jelölést, amíg még fejlesztői hozzáférést ad, és fejlesztői engedélyek nem adhatók hozzá olyan szerepkörhöz, amely nincs megjelölve. A fejlesztők továbbra is adhatnak fejlesztői engedélyeket közvetlenül egy felhasználónak.

### Hogyan működnek a számított engedélyek

Egy felhasználónál:

1. Gyűjtse össze az összes engedélyt az összes szerepköréből és az összes közvetlen engedélyt.
2. Gyűjtse össze az összes korlátozást az összes szerepköréből és az összes közvetlen korlátozást.
3. Távolítsa el a korlátozott engedélyeket az összegyűjtött engedélyek közül.

Az eredmény a felhasználó **számított engedélyei**. A korlátozás mindig erősebb az engedélynél, bárhonnan is származzon az engedély. Az egyik szerepkör korlátozása ezért a felhasználó más szerepköreiből kapott engedélyeket is érinti.

Példa: egy felhasználónak van Administrators szerepköre és `User.Delete` közvetlen korlátozása. Minden adminisztrátori engedélyét megtartja, kivéve a `User.Delete` engedélyt.

Így adhat valakinek szűkebb hozzáférést, mint amit a szerepköre ad: tartsa meg a szerepkört, és adjon hozzá korlátozásokat, vagy helyezzen korlátozásokat egy szerepkörre, hogy mindenkinél szűkítse a hozzáférést, akinek megvan.

## A szerepkörök oldala

Minden szerepkör mutatja az **Engedélyeit** (zöld) és **Korlátozásait** (piros).

### Szerepkör létrehozása

Írjon be egy nevet (legfeljebb 100 karakter) az **Új szerepkör neve** mezőbe, és válassza a **Szerepkör létrehozása** gombot. A neveknek egyedinek kell lenniük, a kis- és nagybetűk figyelmen kívül hagyásával. Az új szerepkörnek nincsenek engedélyei.

### Szerepkör szerkesztése

1. Válassza a szerepkör **Hozzáférés szerkesztése** gombját.
2. Minden engedélynél válasszon a **Nincs beállítva**, az **Engedélyezés** és a **Korlátozás** közül.
3. Válassza a **Mentés** gombot, vagy a **Mégse** gombot az elvetéshez.

Egy engedély egy szerepkörön vagy engedélyezett, vagy korlátozott lehet, nem mindkettő. A szerepkör korlátozásai minden olyan felhasználóra vonatkoznak, akinek megvan a szerepkör.

### Beépített szerepkör engedélyeinek visszaállítása

Válassza az **Engedélyek visszaállítása** gombot egy beépített szerepkörön, és a megerősítés után a szerepkör engedélyei visszaállnak a beépített alapértékekre. A szerepkör korlátozásai megmaradnak. Ugyanazok a szabályok érvényesek, mint a szerepkör szerkesztésére, így csak akkor állíthatja vissza a szerepkört, ha módosíthatja, és Ön is rendelkezik minden engedéllyel, amelyet a szerepkör kapna.

### Szerepkör törlése

Válassza a **Törlés** gombot egy egyéni szerepkörön, és erősítse meg. Ehhez a `Role.Delete` engedély kell. A felhasználók elveszítik a szerepkör által adott hozzáférést. A beépített szerepkörök nem törölhetők. A törölt szerepkör megmarad, és egy fejlesztő vissza tudja állítani.

### Keresés és lapozás

A listán van **Keresés** mező (a szerepkör nevére illeszkedik), betűszűrő és lapozógombok, mint a felhasználók oldalán.

### Törölt szerepkörök (fejlesztők)

A fejlesztők látnak egy **Törölt szerepkörök megjelenítése** kapcsolót. A törölt szerepkörökön **Törölve** jelvény látható a **Visszaállítás** gombbal, amely visszahozza a szerepkört, és a **Végleges törlés** gombbal, amely véglegesen eltávolítja. A még hivatkozott szerepkör nem törölhető véglegesen.

## Korlátok

- Csak olyan szerepkört szerkeszthet, amelynek engedélyeivel Ön is rendelkezik, és csak olyan engedélyeket adhat hozzá, amelyekkel Ön is rendelkezik. A korlátozás feloldása is engedély hozzáadásának számít.
- A fejlesztők bármely szerepkört módosíthatják és bármely engedélyt hozzáadhatnak, de a fejlesztői engedélyek csak fejlesztői szerepkörökhöz illenek.
- Senki sem adhat engedélyeket egy olyan szerepkörhöz, amellyel ő maga is rendelkezik (a korlátozás feloldásával sem); az engedélyek eltávolítása megengedett. Saját hozzáférését a Felhasználók oldalon sem módosíthatja.
- Az adminisztrátorok nem nyúlhatnak a fejlesztői engedélyekhez és fejlesztői szerepkörökhöz.
- Az Ön által nem használható gombok el vannak rejtve. Ha egy mentést elutasítanak, üzenetet lát, és semmi sem változik.

Ha szerepkört szeretne adni valakinek, vagy egy személy hozzáférését szeretné módosítani, használja a [Felhasználók oldal Hozzáférés paneljét](./managing-users.md#felhasználó-hozzáférésének-szerkesztése).
