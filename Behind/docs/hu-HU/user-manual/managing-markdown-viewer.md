# Dokumentáció (Markdown Viewer)

A **Dokumentáció** a Behind Markdown Viewer modulja: egy könyvtár Markdown (`.md`) fájljait mutatja dokumentációs weboldalként, és ugyanazt a dokumentációt statikus HTML oldalakként is elő tudja állítani. Nem használja az adatbázis fájltárolóját (VFS): közvetlenül a kiszolgáló két könyvtárával dolgozik.

A dokumentációt mindenki elolvashatja a főmenü **Dokumentáció** pontja alatt (`#/docs`). A statikus oldalakat az **Adminisztráció > Dokumentáció** alatt állíthatja elő (`#/manage/markdown-viewer`).

| Művelet | Ehhez kell |
|---------|------------|
| A dokumentáció olvasása | semmi, nyilvános |
| A generátor oldal megnyitása | a `MarkdownViewer.Read` engedély |
| A statikus oldalak előállítása | a `MarkdownViewer.Write` engedély |
| A könyvtárak módosítása | a `Configuration.Manage` engedély (fejlesztők) |

Az adminisztrátorok alapból megkapják a `MarkdownViewer` engedélyeket.

## A könyvtárak beállítása

A modul a `markdownviewer.json` fájlban állítható, amelynek saját füle van a [Beállítások](./managing-settings.md) oldalon:

| Beállítás | Jelentése |
|-----------|-----------|
| **SourceDirectory** | A Markdown fájlokat, vagy nyelvenként egy mappát tartalmazó könyvtár (lásd [Nyelvek](#nyelvek)). Üres vagy hiányzó érték esetén a dokumentáció nem érhető el. |
| **OutputDirectory** | A könyvtár, amelybe a statikus oldalak készülnek. Nem tartalmazhatja a forráskönyvtárat, és nem lehet a belsejében. |
| **Title** | A dokumentáció címe, a navigációban és a generált oldalakon jelenik meg. |
| **DefaultDocument** | Az elsőként megjelenő dokumentum, például `README.md`. Ha nem létezik, az első dokumentum jelenik meg. |

A relatív könyvtárakat az alkalmazás mappájához képest értelmezi. A módosítás azonnal érvénybe lép, az alkalmazást nem kell újraindítani. Éles környezetben használjon abszolút útvonalakat vagy a konténerbe csatolt könyvtárakat.

## A dokumentáció olvasása

Az oldal egy navigációban listázza a forráskönyvtár dokumentumait (az almappákkal együtt; a rejtett fájlokat, mappákat és a szimbolikus hivatkozásokat figyelmen kívül hagyja). A navigáció feletti cím a kezdőoldal (a **DefaultDocument**), és nem szerepel újra a listában. Minden legfelső szintű mappa egy szakasz: a nevét a kezdődokumentumban a mappa `README.md` fájljára mutató hivatkozás szövege adja (például **Fejlesztői dokumentáció** és **Felhasználói kézikönyv**), egyébként a `README.md` címe, egyébként a mappa neve. A szakasz a `README.md` fájlra mutat, utána a mappa többi dokumentuma következik. A generált statikus oldalak ugyanezt a navigációt használják. A dokumentum címe az első `# címsora`, vagy a fájlneve. Más `.md` fájlokra mutató hivatkozások a megjelenítőben nyílnak meg, a dokumentumok melletti képek (`png`, `jpg`, `gif`, `webp`, `svg`, `ico`) megjelennek, az ugyanazon oldali címsorokra mutató hivatkozások odagörgetnek. A Markdown fájlban lévő nyers HTML szövegként jelenik meg, a nem biztonságos hivatkozásokat eltávolítja.

## Nyelvek

A dokumentáció követi az alkalmazás nyelvválasztóját. A forráskönyvtár nyelvenként egy mappát tartalmazhat, a területi kód nevével (`en-US`, `hu-HU`: két vagy három kisbetű, kötőjel és két nagybetű):

```text
docs/
  en-US/
    README.md
    guide/setup.md
  hu-HU/
    README.md
    guide/setup.md
```

- A megjelenítő a választóban kiválasztott nyelv dokumentumait mutatja. Ha egy dokumentum nem létezik azon a nyelven, ugyanazt a dokumentumot mutatja az alapértelmezett nyelven (a `frontend.json` `Frontend › Site › DefaultLocale` beállítása), majd az (ábécé szerinti) első nyelven, és egy figyelmeztetés megmondja, melyik nyelv látható. A képek ugyanabból a nyelvi mappából jönnek, ugyanezzel a tartalékkal.
- A dokumentumok közötti hivatkozások egy nyelvi mappán belül és nyelvek között is működnek (a `../hu-HU/README.md` azt a nyelvet nyitja meg, bármelyik nyelv van kiválasztva). A képek a saját nyelvi mappájukban maradnak.
- A forráskönyvtárban közvetlenül lévő (nem nyelvi mappában levő) Markdown fájlok közös oldalak, például az összes nyelv áttekintése. A nyelvi dokumentumok hivatkozhatnak rájuk (`../README.md`), ők pedig a nyelvi mappákra (`en-US/README.md`); a navigációban nem szerepelnek. Más mappákat (például `api`) soha nem tekint nyelvnek.
- Nyelvi mappák nélkül a forráskönyvtár egyetlen nyelvű, és minden a fentiek szerint működik.
- A generátor nyelvenként egy weboldalt készít a `<kimenet>/<nyelv>/...` helyre, minden oldal fejlécében nyelvváltóval (ugyanarra a dokumentumra mutat a másik nyelven, vagy a kezdőlapjára, ha hiányzik). A közös oldalak a kimenet gyökerébe kerülnek (`<kimenet>/README.html`). A `<kimenet>/index.html` a böngésző nyelvére, vagy az alapértelmezett nyelvre irányítja a látogatót. A dokumentumok nélküli nyelvi mappát kihagyja.

## Statikus oldalak előállítása

1. Állítsa be a **SourceDirectory** és az **OutputDirectory** értékét a `markdownviewer.json` fájlban.
2. Nyissa meg az **Adminisztráció > Dokumentáció** oldalt, és válassza a **Statikus oldalak előállítása** gombot.

Minden Markdown fájlból ugyanazon az útvonalon HTML oldal készül (`guide/setup.md` helyett `guide/setup.html`) a navigációval, az oldalak közötti hivatkozásokkal, a `markdownviewer.css` stíluslappal és a forráskönyvtár képeivel. Az `index.html` átirányít az alapértelmezett dokumentumra, kivéve ha a dokumentációban van `index.md` (nyelvek esetén minden nyelvi mappa saját `index.html` fájlt kap). A generátor megjegyzi, mit írt (`.markdownviewer-generated`), így a törölt dokumentumok oldalait a következő futtatáskor eltávolítja, a nem általa írt fájlokhoz pedig soha nem nyúl. Az eredmény megmondja, hány oldal és fájl készült, és hány elavult fájlt törölt.

Ha a könyvtárak nem használhatók, a generátor nem ír semmit, hanem megmondja az okot: a forráskönyvtár nincs megadva vagy nem létezik, a kimeneti könyvtár nincs megadva, vagy a két könyvtár tartalmazza egymást.
