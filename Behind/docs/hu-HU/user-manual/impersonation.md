# Megszemélyesítés

A **Megszemélyesítés** lehetővé teszi, hogy egy adminisztrátor az alkalmazást egy másik felhasználóként használja, például egy általa jelentett probléma reprodukálásához. A megszemélyesítés közben végzett minden tevékenységet az [auditnapló](./audit-log.md) rögzít.

Az `Impersonation.Manage` engedélyre van szükség. Az adminisztrátorok alapértelmezés szerint rendelkeznek vele; a felhasználók és a fejlesztők nem.

## Megkezdés

1. Nyissa meg az **Adminisztráció > Felhasználók** oldalt.
2. Válassza a **Megszemélyesítés** gombot a felhasználónál, és erősítse meg.

A kezdőoldalra kerül, és egy sáv jelenik meg a „Jelenleg {0} nevében jár el.” szöveggel, ahol {0} a felhasználó neve. Az alkalmazást pontosan úgy látja, ahogyan az a felhasználó, az ő hozzáférésével.

A **Megszemélyesítés** gomb csak azoknál a felhasználóknál jelenik meg, akiket megszemélyesíthet. Nem személyesíthet meg:

- önmagát, vagy bárkit, amíg már megszemélyesít valakit,
- letiltott vagy törölt felhasználókat,
- fejlesztőket, kivéve, ha Ön is fejlesztő,
- olyan felhasználókat, akik rendelkeznek olyan engedéllyel, amellyel Ön maga nem. Ilyenkor ezt látja: „Nem személyesítheti meg ezt a felhasználót.”

## Befejezés

Válassza a **Megszemélyesítés befejezése** gombot a sávban. Ezzel visszatér a saját munkamenetébe. A megszemélyesítés egy óra után, a megszemélyesített felhasználó összes munkamenetből való kijelentkeztetésekor, vagy az Ön kijelentkezésekor magától is véget ér.

## Az auditnaplóban

- A megkezdés `ImpersonationStarted`, a befejezés `ImpersonationEnded` bejegyzésként jelenik meg.
- A közben végzett minden módosításnál a **Ki** mezőben Ön, a **Megbízó** mezőben pedig a megszemélyesített felhasználó látható, soha nem csak a megszemélyesített felhasználó.
- A megszemélyesített felhasználó a megszemélyesítést egy további bejegyzésként látja a **Munkamenetek** oldalán.
