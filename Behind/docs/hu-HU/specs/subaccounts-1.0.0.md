# Alfiókok API 1.0.0

[JSON Schema](../../schemas/subaccounts-1.0.0.schema.json)

Minden végponthoz hitelesített munkamenet szükséges. Az azonosítók felhasználókat jelölnek. A kérés és válasz törzsének szerkezetét a séma megnevezett definíciói írják le.

| Metódus és útvonal | Bemenet | Sikeres válasz |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 a szokásos bejelentkezési tokenekkel |

Az elsődleges fiók létrehozhat felhasználókat, módosíthatja a megjelenítési nevet, a születési dátumot, a gyermekfiók-jelzőt és a PIN-kódot. A születési dátum ÉÉÉÉ-HH-NN formátumú, és nem lehet jövőbeli. Frissítéskor a kihagyott PIN-kód megmarad. Törléséhez a `clearPin` legyen igaz, új PIN-kód megadása nélkül. A PIN-kód számjegyekből álló karakterlánc, a kezdő nullák is számítanak.

Váltással indított munkamenetből másik jogosult alfiókra is lehet váltani. Önálló bejelentkezéssel az alfiók nem érheti el a többi alfiókot. A listában az e-mail-címet és a születési dátumot csak az elsődleges fiók látja. A sikeres váltás érvényteleníti a korábbi munkamenetet. Az elsődleges fiókba a szokásos módon kell visszajelentkezni.

A hibás bemenet 400, a tiltott váltás vagy hibás PIN-kód 403, az elérhetetlen cél 404, a foglalt e-mail-cím 409, a túl gyakori váltás 429 választ eredményez. Az 1.0.0 verzió új végpontokat vezet be. Telepítés előtt futtassa a Core adatbázis-migrációt. A meglévő felhasználókat és munkameneteket nem kell átalakítani.
