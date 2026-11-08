# Adminisztráció

Az **Adminisztráció** egy olyan oldal, amely az Ön által használható kezelőeszközök gyorsparancsait tartalmazza. A névmenüben lévő bejegyzése a főmenü egyik eleme, amelyet alapértelmezés szerint csak az `Administrators` és a `Developers` szerepkör lát; azt, hogy ki láthatja, a [Menük](./managing-menus.md) oldalon lehet módosítani.

| Eszköz | Szükséges | Kézikönyv |
|--------|-----------|-----------|
| Felhasználók | a `User.Manage` engedély | [Felhasználók kezelése](./managing-users.md) |
| Szerepkörök | a `Role.Manage` engedély | [Szerepkörök és hozzáférések kezelése](./managing-roles-and-access.md) |
| Oldalak | a `Site.Read` engedély (módosításhoz `Site.Write`) | [Oldalak kezelése](./managing-sites.md) |
| Hírek és hírkategóriák | a `News.Read` engedély (módosításhoz `News.Write`) | [Hírek](./managing-news.md) |
| Cikkek és cikkkategóriák | az `Article.Read` engedély (módosításhoz `Article.Write`) | [Cikkek](./managing-articles.md) |
| Galériák | a `Gallery.Read` engedély (módosításhoz `Gallery.Write`) | [Galériák](./managing-galleries.md) |
| Letöltések | a `Download.Read` engedély (módosításhoz `Download.Write`) | [Letöltések](./managing-downloads.md) |
| Menük | a `Menu.Read` engedély (módosításhoz `Menu.Write`) | [Menük](./managing-menus.md) |
| Idézetek (modul) | a `Quotes.Read` engedély (módosításhoz `Quotes.Write`) | [Idézetek](./managing-quotes.md) |
| Dokumentáció (modul) | a `MarkdownViewer.Read` engedély (a statikus oldalak előállításához `MarkdownViewer.Write`) | [Dokumentáció](./managing-markdown-viewer.md) |
| Útvonal-álnevek | a `RouteAlias.Read` engedély (módosításhoz `RouteAlias.Write`) | [Útvonal-álnevek](./managing-route-aliases.md) |
| Hozzáférési beállítások | az `Access.Read` engedély (módosításhoz `Access.Write`) | [Hozzáférési beállítások](./managing-access-settings.md) |
| Nyers adatbázis | a `RawDatabase.Manage` engedély (fejlesztőknek) | [Nyers adatbázis](./managing-raw-database.md) |
| Auditnapló | az `AuditLog.Read` engedély | [Auditnapló](./audit-log.md) |
| Beállítások | a `Configuration.Read` engedély (módosításhoz `Configuration.Manage`) | [Beállítások](./managing-settings.md) |

A telepített modulok saját oldalakat adnak ehhez a listához. Mindegyiket csak azok a felhasználók látják, akik rendelkeznek a modul oldalának engedélyével, és a modulok engedélyei a Szerepkörök és a Felhasználók oldalon is megjelennek, a beépítettekhez hasonlóan.

Az adminisztrátorok alapértelmezés szerint rendelkeznek mindezekkel az engedélyekkel. Egy felhasználó megszemélyesítéséhez az `Impersonation.Manage` engedély szükséges, lásd a [Megszemélyesítés](./impersonation.md) oldalt. Ha a szükséges hozzáférés nélkül nyit meg egy adminisztrációs oldalt, üzenetet kap, például: „A felhasználók kezeléséhez adminisztrátori hozzáférés szükséges.”

## Ki mit tehet

Az alkalmazás háromféle személyt különböztet meg:

- A **felhasználók** olvashatják az alapvető területeket, és kezelhetik a saját fiókjukat.
- Az **adminisztrátorok** kezelik a felhasználókat és a szerepköröket.
- A **fejlesztők** emellett láthatják a törölt felhasználókat, és használhatják a nyers adatbázist és a konfigurációs területeket. A fejlesztő rendelkezik a `RawDatabase.Manage` engedéllyel.

Az adminisztrátorok soha nem léphetik túl a saját hozzáférésüket. Nem adhatnak szerepkörnek vagy felhasználónak olyan engedélyt, amellyel maguk nem rendelkeznek, nem módosíthatják más fejlesztők adatait, és nem módosíthatják a saját hozzáférésüket. Csak a fejlesztők mentesülnek az „ne adj olyat, amivel nem rendelkezel” szabály alól.
