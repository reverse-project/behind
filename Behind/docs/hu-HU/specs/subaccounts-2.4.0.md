# Alfiókok API 2.4.0

[JSON Schema](../../schemas/subaccounts-2.4.0.schema.json)

Minden végponthoz hitelesített munkamenet szükséges. Az azonosítók felhasználókat jelölnek. A kérés és válasz törzsének szerkezetét a séma megnevezett definíciói írják le.

| Metódus és útvonal | Bemenet | Sikeres válasz |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| PUT /profile/subaccounts/{userId}/pin | `pinMutation` (elsődleges fiók), `selfPinMutation` (kapcsolt alfiók) | 204 |
| POST /profile/subaccounts/{userId}/pin-proof | `pinProofRequest` | 204 |
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

Hibás bemenet esetén a válasz 400, tiltott váltás, hiányzó vagy hibás kötelező PIN-kód esetén 403, elérhetetlen cél esetén 404. Foglalt e-mail-cím, már beállított elsődleges PIN-kód felülírása vagy az első PIN-kód egyidejű beállításakor fellépő ütközés esetén a válasz 409, túl gyakori váltáskor 429. Telepítés előtt futtassa a Core SQLite- vagy PostgreSQL-migrációját. A migráció a meglévő kivonatokat változtatás nélkül a tulajdonosukhoz rendeli. Az elsődleges PIN-kód beállítása választható.

Visszaállításkor az alfiókok kivonatai visszakerülnek a kapcsolatokhoz. Az 1.0-s séma nem tárol elsődleges PIN-kódokat, így ezeket visszaállítás és ismételt frissítés után újra be kell állítani.


## Meglévő felhasználó csatolása

Mindkét biztonsági fiókbeállítást engedélyezni kell. A letiltott útvonalak 404-es választ adnak. A család válasza a munkamenethez tartozó `linkUnlinkEnabled` mezőt is tartalmazza. A függő meghívás nem ad hozzáférést, és amíg bármelyik kapcsoló ki van kapcsolva, nem fogadható el. Újraengedélyezéskor a címzett ismét megtekintheti.

A közvetlenül bejelentkezett elsődleges felhasználó megjelenítési név alapján kereshet. A jelölt aktív, van e-mail-címe, és nincs sem szülője, sem alfiókja. A találatok és meghívások nyilvános felhasználói címkéket mutatnak. Egyszerre egy bejövő meghívás lehet függőben. A címzett megtekinti a javasolt elsődleges fiókot, majd a meghívás azonosítójával jóváhagyja vagy elutasítja a kérést. Jóváhagyáskor a rendszer mindkét fiók aktuális kapcsolatait ellenőrzi. Elavult vagy versengő meghívások nem írhatnak felül kapcsolatot.

A leválasztás eltávolítja a kapcsolatot és érvényteleníti a váltással létrejött családi munkameneteket. A profiladatok, a PIN-kódok és a közvetlen bejelentkezések megmaradnak. E-mail-cím nélküli alfióknál leválasztás előtt váltson át az alfiókra és adjon meg bejelentkezési címet a profilban. Ha a rendszer e-mailes megerősítést kér, erősítse meg a címet. A leválaszthatóságot a válasz `canRelease` mezője jelzi. A meghívások engedélyezése előtt futtassa az új Core migrációt. Meglévő felhasználókhoz kezdetben nincs meghívás. Visszaállításkor a függő meghívások elvesznek.

A csatolt alfiókokkal rendelkező elsődleges fiók végleges törlésekor a `POST /users/{id}/hard/delete` 409-es választ ad, `{"reason":"linkedSubaccounts"}` törzzsel. A kapcsolatok és a csatolt felhasználók megmaradnak.

## Korábbi sémaváltozások

A család válasza a tárolt megjelenítési neveket adja vissza. Meglévő fiókoknál a név lehet üres, vagy állhat csak szóközökből. A létrehozási és módosítási kérésekben továbbra is kötelező nem üres, legalább egy nem szóköz karakterből álló nevet megadni. Az 1.2.1-es séma követi a kiszolgáló PIN-kód-törlési ellenőrzését. A sémát frissítő kliensek fogadják el a tárolt megjelenítési neveket a válaszokban, és kezeljék a 409-es választ, amikor két frissítés egyszerre próbálja beállítani az első PIN-kódot. Az 1.2.0-s verzióról való frissítéshez nincs szükség további adatmigrációra.

## PIN-kód kezelése a 2.0.0 verzióban

Az elsődleges felhasználó a saját vagy egy kapcsolt fiók PIN-kódját a `PUT /profile/subaccounts/{userId}/pin` végponton állíthatja be, módosíthatja vagy törölheti (`pinMutation`, siker esetén 204). Az alfiók szerkesztésekor ugyanez az ellenőrzés érvényes. Létrehozáskor bármelyik PIN megadása igazolást igényel. A PIN-t nem érintő műveletekhez nincs szükség igazolásra.

A `proof` egyetlen hitelesítő adatot tartalmaz: a célfiók jelenlegi PIN-kódját (`currentPin`), a bejelentkezett elsődleges felhasználó jelszavát (`password`) vagy az egyszer használható `magicToken` kódot. Létrehozáskor a cél az elsődleges fiók. A `POST /profile/subaccounts/{userId}/pin-proof` végpontnak küldött `operation` értéke `change`, `remove` vagy `create` lehet (204). Az e-mailben kapott hivatkozás kódját másolja az eredeti űrlapra. A kód csak a kérelmezőre, célfiókra és műveletre érvényes a beállított lejáratig. Hibás vagy hiányzó igazolás esetén a válasz 403. A jelszavas és e-mailes módnak engedélyezve kell lennie a használatához.

A `POST /users/{id}/pin/unset` végpont (204) ellenőrzi a User.Manage jogosultságot és a célfiókra vonatkozó moderálási szabályokat. A felhasználók listájában a `permissions.canUnsetPin` jelzi, ha a PIN törölhető.

Az 1.2.1 verzióról frissítő klienseknek PIN-módosításkor igazolást kell küldeniük. A tárolt PIN-kódok és kapcsolatok nem igényelnek adatmigrációt.

## Igazolás az elsődleges fiók PIN-kódjával a 2.1.0 verzióban

A bejelentkezett elsődleges felhasználó kapcsolt alfiók PIN-kódjának beállításakor, módosításakor vagy törlésekor egyetlen hitelesítő adatként a `proof.primaryPin` mezőben a saját PIN-kódját is megadhatja. Ez a `PUT /profile/subaccounts/{userId}` és a `PUT /profile/subaccounts/{userId}/pin` végponton egyaránt érvényes. Saját PIN-kódjának kezelésekor az elsődleges felhasználó továbbra is a `proof.currentPin` mezőt használja. A fiók létrehozásakor a 2.0.0 verzió igazolási lehetőségei érvényesek. A létrehozási kérés legfelső szintű `primaryPin` mezője továbbra is az elsődleges fiók első PIN-kódját állítja be.

Az új hitelesítő adat megadása választható. A meglévő kliensek továbbra is a korábbi igazolási mezőket küldhetik. A 2.0.0 verzióról történő frissítéshez nincs szükség adatmigrációra.

## A kapcsolt alfiók PIN-kódjának állapota

A 2.2.0-s verzióban bevezetett `currentHasPin` logikai érték jelzi, hogy a bejelentkezett fióknak van-e váltási PIN-kódja. A kapcsolt alfiók közvetlen bejelentkezés és fiókváltás után is a saját PIN-kódjának állapotát látja. Ha ez a mező hiányzik, a kliens rejtse el a saját PIN-kód kezelésére szolgáló vezérlőket.

## A kapcsolt alfiók saját PIN-kódjának kezelése a 2.3.0 verzióban

A bejelentkezett, aktív kapcsolt alfiók a `PUT /profile/subaccounts/{userId}/pin` végponton a saját felhasználói azonosítóját és a `selfPinMutation` kérésformátumot használhatja. A beállításhoz küldje el a `{"pin":"0042"}` kérést. A `proof` mező elhagyható, vagy értéke null lehet. A kiszolgáló csak akkor fogadja el a beállítást, ha nincs tárolt PIN-kód, így a korábbi kód törlése után is beállítható új kód. A tárolt PIN-kód módosításához küldje el a `{"pin":"0042","proof":{"currentPin":"0017"}}` kérést, törléséhez pedig a `{"clearPin":true,"proof":{"currentPin":"0017"}}` kérést. Siker esetén mindhárom művelet 204-es választ ad.

Másik felhasználót célzó kérés esetén a válasz 403. Módosításhoz és törléshez egyetlen hitelesítő adatként a helyes jelenlegi PIN-kódot kell megadni. Ha már létezik PIN-kód, az igazolás nélküli beállítás 403-as választ ad. Egyidejű írási ütközéskor a válasz 409, és a sikeres kérés által mentett PIN-kód megmarad. Elutasított beállítás után kérje le újra a család adatait az aktuális vezérlők megjelenítéséhez. A PIN-kód beállítására és cseréjére ugyanaz a beállított hosszkorlát vonatkozik.

A 2.2.0-s kliensek kérései továbbra is működnek a 2.3.0-s kiszolgálókon. Az első PIN-kód beállítását csak a 2.3.0-s verziót támogató kiszolgálók esetén engedélyezze. A család válaszformátuma változatlan, és nincs szükség adatmigrációra.

## Tájékoztatás a közvetlen bejelentkezésről a 2.4.0 verzióban

A család válasza a `linkUnlinkRequiresDirectSignIn` logikai mezővel bővül. Értéke csak akkor igaz, ha az összekapcsolás engedélyezett, és az aktuális munkamenetben visszaváltottak az elsődleges fiókra. A kliens ilyenkor közvetlen bejelentkezésre kérheti a felhasználót. Az összekapcsolási és leválasztási vezérlők megjelenítéséhez továbbra is a `linkUnlinkEnabled` mezőt használd.

A meglévő kliensek továbbra is olvashatják a választ és küldhetik ugyanazokat a kéréseket. Régebbi kiszolgálóknál a hiányzó `linkUnlinkRequiresDirectSignIn` értékét tekintsd hamisnak. Adatmigráció nem szükséges.
