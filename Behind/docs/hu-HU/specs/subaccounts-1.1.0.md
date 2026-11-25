# Alfiókok API 1.1.0

[JSON Schema](../../schemas/subaccounts-1.1.0.schema.json)

Minden végponthoz hitelesített munkamenet szükséges. Az azonosítók felhasználókat jelölnek. A kérés és válasz törzsének szerkezetét a séma megnevezett definíciói írják le.

| Metódus és útvonal | Bemenet | Sikeres válasz |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 a szokásos bejelentkezési tokenekkel |

Az elsődleges fiók létrehozhat felhasználókat, módosíthatja a megjelenítési nevet, a születési dátumot, a gyermekfiók-jelzőt és a PIN-kódot. A születési dátum ÉÉÉÉ-HH-NN formátumú, és nem lehet jövőbeli. Frissítéskor a kihagyott PIN-kód megmarad. Törléséhez a `clearPin` legyen igaz, új PIN-kód megadása nélkül. A PIN-kód számjegyekből álló karakterlánc, a kezdő nullák is számítanak.

A létrehozáskor megadható `primaryPin` az elsődleges fiók első váltási PIN-kódját állítja be. A beállítás kihagyható. A `pin` az alfiókhoz tartozik. Családi munkamenetből az aktív elsődleges fiókba annak PIN-kódjával lehet visszaváltani. Enélkül teljes bejelentkezés szükséges. Közvetlenül bejelentkezett alfiók nem válthat fiókot. A válaszban a `primaryHasPin` ilyen esetben hamis. Az elsődleges fiók csak elérhető PIN-kódos célként szerepel az `accounts` listában. A születési dátumot és az e-mail-címet csak az elsődleges fiók látja. A sikeres váltás érvényteleníti a korábbi munkamenetet.

Hibás bemenet esetén a válasz 400, tiltott váltás, hiányzó vagy hibás kötelező PIN-kód esetén 403, elérhetetlen cél esetén 404. Foglalt e-mail-cím vagy már beállított elsődleges PIN-kód felülírása esetén a válasz 409, túl gyakori váltáskor 429. Az 1.0-s kliensek elhagyhatják az új mezőt. Telepítés előtt futtassa a Core SQLite- vagy PostgreSQL-migrációját. A migráció a meglévő kivonatokat változtatás nélkül a tulajdonosukhoz rendeli. Az elsődleges PIN-kód beállítása választható.

Visszaállításkor az alfiókok kivonatai visszakerülnek a kapcsolatokhoz. Az 1.0-s séma nem tárol elsődleges PIN-kódokat, így ezeket visszaállítás és ismételt frissítés után újra be kell állítani.
