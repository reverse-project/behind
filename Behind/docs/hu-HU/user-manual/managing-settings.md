# Beállítások

A **Beállítások** a telepítés konfigurációs fájljait mutatja, fájlonként egy fülön, és lehetővé teszi egy fejlesztő számára, hogy a kiszolgáló megnyitása nélkül módosítsa őket. Az [Adminisztráció](./administration.md) oldal egyik eszköze.

## Ki használhatja

| Ön képes rá | Ehhez kell |
|-------------|------------|
| megnyitni az oldalt és olvasni a beállításokat | a `Configuration.Read` engedély |
| módosításokat menteni, titkokat generálni és forgatni | a `Configuration.Manage` engedély |

Mindkét engedély a `Developers` szerepkörhöz tartozik. Az adminisztrátorok soha nem rendelkeznek ezekkel az engedélyekkel.

## Fülek

Minden, az alkalmazás mellett létező konfigurációs fájl kap egy fület: `appsettings.json`, `backend.json`, `database.json`, `email.json`, `frontend.json` és `security.json`, valamint minden olyan modul fájlja, amelynek van ilyen (például a [Dokumentáció](./managing-markdown-viewer.md) modul `markdownviewer.json` fájlja). Az `email.json` és az `appsettings` fájlok kivételével, amelyeknek saját űrlapjuk van (lásd alább), a fül a fájl beállításait szakaszok szerint csoportosítva listázza, például `Security › Jwt`. A szöveges beállítások szövegmezők, a be- és kikapcsolós beállítások kapcsolók, a számok pedig számmezők.

Az oldalak képforgóinak korlátai a `frontend.json` számai (`Frontend › Carousels`): **MaxSlides** (képek száma képforgónként), **MinIntervalSeconds** és **MaxIntervalSeconds** (az engedélyezett másodpercek képenként), **IntervalSeconds** (ezzel az értékkel indul egy új képforgó), **MaxCaptionLength** és **MaxImageBytes**. Lásd az [Oldalak kezelése](./managing-sites.md) részt.

A `frontend.json` **DefaultLocale** beállítása (`Frontend › Site`) az elérhető nyelvek legördülő listája azok hivatalos nevével, például **English (International)** és **Magyar**. A nyelv minden látogatóra érvényes; a fájl nélküli nyelvet a rendszer elutasítja.

## Korhatárszűrés

A **backend.json > Backend > ContentRestrictions** szakasz **Enabled** kapcsolója vezérli a globális korhatárszűrést. **Alapértelmezés szerint kikapcsolt**, és újraindítás nélkül frissül. Kikapcsolva a tárolt korhatárok nem korlátozzák a hozzáférést. Bekapcsolva az olvasó privát születési dátumának meg kell felelnie az elem korhatárának; ismeretlen dátummal a korlátozott elem, kapcsolódó tartalma és fájljai nem érhetők el. A szabályok a **Tartalomkorlátozások** oldalon `RawDatabase.Manage` engedéllyel vagy a támogatott elemszerkesztők korhatármezőjével kezelhetők. A nulla megszünteti a korhatárt. A fejlesztők listázhatják, visszaállíthatják vagy végleg törölhetik a törölt szabályokat.

Válasszon tartalomtípust, majd az **Elemek böngészése** gombbal UUID-másolás nélkül kereshet elemet. Megjelenítési név vagy teljes azonosító alapján kereshet, az **Előző elemek** / **Következő elemek** gombokkal lapozhat. Az elem címe, fájlneve, neve vagy más elérhető szövege jelenik meg; címke hiányában az azonosítója. A kiválasztás kitölti a korlátozás azonosítóját. Az azonosító kézzel is megadható. A meglévő szabályok mellett is olvasható címke jelenik meg.

## Fiók-bejelentkezési módok

A **backend.json > Backend > Accounts** szakasz **Password**, **MagicLink**, **TwoFactor**, **Passkey** és **EmailTwoFactor** kapcsolói vezérlik a bejelentkezési és második hitelesítési módokat. Mindegyik alapértelmezett értéke **true**, a módosítások újraindítás nélkül érvénybe lépnek.

- A **Password** engedélyezi vagy letiltja a jelszavas bejelentkezést.
- A **MagicLink** engedélyezi vagy letiltja az e-mailes bejelentkezési hivatkozásokat. Az **LostPassword** jelszó-helyreállító hivatkozásokat **nem** tiltja le; ezek csak jelszó-visszaállítást tesznek lehetővé, teljes bejelentkezett munkamenetet nem adnak.
- A **TwoFactor** engedélyezi vagy letiltja a kétlépcsős hitelesítést. Kikapcsolása nem kerüli meg a védelmet: a kétlépcsős hitelesítést már beállított felhasználók nem tudnak bejelentkezni, amíg az ki van kapcsolva; akkor sem lehet bejelentkezni, ha a kétlépcsős hitelesítés kötelező, de ki van kapcsolva.
- A **Passkey** a hozzáférési kulcsokat bejelentkezési és második hitelesítési módként is engedélyezi vagy tiltja. A bejelentkezéshez használt kulcs ugyanannak a bejelentkezésnek a második lépéséhez nem használható. Kikapcsolva a helyreállító kódok továbbra is használhatók.
- Az **EmailTwoFactor** az e-mailes második hitelesítési kódokat engedélyezi vagy tiltja. A felhasználó az első hitelesítés után választhat a fiók megerősített címei közül. E-mailes bejelentkezési vagy jelszó-helyreállító hivatkozás után nem használható e-mailes kód, mert ugyanazt a csatornát használná.

Jelszó-helyreállítás után minden meglévő munkamenet, valamint a még érvényes bejelentkezési és helyreállítási hivatkozás érvényét veszti.

Ha egy mód le van tiltva, a korábban kiadott hivatkozásokat és az adott módhoz tartozó aktív kihívásokat a rendszer elutasítja.

## E-mail beállítások

Az `email.json` fül értéklista helyett űrlap:

1. Válassza ki az **E-mail szolgáltatót**. A **Logger** csak az alkalmazásnaplóba írja az e-maileket (fejlesztéshez jó). Az **Smtp** levelezőszerveren keresztül küldi őket.
2. Az **Smtp** esetén töltse ki a **Szerver**, a **Port** (1–65535) mezőt, az **SSL használata** beállítást és szükség esetén a **Feladó címe** mezőt. Feladó címe nélkül az alapszintű hitelesítés felhasználóneve szolgál feladóként.
3. Válassza ki a **Hitelesítés** típusát: **No** vagy **Basic** felhasználónévvel és jelszóval. A jelszó maszkolva van; hagyja üresen a tárolt jelszó megtartásához.
4. Szükség esetén írja meg a saját **E-mail sablonjait** (`ConfirmationEmail`, `PasswordResetCodeEmail`, `PasswordResetLinkEmail`, `MagicLinkEmail`). Az üres sablon a beépítettet használja, amely tippként látható.
5. Válassza a **Mentés** gombot. Az értékeket a rendszer ellenőrzi, és elutasítja, ha nem érvényesek. Az új szolgáltató azonnal érvénybe lép.

## Naplószintek

Az `appsettings.json` fül értéklista helyett a naplószintek űrlapja:

1. Válassza ki az **Alapértelmezett szintet**: a naplóba írt legalacsonyabb szintet (Verbose, Debug, Information, Warning, Error vagy Fatal). A **Nincs beállítva** meghagyja a beépített alapértéket (Information).
2. A **Szintek forrásonként** alatt vegyen fel egy forrást, egy névteret, például `Microsoft.AspNetCore` vagy `Behind`, és válassza ki a szintjét. A saját szinttel rendelkező forrás figyelmen kívül hagyja az alapértelmezett szintet. Minden forrásnak különböző névvel kell rendelkeznie; az **Eltávolítás** töröl egyet.
3. Válassza a **Mentés** gombot. Csak a szintek íródnak a fájlba; minden más benne úgy marad, ahogy volt. Az új szintek azonnal érvénybe lépnek.

## A titkok maszkolva vannak

A jelszavak, titkos kulcsok, aláírókulcsok, tokenek és kapcsolati karakterláncok soha nem jelennek meg. Csak az első és az utolsó karakterük látható, például `to...........UL`. Egy ilyen érték cseréjéhez írja be az újat a mezőjébe; a mező üresen hagyása megtartja a jelenlegi értéket.

## Beállítások módosítása

1. Módosítsa a kívánt értékeket egy fülön.
2. Válassza a **Mentés** gombot. Csak a módosított értékek íródnak a fájlba; minden más a fájlban úgy marad, ahogy volt.
3. Az **Elvetés** visszaállítja az eredeti értékeket a mezőkben.

Minden módosítás azonnal érvénybe lép, az alkalmazást nem kell újraindítani. Ugyanez igaz, ha kézzel szerkeszt egy konfigurációs fájlt: az alkalmazás figyeli a `database.json`, `backend.json`, `email.json`, `security.json`, `frontend.json` és `appsettings.json` fájlokat, és módosításukkor újratölti őket. Az érvénytelen JSON fájlt vagy a használhatatlan értékeket a rendszer elutasítja, és az előző értékek maradnak aktívak (a hiba a naplóba kerül). Csatolt kötettel rendelkező tárolóban a fájlfigyelőhöz a `DOTNET_USE_POLLING_FILE_WATCHER=true` beállítás szükséges; a módosítást ilyenkor néhány másodpercen belül észleli.

Az egyetlen érték, amelyet még mindig egyszer, induláskor olvas be a rendszer, az adatvédelmi tanúsítvány és kulcsok (`Security:DataProtection`), az OpenTelemetry beállításai és az adatbázis-szolgáltató.

## Generálás és forgatás

Egyes titkokat nem lehet beírni; helyettük gomb van:

| Beállítás | Gomb | Mit csinál |
|-----------|------|------------|
| `Security:SecretKey` (adminisztrátori jogosultságemelés) | **Generálás** | Új hitelesítő titkot hoz létre. |
| `Security:Elevation:DeveloperSecretKey` (fejlesztői jogosultságemelés) | **Generálás** | Új hitelesítő titkot hoz létre, akkor is, ha még nincs beállítva. |
| `Security:Jwt:SigningKey` | **Generálás** | Új, véletlenszerű aláírókulcsot hoz létre. Mindenkinek újra be kell jelentkeznie. |
| `Security:MagicLink:ActualSecret` | **Forgatás** | A jelenlegi varázshivatkozás-titok régi titokká válik, amelyet a konfigurált átfedési ideig még elfogad a rendszer, és mostantól új titkot használ. Azonnal érvénybe lép. |

Egy hitelesítő titok generálása **egyszer** megmutatja a hitelesítő címét és egy QR-kódot. Azonnal adja hozzá a hitelesítőjéhez; az oldal soha többé nem mutatja meg. Az új titok azonnal érvénybe lép. Minden gomb megerősítést kér.

## Amikor semmi sem módosítható

Ha a telepítés csak olvasható konfigurációval fut (`Security:ReadOnlyConfiguration`), az oldal megmutatja a maszkolt értékeket, de minden módosítást, generálást és forgatást elutasít. Az ilyen telepítést a külső konfigurációszolgáltatóján keresztül módosítsa.

A módosítások az alkalmazás melletti fájlokba íródnak. Tárolóban a fájlnak írhatónak kell lennie arra a felhasználóra nézve, amelyként a tároló fut, ezért a `frontend.json` fájlt (és minden más szerkeszteni kívánt fájlt) írható gazdagép-útvonalról csatolja, mint a `security.json` fájlt a példa compose fájlban. Legyen óvatos az olyan beállításokkal, mint az adatbázis-kapcsolat: egy hibás érték megakadályozhatja az alkalmazás indulását.
