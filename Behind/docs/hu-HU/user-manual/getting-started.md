# Első lépések

## A navigációs sáv

| Elem | Mikor látható | Hová vezet |
|------|---------------|------------|
| Behind / Kezdőlap | mindig | az üdvözlő oldalra |
| Szégyenfal | mindig | [a szégyenfalra](./wall-of-shame.md) |
| Bejelentkezés, Regisztráció | kijelentkezett állapotban | a bejelentkezési és a regisztrációs oldalra |
| Az Ön neve (menü) | bejelentkezett állapotban | Profil, Munkamenet, Munkamenetek, Adminisztráció (csak ha használhatja) és Kijelentkezés |

A nevéhez állandó rövid fiókazonosító tartozik. Ha nincs megjelenítendő neve, `user#azonosító` jelenik meg, sohasem az e-mail-címe.

## Fiók létrehozása

1. Válassza a **Regisztráció** lehetőséget.
2. Adja meg az **e-mail-címét** és egy **jelszót**.
3. Igény szerint adjon meg egy **megjelenítendő nevet**. Állandó fiókazonosító egészíti ki; üres névnél `user#azonosító` jelenik meg.
4. Írja be a captcha képen látható karaktereket. Ha nehezen olvashatók, válassza az **Új kép** gombot.
5. Válassza a **Fiók létrehozása** gombot.

Ha az e-mail-cím már használatban van, erről tájékoztatjuk. Ha az adminisztrátorok bekapcsolták a [regisztráció moderálását](./managing-access-settings.md), az új fiókot egy adminisztrátornak jóvá kell hagynia, mielőtt bejelentkezhetne. A kiszolgáló adminisztrátora a `backend.json` fájlban elrejtheti a felső menü **Regisztráció** és **Bejelentkezés** gombjait; ez csak a gombokat rejti el, maguk az oldalak a címük megnyitásakor továbbra is működnek. A fiók létrehozása után jelentkezzen be.

Ha a telepítésen ki nem kapcsolták az e-mail-megerősítést, egy e-mailt is kap egy hivatkozással a címe megerősítéséhez. Kövesse a hivatkozást, hogy az e-mail-címe megerősítettnek minősüljön. Ha a megerősítés ki van kapcsolva, az e-mail-címe a regisztráció pillanatától megerősítettnek számít. Ha a levél nem érkezett meg, vagy a hivatkozás lejárt, adja meg az e-mail-címét a bejelentkezési oldalon, és válassza a **Megerősítő e-mail újraküldése** gombot. A válasz ugyanaz, akár létezik a fiók, akár nem, és tíz percenként csak néhányszor kérheti.

Minden új fiók megkapja a **Users** szerepkört, amely lehetővé teszi az alkalmazás alapvető területeinek olvasását.

## Bejelentkezés

### Jelszóval

1. Válassza a **Bejelentkezés** lehetőséget.
2. Adja meg az e-mail-címét és a jelszavát.
3. Írja be a captcha képen látható karaktereket.
4. Válassza a **Bejelentkezés** gombot.

Ugyanez a captcha védi a bejelentkezési oldal többi gombját is (**Bejelentkezési hivatkozás küldése e-mailben**, **Elfelejtette a jelszavát?** és **Megerősítő e-mail újraküldése**). Egy kép csak egyszer használható, és 5 perc után lejár, ezért minden próbálkozás után új képet kap, és az új karaktereket kell beírnia. A betűk nem különböztetik meg a kis- és nagybetűt.

### E-mailes hivatkozással (varázslink)

1. A bejelentkezési oldalon adja meg az e-mail-címét.
2. Válassza a **Bejelentkezési hivatkozás küldése e-mailben** gombot. Az adatvédelem érdekében az alkalmazás mindig azt mondja, hogy a hivatkozás úton van, akár létezik a fiók, akár nem.
3. Nyissa meg az e-mailt, és kövesse a hivatkozást. Bejelentkezik, és az alkalmazásba kerül.

A hivatkozás csak rövid ideig érvényes. Ha érvénytelen vagy lejárt, az oldal felajánlja egy új kérését. Ha a kérések túl gyorsan követik egymást, várakozásra kérjük.

### Kétlépcsős hitelesítés

Ha bekapcsolta a kétlépcsős hitelesítést (lásd [Kétlépcsős hitelesítés](./your-account.md#kétlépcsős-hitelesítés)), a jelszóval vagy e-mailes hivatkozással történő bejelentkezés egy második lépést kér, mielőtt bejelentkezne. Ez ugyanúgy működik jelszó, bejelentkezési hivatkozás és elfelejtett jelszó hivatkozás után.

- Adja meg a **hitelesítő alkalmazás** hatjegyű kódját, vagy
- válassza a **Hozzáférési kulcs használata** lehetőséget, és erősítse meg az eszközén, vagy
- válassza a **Helyreállító kód használata** lehetőséget, és adja meg az egyik helyreállító kódját (8 betű és szám). Minden helyreállító kód egyszer használható. Amikor az utolsót is felhasználta, új, 20 darabos készletet kap.

Ha a telepítés megköveteli a kétlépcsős hitelesítést, és Ön még nem állította be, a jelszava vagy a hivatkozása után végigvezetjük a beállításon: válasszon egy hitelesítő alkalmazást (olvassa be a QR-kódot, és adja meg az alkalmazás által mutatott kódot) vagy egy hozzáférési kulcsot. Ezután megkapja a helyreállító kódjait. Mentse el őket, csak egyszer jelennek meg.

A második lépést 5 percen belül be kell fejezni, és tíz percenként csak korlátozott számú alkalommal próbálkozhat.

### Elfelejtett jelszó

1. A bejelentkezési oldalon adja meg az e-mail-címét, és válassza az **Elfelejtette a jelszavát?** gombot. Ugyanúgy kap egy hivatkozást, mint a varázslink esetén.
2. Kövesse a hivatkozást. Bejelentkezteti, és közvetlenül a **Profil** oldal **Új jelszó** mezőjéhez viszi.
3. Adjon meg egy új jelszót, és válassza a **Mentés** gombot.

### Letiltott fiókok

Ha a fiókja le van tiltva, a bejelentkezés sikertelen, és az adminisztrátor által megadott indoklás megjelenik. Lásd a [Szégyenfal](./wall-of-shame.md) oldalt.

## Kijelentkezés

- A **Kijelentkezés** (a navigációs menüben vagy az üdvözlő oldalon) csak az aktuális munkamenetet fejezi be.
- A [Munkamenetek oldalon](./your-account.md#az-összes-munkamenete) a **Kijelentkezés az összes munkamenetből** a fiókja minden munkamenetét befejezi, a többi böngészőben és eszközön lévőket is.

Ha a munkamenete lejár, vagy egy adminisztrátor kijelentkezteti, újra be kell jelentkeznie.
