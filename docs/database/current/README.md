# SPWPOS — database schema explorer

Captured from the running SQL Server database at **2026-10-18T15:26:57.3648367Z** (UTC).

**45 tables · 356 columns · 69 declared foreign keys.**

Open [index.html](index.html) for the SQL-style interactive explorer. Every table belongs to one of seven labeled functional areas, with all cross-group connections retained. Click a group heading to focus its area. Choose **English** or **Tiếng Việt** in the top bar; controls, group labels, descriptions, and relationship details are translated while SQL identifiers and literal definitions stay unchanged. Short cards fit their columns and long cards use bounded internal scrolling. Connectors use horizontal and vertical segments. Selecting a table smoothly centers it and highlights its relationships; Ctrl/Command-click connections to highlight several together and dim the rest. Search, zoom, Shift + left-drag panning, and the minimap navigate the complete schema.

This is a captured snapshot. Regenerate after database changes.

- [Compact table map](map.svg) — every table and FK; up to eight preview columns per card.
- [Sơ đồ nhóm chức năng bằng tiếng Việt](map.vi.svg) — đầy đủ bảng và khóa ngoại.
- [Full-column print export](complete.svg) — every captured column.
- [PNG image gallery / Thư viện ảnh PNG](images/README.md) — English and Vietnamese maps, full columns, and function details.
- [Grouped map PNG](images/map.png) · [Sơ đồ nhóm PNG tiếng Việt](images/map.vi.png)
- [Every column PNG](images/complete.png) · [Ảnh đầy đủ cột](images/complete.vi.png)
- [Legacy relationship overview](overview.svg)
- [Accounts & security print export](accounts.svg)
- [Availability & preferences print export](time.svg)
- [Tasks & categories print export](tasks.svg)
- [Planning & execution print export](planning.svg)
- [Intelligence & workload print export](intelligence.svg)
- [Notifications & operations print export](notifications.svg)
- [Notes & AI assistant print export](assistant.svg)
- [Raw catalog metadata](schema.json)
- [Routing geometry](layout.json)
- [Generation manifest](manifest.json)

Regenerate from the repository root:

```powershell
npm --prefix scripts/database-diagram ci --ignore-scripts
.\scripts\Export-DatabaseDiagram.ps1
```

See [the generator guide](../../../scripts/database-diagram/README.md). The query reads metadata only and does not change the database or apply migrations. Application descriptions are editorial notes; the captured SQL catalog is authoritative for structure.
