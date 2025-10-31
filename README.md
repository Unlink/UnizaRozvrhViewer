# UNIZA Rozvrh Viewer

Blazor WebAssembly aplikácia pre pohodlné prehliadanie rozvrhov UNIZA. Umožňuje vyhľadávať predmety, prezerať rozvrhy učiteľov, exportovať rozvrh do Excelu a hľadať prieniky voľných okien. Podporuje svetlý/tmavý motív a PWA.

## Funkcie
- Vyhľadávanie a zobrazenie rozvrhu podľa predmetu
- Zoznam učiteľov a rozvrh podľa učiteľa
- Export rozvrhu učiteľa do Excelu (ClosedXML + vstavaná šablóna)
- Vyhľadávanie prienikov (voľné okná medzi rozvrhmi)
- Prepínanie motívu (svetlý/tmavý) s uložením preferencie
- PWA podpora (inštalovateľná)

## Technológie
- Blazor WebAssembly (.NET9, C#13)
- Bootstrap5 pre UI
- ClosedXML pre generovanie Excel súborov
- Vnútorné knižnice:
 - `UnizaScheduleApi` – modely a pomocníci pre dáta rozvrhov
 - `UnizaScheduleTable` – logika tabuľky rozvrhu (výpočty a reprezentácia)

---
© Michal Ďuračík, FRI UNIZA