# Alfiókok API 3.3.0

[JSON-séma](../../schemas/subaccounts-3.3.0.schema.json)

Minden végponthoz hitelesített, nem megszemélyesített munkamenet szükséges. Az azonosítók felhasználókat jelölnek. A kérések és válaszok szerkezetét a séma megnevezett definíciói írják le.

| Metódus és útvonal | Bemenet | Sikeres válasz |
| --- | --- | --- |
| GET /profile/subaccounts | — | 200 `family` |
| POST /profile/subaccounts | `create` | 201 `created` |
| PUT /profile/subaccounts/{userId} | `update` | 204 |
| PUT /profile/subaccounts/{userId}/pin | `pinMutation` vagy `selfPinMutation` | 204 |
| POST /profile/subaccounts/{userId}/pin-proof | `pinProofRequest` | 204 |
| POST /profile/subaccounts/{userId}/switch | `switch` | 200 bejelentkezési tokenek |
| GET /profile/subaccounts/candidates?query=…&page=1&pageSize=10 | — | 200 `candidateList` |
| POST /profile/subaccounts/{userId}/request | — | 204 |
| GET /profile/subaccounts/requests | — | 200 `invitationList` |
| POST /profile/subaccounts/requests/{requestId}/confirm | — | 204 |
| POST /profile/subaccounts/requests/{requestId}/decline | — | 204 |
| DELETE /profile/subaccounts/{userId} | — | 204 |

A `canManageChildren` jelzi, hogy a munkamenet kezelheti-e a család gyermekfiókjait. Az elsődleges fiók a kapcsolt profilokat, köztük a felnőtteket is kezelheti. Az átváltott felnőtt létrehozhatja, szerkesztheti, meghívhatja és leválaszthatja az engedélyezett család gyermekfiókjait, de más felnőttet nem hozhat létre és nem szerkeszthet, gyermeket nem alakíthat felnőtté, és nem kezelheti az elsődleges fiók PIN-kódját. Kapcsolt felnőttként közvetlenül bejelentkezve nem jár jog az elsődleges fiók gyermekeinek kezelésére. A rendszer minden kérésnél újra ellenőrzi az aktív kapcsolatokat. Megszemélyesítéskor a műveletek nem érhetők el.

Az `accounts` a kezelhető profilok adatait, köztük a születési dátumot és az e-mail-címet tartalmazza; átváltott felnőttnél csak a gyermekeket. A `switchAccounts` nem tartalmaz privát adatokat, az `isChild` alapján csoportosítható. Az elsődleges fiók PIN nélkül válthat olyan kapcsolt profilra, amelynek nincs PIN-kódja; az átváltott felnőtt ugyanígy válthat engedélyezett gyermekfiókra. Tárolt cél-PIN esetén a helyes kód mindig kötelező. Más nem elsődleges fiókból PIN szükséges. A felnőtt érvényes PIN-kódja gyermekből váltva is engedélyezi a család kezelését. A gyermekből elért felnőtt munkamenet más fiókkezelési műveletekre továbbra is korlátozott. A váltás érvényteleníti a korábbi munkamenetet.

A `displayName` létrehozáskor és módosításkor kötelező; a tárolt válasznév lehet üres. A születési dátum ÉÉÉÉ-HH-NN formátumú, és nem lehet jövőbeli. A PIN számjegyekből álló karakterlánc, kezdő nullákkal. A `pin` elhagyása megőrzi a kódot; törléséhez a `clearPin` legyen igaz, a `pin` pedig hiányozzon vagy legyen null. Igaz `clearPin` mellett karakterlánc-PIN esetén a válasz 400.

PIN létrehozásához és módosításához egyetlen igazolás szükséges. Az elsődleges fiók használhatja a cél `currentPin` mezőjét, kapcsolt fióknál a saját `primaryPin` értékét, jelszavát vagy egyszer használható `magicToken` kódot. Átváltott felnőtt gyermek-PIN kezeléséhez a saját PIN-jét `primaryPin` mezőben, jelszavát vagy `magicToken` kódot kell használni; a gyermek PIN-je önmagában nem elegendő. Új gyermek PIN-jéhez a felnőtt saját PIN-jét `currentPin` mezőben, jelszót vagy `magicToken` kódot kell megadni, és a `create` igazolást a saját fiókra kell kérni. Átváltott felnőtt létrehozáskor nem állíthatja be a `primaryPin` mezőt. Kapcsolt felhasználó saját első PIN-jét igazolás nélkül állíthatja be, módosításhoz és törléshez saját jelenlegi PIN-je kell. Az e-mailes igazolás a kérelmezőhöz, célhoz és művelethez kötött, lejár, és a magic-link mód szükséges hozzá. A jelszavas igazoláshoz jelszavas mód szükséges.

Az összekapcsoláshoz mindkét fiókfunkciót engedélyezni kell. A `canBrowseAndRelease` jelzi a keresés és leválasztás jogát, a `linkUnlinkEnabled` a meghívás küldését, a `canRespondToInvitations` a bejövő meghívások kezelését. Az elsődleges fiók visszaváltás után kereshet és leválaszthat, de meghívást csak közvetlen bejelentkezéssel küldhet. Az átváltott felnőtt az elsődleges fiók nevében küldhet meghívást. Bejövő meghívást csak közvetlen bejelentkezéssel lehet megtekinteni, elfogadni vagy elutasítani. A jelölt aktív, van e-mail-címe, nincs saját profilja, és nincs összeférhetetlen kapcsolata. Másik felnőtthöz gyermekfiókként már kapcsolódhat. A meghívás nyilvános címkét mutat, a címzett hozzájárulását igényli, elfogadáskor a rendszer újra ellenőrzi a kapcsolatokat.

A leválasztáshoz használható bejelentkezési e-mail-cím kell, amelyet a beállítástól függően meg kell erősíteni. A `canRelease` jelzi a jogosultságot. A leválasztás az elsődleges kapcsolatot törli, a profiladatokat és PIN-kódokat megőrzi, és az érintett váltott munkameneteket, köztük az átváltott kezdeményezőét érvényteleníti. Sikeres leválasztás után a kliens törölje a kezdeményező váltott munkamenetét. A többi közvetlen munkamenet és független kapcsolat megmarad. Kapcsolt profilokkal rendelkező elsődleges fiók végleges törlése 409-es választ ad `{"reason":"linkedSubaccounts"}` törzzsel.

Hibás bemenetre 400, tiltott műveletre vagy hibás igazolásra 403, hiányzó célra 404, foglalt e-mailre, kapcsolati vagy PIN-ütközésre 409, gyakoriságkorlátra 429 a válasz.

A `Backend.Accounts.SubaccountSwitch` önállóan tiltja a váltási végpontot 404-es válasszal, miközben a család kezelése elérhető marad. A `GET /accounts/settings` közli a `subaccountSwitch` értéket. A PIN hosszát a pozitív 32 bites `Security.Accounts.SwitchPinMaxLength` korlátozza, alapértéke 128; a séma nem fejezheti ki a telepítésenként eltérő korlátot. Nem pozitív beállításnál a PIN-kérés 500-as hibát ad.

## Kompatibilitás és migráció

A 3.2.0 kötelező `canManageChildren` válaszmezőt vezet be és bővíti a fenti jogosultságokat, illetve PIN nélküli célokat. A régebbi kliensek a meglévő mezőket tovább használhatják, de nem jelenítik meg az új kezelési vezérlőket. Telepítés előtt futtassa a Core `AddFamilySessionManager` migrációját. Ez megőrzi a meglévő munkameneteket és kapcsolatokat, és a gyermekből indított további váltásokhoz tárolja az engedélyezett családot. A kiszolgáló minden kérésnél ellenőrzi az eredeti kapcsolatot és az aktív családot. A munkamenet családi jogosultságmezői nem módosíthatók a nyers adatbázis-szerkesztővel. Visszaállításkor ez a munkamenet-állapot törlődik, ezért előtte zárja le az érintett váltott munkameneteket. A korábbi specifikációk és sémák továbbra is elérhetők.

A 3.3.0 eltávolítja a séma rögzített PIN-hosszkorlátját, és önálló kapcsolót ad a váltáshoz. A meglévő konfigurációk alapértékei megőrzik a korábbi működést. A régi 128 karakteres korlátot alkalmazó klienseket frissíteni kell a hosszabb PIN-ekhez. Új adatbázis-migráció nem szükséges.
