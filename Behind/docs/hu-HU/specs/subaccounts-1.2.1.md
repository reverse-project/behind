# Alfiókok API 1.2.1

[JSON Schema](../../schemas/subaccounts-1.2.1.schema.json)

Minden végponthoz hitelesített munkamenet szükséges. Az azonosítók felhasználókat jelölnek. A kérés és válasz törzsének szerkezetét a séma megnevezett definíciói írják le.

| Metódus és útvonal | Bemenet | Sikeres válasz |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 a szokásos bejelentkezési tokenekkel |
| GET /profile/subaccounts/candidates?query=…&page=1&pageSize=10 | — | 200 `candidateList` |
| POST /profile/subaccounts/{userId}/request | — | 204 |
| GET /profile/subaccounts/requests | — | 200 `invitationList` |
| POST /profile/subaccounts/requests/{requestId}/confirm | — | 204 |
| POST /profile/subaccounts/requests/{requestId}/decline | — | 204 |
| DELETE /profile/subaccounts/{userId} | — | 204 |

Az elsődleges fiók létrehozhat felhasználókat, módosíthatja a megjelenítési nevet, a születési dátumot, a gyermekfiók-jelzőt és a PIN-kódot. A születési dátum ÉÉÉÉ-HH-NN formátumú, és nem lehet jövőbeli. Frissítéskor a PIN-kód megmarad, ha a `pin` mező hiányzik, és a `clearPin` nem igaz. A PIN-kód törléséhez állítsa a `clearPin` mezőt igazra, és hagyja ki a `pin` mezőt, vagy adjon meg null értéket. Ha a `clearPin` igaz, és a `pin` karakterlánc, a válasz 400. A PIN-kód számjegyekből álló karakterlánc, a kezdő nullák is számítanak.

A megadott PIN-kód legfeljebb 128 karakter hosszú lehet. A rendszergazda a [`Security.Accounts.SwitchPinMaxLength`](./configuration-json-1.4.0.md) beállítással csökkentheti ezt a korlátot. A beállított korlátnál hosszabb PIN-kód 400-as hibát ad.

A létrehozáskor megadható `primaryPin` az elsődleges fiók első váltási PIN-kódját állítja be. A beállítás kihagyható. A `pin` az alfiókhoz tartozik. Családi munkamenetből az aktív elsődleges fiókba annak PIN-kódjával lehet visszaváltani. Enélkül teljes bejelentkezés szükséges. Közvetlenül bejelentkezett alfiók nem válthat fiókot. A válaszban a `primaryHasPin` ilyen esetben hamis. Az elsődleges fiók csak elérhető PIN-kódos célként szerepel az `accounts` listában. A születési dátumot és az e-mail-címet csak az elsődleges fiók látja. A sikeres váltás érvényteleníti a korábbi munkamenetet.

Hibás bemenet esetén a válasz 400, tiltott váltás, hiányzó vagy hibás kötelező PIN-kód esetén 403, elérhetetlen cél esetén 404. Foglalt e-mail-cím, már beállított elsődleges PIN-kód felülírása vagy az első PIN-kód egyidejű beállításakor fellépő ütközés esetén a válasz 409, túl gyakori váltáskor 429. Az 1.0-s kliensek elhagyhatják az új mezőt. Telepítés előtt futtassa a Core SQLite- vagy PostgreSQL-migrációját. A migráció a meglévő kivonatokat változtatás nélkül a tulajdonosukhoz rendeli. Az elsődleges PIN-kód beállítása választható.

Visszaállításkor az alfiókok kivonatai visszakerülnek a kapcsolatokhoz. Az 1.0-s séma nem tárol elsődleges PIN-kódokat, így ezeket visszaállítás és ismételt frissítés után újra be kell állítani.


## Meglévő felhasználó csatolása

Mindkét biztonsági fiókbeállítást engedélyezni kell. A letiltott útvonalak 404-es választ adnak. A család válasza a munkamenethez tartozó `linkUnlinkEnabled` mezőt is tartalmazza. A függő meghívás nem ad hozzáférést, és amíg bármelyik kapcsoló ki van kapcsolva, nem fogadható el. Újraengedélyezéskor a címzett ismét megtekintheti.

A közvetlenül bejelentkezett elsődleges felhasználó megjelenítési név alapján kereshet. A jelölt aktív, van e-mail-címe, és nincs sem szülője, sem alfiókja. A találatok és meghívások nyilvános felhasználói címkéket mutatnak. Egyszerre egy bejövő meghívás lehet függőben. A címzett megtekinti a javasolt elsődleges fiókot, majd a meghívás azonosítójával jóváhagyja vagy elutasítja a kérést. Jóváhagyáskor a rendszer mindkét fiók aktuális kapcsolatait ellenőrzi. Elavult vagy versengő meghívások nem írhatnak felül kapcsolatot.

A leválasztás eltávolítja a kapcsolatot és érvényteleníti a váltással létrejött családi munkameneteket. A profiladatok, a PIN-kódok és a közvetlen bejelentkezések megmaradnak. E-mail-cím nélküli alfióknál leválasztás előtt váltson át az alfiókra és adjon meg bejelentkezési címet a profilban. Ha a rendszer e-mailes megerősítést kér, erősítse meg a címet. A leválaszthatóságot a válasz `canRelease` mezője jelzi. A meghívások engedélyezése előtt futtassa az új Core migrációt. Meglévő felhasználókhoz kezdetben nincs meghívás. Visszaállításkor a függő meghívások elvesznek.

A csatolt alfiókokkal rendelkező elsődleges fiók végleges törlésekor a `POST /users/{id}/hard/delete` 409-es választ ad, `{"reason":"linkedSubaccounts"}` törzzsel. A kapcsolatok és a csatolt felhasználók megmaradnak.

## Kompatibilitás az 1.2.0-s verzióval

A család válasza a tárolt megjelenítési neveket adja vissza. Meglévő fiókoknál a név lehet üres, vagy állhat csak szóközökből. A létrehozási és módosítási kérésekben továbbra is kötelező nem üres, legalább egy nem szóköz karakterből álló nevet megadni. Az 1.2.1-es séma követi a kiszolgáló PIN-kód-törlési ellenőrzését. A sémát frissítő kliensek fogadják el a tárolt megjelenítési neveket a válaszokban, és kezeljék a 409-es választ, amikor két frissítés egyszerre próbálja beállítani az első PIN-kódot. Az 1.2.0-s verzióról való frissítéshez nincs szükség további adatmigrációra.
