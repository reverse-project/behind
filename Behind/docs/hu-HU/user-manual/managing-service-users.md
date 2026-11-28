# Szolgáltatásfelhasználók és AI-ügynökök

Nyissa meg az **Adminisztráció > Beállítások > backend.json** lapot.
Engedélyezze a fiókoknál a **Szolgáltatásfelhasználó-hitelesítést**, valamint az
**MCP szervert**. Meglévő telepítésen mindkettő letiltott alapértékű.
A változtatások újraindítás nélkül érvényesülnek.

Hozzon létre elnevezett szolgáltatásfelhasználót. Ez külön felhasználó,
nem az Ön fiókja: saját szerepkörei, engedélyei és korlátozásai vannak,
és nem örökli az Ön jogosultságait. A hozzáférésszerkesztőben csak az ügynök
számára szükséges engedélyeket adja meg. Nem adhat át nem birtokolt engedélyt.

Másolja ki a létrehozás után egyszer megjelenő bearer kulcsot.
Az ügynök Streamable HTTP MCP címe `https://your-host/mcp`, fejléce
`Authorization: Bearer <key>`. Tartsa titokban a kulcsot.
Az `api_schema` felderíti a JSON API-kat, az `api_request` a szolgáltatás
saját hozzáférésével hívja azokat. Az MCP minden `tools/list` kéréskor
automatikusan felderíti a telepített, engedélyezett modulok JSON-műveleteit is.
Például egy fórum modul `forum_get_posts` és `forum_post_posts` eszközöket
tehet elérhetővé; a séma leírja az útvonal-, lekérdezési és JSON-törzsadatokat.
Az eszközök nem rejtőznek el a szolgáltatásfelhasználó engedélyei alapján, de
minden hívásnál a rendszer az aktuális jogosultságokat és a szokásos
alkalmazás-middleware-t érvényesíti. Ide tartoznak a modulkezelési és moderálási
műveletek is, ha az identitás hozzáfér hozzájuk. A modul letiltásakor eszközei
eltűnnek; az elavult vagy ismeretlen eszköznév -32602 hibát ad. Interaktív
fiókművelet és bináris átvitel nem támogatott. Ha nincs kötelező argumentum,
az `arguments` elhagyható. Az `api_request` mintájára a moduleszközök szövege is
`{status,body}` JSON; törzs nélküli válasznál a `body` üres karakterlánc.

Letiltással ideiglenesen visszavonható a hozzáférés; kulcscserével a hitelesítőadat
lecserélhető. A törlés visszavonja a hozzáférést. Fejlesztők listázhatják,
visszaállíthatják és végleg törölhetik a törölt identitásokat; a visszaállítás
nem éleszti újjá a régi kulcsot. Bármelyik MCP kapcsoló letiltása blokkolja
a végpontot, a felhasználók eltávolítása nélkül.

A kezeléshez Configuration engedély és User.Manage vagy User.Delete szükséges.
Írásvédett konfiguráció mellett nem módosíthat. Origin nélküli ügynökkérés
engedélyezett; böngészős Origin értéket az AllowedOrigins listába kell felvenni.
A kérés- és API-válaszméret beállítható. Ne próbáljon újra vakon módosítást,
ha a túl nagy válasz szerint a művelet már végrehajtódhatott.
