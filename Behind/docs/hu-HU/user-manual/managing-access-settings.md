# Hozzáférési beállítások

A **Hozzáférési beállítások** oldal azt szabályozza, hogy ki láthatja a weboldalt, és ki jelentkezhet be. Két egymástól független eszközt tartalmaz:

- a **korlátozó oldal**: a weboldal tartalmát csak a kiválasztott szerepkörrel rendelkező felhasználók látják, mindenki más egy Ön által írt üzenetet tartalmazó oldalt lát,
- a **regisztráció moderálása**: az új fiókokat jóvá kell hagyni, mielőtt bejelentkezhetnének.

Az oldalt az **Adminisztráció > Hozzáférési beállítások** alatt találja (`#/manage/access-settings`).

| Művelet | Szükséges |
|---------|-----------|
| A beállítások és a jóváhagyásra váró felhasználók megtekintése | az `Access.Read` engedély |
| A beállítások módosítása, felhasználók jóváhagyása és elutasítása | az `Access.Write` engedély (az elutasításhoz a `User.Delete` engedély is kell) |
| A beállítások visszaállítása az alapértelmezettre | az `Access.Delete` engedély |
| A visszaállított beállítások megtekintése, helyreállítása és végleges törlése | a `RawDatabase.Manage` engedély (fejlesztőknek) |

Az adminisztrátorok alapértelmezés szerint rendelkeznek az első három engedéllyel. A felhasználók nem.

## A korlátozó oldal

1. Kapcsolja be ezt a beállítást: **A korlátozó oldal megjelenítése a szükséges szerepkör nélküli látogatóknak**.
2. Válassza ki a **Szükséges szerepkört**. Válassza a **Users** szerepkört, ha minden regisztrált felhasználót be szeretne engedni; válasszon másik szerepkört, például egy csak a tagjainak adott szerepkört, ha a weboldalt csak nekik szeretné fenntartani.
3. Igény szerint írjon be egy **Címet**, a korlátozó oldal címsorát. Ha üres, a címsor **Korlátozott weboldal** lesz.
4. Írja meg az **Üzenetet**, amelyet a látogatók látnak, a formázott szövegszerkesztőben (címsorok, félkövér szöveg, listák, hivatkozások, képek és így tovább). A kép feltöltéséhez használja a **Kép** gombot, vagy húzzon egy képet a szerkesztőbe; a képeket a hozzáférési beállításokkal együtt tároljuk, és a korlátozott látogatók számára is láthatók. Ha üres, egy alapértelmezett üzenet jelenik meg.
5. Nyomja meg a **Mentés** gombot.

Amíg a korlátozás be van kapcsolva:

- a szerepkör nélküli látogatók (a nem bejelentkezett látogatókat is beleértve) csak a korlátozó oldalt látják az Ön üzenetével, valamint a **Bejelentkezés** és a **Regisztráció** gombbal. A weboldal tartalmát, például az oldalakat, híreket, cikkeket, galériákat, letöltéseket és menüket egyáltalán nem küldjük el nekik,
- a bejelentkezési, regisztrációs, varázslink- és e-mail-megerősítési oldalak továbbra is működnek, így az emberek be tudnak jutni,
- a **Hozzáférési beállítások kezelése** engedéllyel (`Access.Manage`, alapértelmezés szerint az adminisztrátorok) rendelkező felhasználók mindig látják a weboldalt, így a korlátozás soha nem zárhatja ki az adminisztrátorokat,
- a szerepkör nélküli bejelentkezett felhasználó a korlátozó oldalt látja, azzal a megjegyzéssel, hogy a fiókja nem tekintheti meg a weboldalt.

## A regisztráció moderálása

Kapcsolja be ezt a beállítást: **Csak jóváhagyott felhasználók jelentkezhetnek be**, és nyomja meg a **Mentés** gombot.

- Az új fiók a szokásos módon létrejön, de jóváhagyásra vár. Amikor a tulajdonosa bejelentkezik (jelszóval vagy varázslinkkel), az alkalmazás azt válaszolja, hogy a fiók jóváhagyásra vár. A regisztrációs oldal is ezt közli.
- A várakozó fiókok a **Jóváhagyásra váró felhasználók** alatt jelennek meg listában, egy **Keresés** mezővel. A **Jóváhagyás** gomb lehetővé teszi, hogy a felhasználó ezentúl bejelentkezhessen. Az **Elutasítás** törli a fiókot (egy fejlesztő visszaállíthatja a [Felhasználók](./managing-users.md) oldalon).
- A meglévő felhasználókat ez nem érinti, és a beállítás későbbi kikapcsolása nem hagyja jóvá a várakozó fiókokat: ezek a listán maradnak, amíg Ön jóvá nem hagyja vagy el nem utasítja őket.

## Visszaállítás és helyreállítás

- A **Visszaállítás az alapértelmezettre** átmenetileg törli a mentett beállításokat: a korlátozó oldal és a moderálás kikapcsol. A már jóváhagyásra váró fiókok továbbra is várakoznak.
- A fejlesztők egy **Törölt hozzáférési beállítások megjelenítése** kapcsolót látnak. A törölt beállításoknál a **Visszaállítás** és a **Végleges törlés** gomb jelenik meg.

Minden mentés a beállítások új verzióját hozza létre, és minden módosítás, jóváhagyás és elutasítás rögzítésre kerül az [auditnaplóban](./audit-log.md).
