# Ütemezések kezelése

Az ütemezések előre meghatározott műveleteket futtatnak ötmezős, UTC szerint értelmezett cron kifejezéssel. Nyissa meg az **Adminisztráció > Ütemezések** oldalt. A megtekintéshez `Schedule.Read`, létrehozáshoz és szerkesztéshez `Schedule.Write`, törléshez `Schedule.Delete`, az azonnali futtatáshoz pedig `Schedule.Manage` engedély szükséges. A `RawDatabase.Manage` engedéllyel rendelkező fejlesztők a törölt ütemezéseket és futásokat is megjeleníthetik, visszaállíthatják vagy véglegesen törölhetik.

## Ütemezés létrehozása

Adjon meg egy nevet, válassza ki, hogy az ütemezés engedélyezett legyen-e, majd válasszon műveletet. A műveleteket a Core és a telepített modulok biztosítják: a Core a **Munkamenetek takarítása** (`core.session-cleanup`) műveletet adja. A művelet paraméterei és választható értékei dinamikusan jelennek meg. A sablonparaméterek kattintható `{{helyőrző}}` elemeket mutathatnak; ezek a kurzor pozíciójára szúrhatók be.

A cron kifejezés öt, szóközzel elválasztott mezőből áll:

| Mező | Értékek |
|---|---|
| Perc | 0–59 |
| Óra | 0–23 |
| A hónap napja | 1–31 |
| Hónap | 1–12 |
| A hét napja | 0–6 (a vasárnap 0) |

A `*` minden értéket jelent, a `*/N` minden N-edik értéket, az `a,b,c` felsorolt értékeket, az `a-b` pedig tartományt jelöl. Például a `*/15 * * * *` 15 percenként fut; a `0 2 * * *` naponta 02:00 UTC-kor; a `30 8 * * 1-5` pedig hétköznapokon 08:30 UTC-kor. A mezőkalkulátor ugyanazt a nyers kifejezést szerkeszti. A percenkénti, óránkénti, napi, heti és havi gyakorisághoz előbeállítások tartoznak. A kalkulátor egyszerű módjaiba nem illő kifejezések nyers cron szövegként szerkeszthetők tovább. A kifejezés alatt megjelenik az ellenőrzés eredménye és a következő öt időpont.

## Munkamenetek takarítása

A **Munkamenetek takarítása** művelet véglegesen törli azokat a munkameneteket, amelyeket az ütemezett futási időponthoz képest több mint **Érvénytelenített munkamenet kora (nap)** nappal ezelőtt érvénytelenítettek (kijelentkezés, rendszergazdai lezárás vagy csere miatt). A kor egész számú nap; üresen hagyva az alapértelmezett 30 nap érvényes. A felső korlát 3650 nap, hacsak az üzemeltető nem módosítja a `Backend:Scheduler:MaxSessionCleanupAgeDays` beállítást. Az aktív munkamenetek és az érvénytelenítési időpont nélküli érvénytelen munkamenetek soha nem törlődnek. A futás összegzése megadja a törölt munkamenetek számát; a művelet nem ad eredményhivatkozást. Például a `0 3 * * *` cron kifejezés minden nap 03:00 UTC-kor futtatja a takarítást.

## Résztvevők és e-mail

A Résztvevők lapon szerepköröket adhat hozzá, felhasználók között kereshet, vagy egyedi e-mail-címeket vehet fel. Az ismétlődő bejegyzéseket a rendszer figyelmen kívül hagyja. Válassza ki, mikor küldjön e-mailt a művelet eredményéről: **Soha**, **Mindig**, **Siker esetén** vagy **Hiba esetén**. Az eredményről szóló e-mail a létrehozott jelentés hivatkozását is tartalmazhatja. Üres résztvevőlista esetén nincsenek kiválasztott címzettek.

## Futtatás és előzmények

A **Futtatás most** gomb sorba állít egy azonnali futást; a már futó ütemezés nem indítható el még egyszer. Az ütemezés melletti **Futások** hivatkozással az adott ütemezés előzményei nyithatók meg, a **Futási előzmények** pedig az összes ütemezés futásait mutatja. Az oldal állapotot, összefoglalót, kezdési és befejezési időt, e-mail-kézbesítési állapotot és címzettszámot jelenít meg. Ha elérhető hivatkozás, az **Eredmény megnyitása** nyitja meg a művelet eredményét. Az ütemezések és futások törlése átmeneti; a fejlesztők visszaállíthatják vagy véglegesen törölhetik a törölt rekordokat.
