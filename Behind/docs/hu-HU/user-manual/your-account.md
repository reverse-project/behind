# Az Ön fiókja

Ezeket az oldalakat a navigációs sávban lévő nevére kattintva nyithatja meg.

## Profil

A **Profil** segítségével módosíthatja a megjelenítendő nevét és a jelszavát. A jelszómódosítás nem érhető el, amíg egy másik felhasználót személyesít meg.

1. Írjon be egy új nevet (legfeljebb 500 karakter). Hagyja üresen, hogy az elsődleges e-mail-címe jelenjen meg.
2. Válassza a **Mentés** gombot.

A „Megjelenik mint: ..." sor megmondja, hogyan látnak Önt mások. Ez a nyilvános fórumüzenetekre is érvényes: üres név esetén az elsődleges e-mail-cím nyilvánossá válik.

### Profilkép

A **Profilkép** alatt válasszon legfeljebb 2 MiB méretű PNG, JPEG, GIF vagy WebP képet; a mentés azonnali. Az **Eltávolítás** megszünteti a hivatkozást. A kép nyilvános, és a fórumüzenetek mellett jelenik meg. Lásd: [Fórum](./managing-forum.md).

### Nyelv

Válassza ki a **Nyelvet** az elérhető nyelvek listájából azok nevével (például **English (International)** vagy **Magyar**), és válassza a **Mentés** gombot. Az **Az oldal alapértelmezettje** választás az oldalon beállított nyelvet követi.

Bejelentkezve a választás a fiókjába mentődik, és minden eszközre elkíséri. Bejelentkezés nélkül csak ebben a böngészőben jegyzi meg a rendszer.

## Kétlépcsős hitelesítés

A **Profil > Kétlépcsős hitelesítés** második lépést ad a bejelentkezéshez, hogy egy ellopott jelszó vagy bejelentkezési hivatkozás ne legyen elég. Nem kötelező, kivéve, ha az adminisztrátor mindenkinek előírja. Nem érhető el, amíg egy másik felhasználót személyesít meg.

- **Hitelesítő alkalmazás**: válassza a **Beállítás** gombot, olvassa be a QR-kódot egy hitelesítő alkalmazással (vagy írja be a megjelenített kulcsot), majd írja be az alkalmazás által mutatott hatjegyű kódot, és válassza a **Megerősítés** gombot. A megerősítésig az alkalmazás nincs használatban. Az **Eltávolítás** kikapcsolja.
- **Hozzáférési kulcsok**: válassza a **Hozzáférési kulcs hozzáadása** gombot, szükség esetén nevezze el (például „Munkahelyi laptop"), és erősítse meg az eszközével (biztonsági kulcs, telefon, Windows Hello, Touch ID). Többet is hozzáadhat, és bármelyiket eltávolíthatja az **Eltávolítás** gombbal.
- **Helyreállító kódok**: amikor bekapcsolja az első módszert, 20 helyreállító kódot kap, egyenként 8 betűvel és számmal. **Csak egyszer** jelennek meg: másolja ki vagy töltse le őket, és tartsa biztonságos helyen. Ha elveszíti a hitelesítő alkalmazását vagy a hozzáférési kulcsát, a bejelentkezés második lépésénél írjon be helyette egy helyreállító kódot. Minden kód csak egyszer használható, és amikor az utolsót is felhasználta, új készletet kap. Az **Új helyreállító kódok létrehozása** bármikor lecseréli az összeset; a régiek érvényüket vesztik.

Ha az adminisztrátor előírja a kétlépcsős hitelesítést, nem távolíthatja el az utolsó módszerét. Az utolsó módszer eltávolítása kikapcsolja a kétlépcsős hitelesítést, és törli a helyreállító kódokat.

## Munkamenet

A **Munkamenet** megmutatja, kinek tekinti Önt az alkalmazás éppen most, és meddig marad bejelentkezve.

- **Felhasználó**: a neve, a felhasználóazonosítója, az e-mail-címei és a szerepkörei, az egyes szerepkörök által adott engedélyekkel. A szerepkörönként felsorolt engedélyeknél már érvényesítve vannak a szerepkör korlátozásai.
- **Részletek**: mikor kezdődött a munkamenet, az életkora, mikor jár le, mikor adták ki és mikor jár le a rövid élettartamú hozzáférési token, a távoli cím, és a böngésző (felhasználói ügynök), amelyből a munkamenetet elindították.

A hozzáférési token az alkalmazás használata közben automatikusan megújul. A **Frissítés** újratölti az információkat. Ha egy adminisztrátor módosította a szerepköreit vagy a hozzáférését, frissítsen a változás érvénybe lépéséhez.

Ha még nem rendelkezik minden emelt szintű szerepkörrel, ez az oldal az [emelési űrlapot](./elevation.md) is megjeleníti.

## Az összes munkamenete

A **Munkamenetek** felsorolja minden helyet, ahol be van jelentkezve.

- **Aktuális munkamenetek**: IP-cím, böngésző, mikor kezdődött, mikor volt utoljára aktív, és mikor jár le. Az aktuális munkamenete **Ez a munkamenet** jelöléssel szerepel.
- **Régi munkamenetek megjelenítése**: korábbi munkamenetek azzal, hogy mikor és miért értek véget („Kijelentkezett" vagy „Lejárt"). A hosszú listák lapozottak.
- A **Frissítés** újratölti a listákat.
- A **Kijelentkezés az összes munkamenetből** mindet befejezi, ezt is beleértve.

Ha olyan munkamenetet lát, amelyet nem ismer fel, jelentkezzen ki az összes munkamenetből, és módosítsa a jelszavát.
