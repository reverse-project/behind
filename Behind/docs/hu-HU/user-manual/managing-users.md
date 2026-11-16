# Felhasználók kezelése

Nyissa meg az **Adminisztráció > Felhasználók** oldalt. Ehhez a `User.Manage` engedély kell.

## Felhasználók keresése

- **Keressen** név vagy e-mail-cím alapján, és válassza a **Keresés** gombot.
- Használja a **betűsort** a név első betűje szerinti szűréshez (vagy az e-mail-cím szerint, ha nincs név). A felhasználó nélküli betűk le vannak tiltva; a **#** mindent lefed, ami nem betű.
- Az alul lévő lapozóval lépkedhet az oldalak között.

Minden felhasználó mutatja a nevét, e-mail-címeit, szerepkör-jelvényeit és az esetleges kitiltási üzenetet. A jelvények a következőket jelölik: **Ön**, **Az e-mail-cím nincs megerősítve**, **Kitiltva** és **Törölve** felhasználók.

A felhasználó nevére kattintva megnyithatja nyilvános profilját. A profilon a **Kitiltás**, **Kitiltás feloldása** és **Megszemélyesítés** is elérhető, ha a szerver engedélyezi. Az e-mail elrejtése a nyilvános profilra vonatkozik, nem erre a jogosultsággal védett kezelőoldalra.

A gombok csak akkor jelennek meg vagy működnek, ha használhatja őket. Önmagát soha nem moderálhatja, és más fejlesztőket csak a fejlesztők moderálhatnak.

## Szerepkör-gyorsparancsok

Az **Adminisztrátorrá tétel** / **Adminisztrátori jog elvétele** és a **Fejlesztővé tétel** / **Fejlesztői jog elvétele** hozzáadja vagy eltávolítja ezeket a beépített szerepköröket. A fejlesztői szerepkört csak a fejlesztők módosíthatják.

Bármely más szerepkörhöz válassza egy felhasználónál a **Szerepkörök** gombot, és jelölje be azokat a szerepköröket, amelyekkel a felhasználónak rendelkeznie kell (lásd [Felhasználó szerepköreinek módosítása](#felhasználó-szerepköreinek-módosítása)), vagy használja a [Hozzáférés panelt](#felhasználó-hozzáférésének-szerkesztése).

## Felhasználó kijelentkeztetése

A **Kijelentkeztetés mindenhonnan** a felhasználó összes munkamenetét befejezi.

A **Megszemélyesítés** lehetővé teszi egy adminisztrátornak, hogy a felhasználó nevében járjon el, lásd [Megszemélyesítés](./impersonation.md).

## E-mail-cím megerősítése

Az **E-mail-cím megerősítése** azoknál a felhasználóknál jelenik meg, akiknek az e-mail-címe még nincs megerősítve. Megerősíti az e-mail-címet anélkül, hogy a felhasználónak a megerősítő levélben lévő hivatkozást kellene használnia, így a felhasználó be tud jelentkezni, ha az e-mail-cím megerősítése kötelező.

## Kitiltás és a kitiltás feloldása

1. Válassza a **Kitiltás** gombot.
2. Adja meg a [szégyenfalon](./wall-of-shame.md) megjelenő üzenetet (nem kötelező, legfeljebb 500 karakter; hagyja üresen az üzenet nélküli kitiltáshoz).
3. Válassza a **Felhasználó kitiltása** gombot.

A felhasználó ki van tiltva, és az összes munkamenetéből ki van jelentkeztetve. A **Kitiltás feloldása** feloldja a kitiltást.

## Törlés és visszaállítás

- A **Törlés** eltávolítja a felhasználót. Ehhez a `User.Delete` engedély kell, és megerősítést kérnek. A törölt felhasználót egy fejlesztő vissza tudja állítani.
- A fejlesztők a **Törölt felhasználók megjelenítése** kapcsolót is látják. A törölt felhasználóknál a **Visszaállítás** gombbal visszaállíthatják a fiókot, vagy a **Végleges törlés** gombbal véglegesen törölhetik. A végleges törlés nem vonható vissza, és a rendszer elutasítja, ha az auditnapló még hivatkozik a felhasználóra.

## Felhasználó szerepköreinek módosítása

Válassza egy felhasználónál a **Szerepkörök** gombot a szerepkörök panel megnyitásához, jelölje be vagy törölje a szerepkörök jelölését, és válassza a **Szerepkörök mentése** gombot. A **Szerepkörök elrejtése** bezárja. A **Users** szerepkör kötelező, és a jelölése nem törölhető. Csak azoknak a felhasználóknak módosíthatja a szerepköreit, akiket moderálhat, és csak a saját engedélyei keretein belül. A felhasználó közvetlen engedélyei és korlátozásai változatlanok maradnak.

## Felhasználó hozzáférésének szerkesztése

Válassza egy felhasználónál a **Hozzáférés** gombot a hozzáférési panel megnyitásához. A **Hozzáférés elrejtése** bezárja. Négy részből áll.

### Számított engedélyek

Az engedélyek, amelyekkel a felhasználó ténylegesen rendelkezik: a szerepköreinek összes engedélye plusz a közvetlen engedélyei, mínusz az összes korlátozás. Frissül, ahogy módosítja az alábbi választásokat, még mentés előtt. Lásd [a számítás módját](./managing-roles-and-access.md#hogyan-működnek-a-számított-engedélyek).

### Szerepkörök

Jelölje be azokat a szerepköröket, amelyekkel a felhasználónak rendelkeznie kell. A **Users** szerepkör kötelező, és a jelölése nem törölhető.

### Közvetlen engedélyek és korlátozások

Minden engedélynél van egy legördülő lista:

- **Nincs beállítva**: nincs közvetlen hatása.
- **Engedélyezés**: a felhasználó a szerepkörei mellett megkapja ezt az engedélyt.
- **Korlátozás**: a felhasználó elveszíti ezt az engedélyt akkor is, ha egy szerepkör megadja.

### Mentés

Válassza a **Hozzáférés mentése** gombot. A lista frissül, és megjeleníti az „A hozzáférés mentve." üzenetet. A felhasználó a módosítást a munkamenete következő frissítésekor látja.

A panel csak olvasható, ha nem szerkesztheti az adott felhasználót (saját magát, vagy egy fejlesztőt, ha Ön nem az). A mentést a rendszer üzenettel elutasítja, ha:

- olyan engedélyt próbál adni a felhasználónak, amellyel Ön maga nem rendelkezik (szerepkörön, közvetlen engedélyen vagy korlátozás feloldásán keresztül), vagy
- egy választás nem érvényes.
