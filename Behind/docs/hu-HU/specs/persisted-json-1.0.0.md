# Tárolt JSON-rekordok 1.0.0

Állapot: aktuális. A sémák meglévő formátumokat írnak le; nincs adatformátum-migráció.

| Struktúra | JSON Schema 2020-12 | Ellenőrzés mentés előtt |
| --- | --- | --- |
| LayoutConfiguration, SiteLayout, LayoutSection, LayoutWidget, LayoutBadge, LayoutAssignment | [elrendezések](../../schemas/layouts-1.0.0.schema.json) | A LayoutService az atomi fájlcsere előtt ellenőrzi a tisztított dokumentumot |
| ContactSubjects | [tárgyak](../../schemas/contact-subjects-1.0.0.schema.json) | Közös kapcsolat-JSON-konverter, olvasáskor és íráskor |
| ContactFieldSchema / ContactField | [mezők](../../schemas/contact-fields-1.0.0.schema.json) | Közös kapcsolat-JSON-konverter, olvasáskor és íráskor |
| ContactFieldValues / ContactValue | [értékek](../../schemas/contact-values-1.0.0.schema.json) | Közös kapcsolat-JSON-konverter, olvasáskor és íráskor |
| ScheduleParticipants / résztvevők | [résztvevők](../../schemas/schedule-participants-1.0.0.schema.json) | ParseScheduleParticipants és ToJson |
| ScheduleParameters | [paraméterek](../../schemas/schedule-parameters-1.0.0.schema.json) | ParseScheduleParameters és ToJson |
| AuditAction és PropertyDifference | [auditművelet](../../schemas/audit-action-1.0.0.schema.json) | Audit-JSON-konverter és tárolási konverzió az adattartalom írása előtt |

A Common beágyazott `JsonSchemas` szolgáltatása egyszer fordítja le az ellenőrzőket.
A hibák nem tartalmaznak adatértékeket; a sémanév azonosítja a hibás formátumot.
A típusos kérésfeldolgozás és a meglévő tartományellenőrzés továbbra is elutasítja
a hibás értékeket. A séma a struktúrát írja le, nem jogosultságot vagy üzleti szabályt.
A dinamikus telepített widgetek, nyelvek, útvonalegyediség, azonosítóhivatkozások,
cron- és műveletszabályok nincsenek megismételve a sémákban.

Az elrendezések megtartják a schemaVersion 0 frissítését és az 1-es tárolást,
az optimista revízióellenőrzést, tisztítást és töröltelrendezés-védelmet.
A közzétett séma a kanonikus camelCase kimenetet írja le; a meglévő olvasó
továbbra sem kisbetűérzékeny, mentéskor kanonikus JSON készül.

A kapcsolatsémák elfogadják a camelCase és PascalCase tulajdonságneveket,
valamint a jelenlegi szerializálók opcionális mezőit. A mezőtípusok a meglévő
`text`, `textarea`, `email`, `boolean` szövegek, nem számmal jelölt enumértékek.
Az ütemezőparaméterek szövegek vagy null értékek. A résztvevőknél megmaradnak
a kisbetűfüggetlen mezőnevek és a szemantikus típusellenőrzés. A null gyökér és
alapérték kezelése, valamint a null résztvevő meglévő FormatException hibája marad;
a séma ezt a szemantikus elutasítást a feldolgozóra hagyja.
Az egyszerű auditműveletek tárolt nevek maradnak; az adattartalmú műveletek
megőrzik a `value` típusjelölőt és a tetszőleges régi/új különbségértékeket.

A `PersistedJsonSchemaTests`, a meglévő elrendezés-, kapcsolat-, ütemező- és
audittesztek, valamint a `JsonSchemaPostgresIntegrationTests` ellenőrzi
a kompatibilitást, hibás alakokat, változatlan fájlokat, konvertereket és a valós
PostgreSQL-tárolást. A személyes adatot tartalmazó kapcsolat-/audit-JSON helyben
ellenőrződik, nem kerül gyorsítótárba vagy naplóba.
