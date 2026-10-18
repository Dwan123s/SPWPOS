# Generate diagrams from the running database

This exporter reads the live SQL Server catalog in the existing development container. It does not use the EF model snapshot, pending migrations, `docs/database/schema.sql`, or the manually maintained UML v2 specification. It runs SELECT queries against catalog views in a transaction; it does not change the schema or read application records.

From the repository root, install the pinned Graphviz WebAssembly renderer and Sharp PNG rasterizer once:

```powershell
npm --prefix scripts/database-diagram ci --ignore-scripts
```

Then generate the current diagrams:

```powershell
.\scripts\Export-DatabaseDiagram.ps1
```

Requirements: Node.js (the repository's configured version is suitable), Docker CLI access, and the running `spwpos-sqlserver-1` development container. The script uses the container's existing SQL Server credential without printing it or passing it in host process arguments. It does not start or restart Docker, SQL Server, or the API. The internal container connection uses its installed `sqlcmd` with `-C` for the development SQL Server certificate, consistent with the project's existing container health check.

The default output is [docs/database/current/index.html](../../docs/database/current/index.html). Open it in a normal browser by double-clicking the file; refresh an already open tab after regenerating. All diagram content is embedded; there are no CDN requests and no web server is needed. Keep the other generated files beside the HTML for its SVG and metadata download links.

The explorer follows a conventional SQL diagram: blue table headers, larger monospace column names, PK/FK labels, and seven functional areas with visible boundaries and headings. The groups are accounts/security, availability/preferences, tasks/categories, planning/execution, workload analysis, notifications/operations, and notes/AI assistant. Short tables fit their content; long tables cap at 284 pixels with internal scrolling. All objects and cross-group connections remain present. Dependency ranks order tables inside each functional area; group placement affects presentation only. Unknown tables appear in an additional Other area.

Choose **English** or **Tiếng Việt** in the top bar. This translates navigation, controls, headings, tooltips, table-purpose notes, and relationship/column/key labels. The choice is remembered when browser storage is available. Changing language preserves selection, zoom, camera position, and expanded detail rows. Physical SQL table names, column names, constraint/index names, types, defaults, and computed expressions remain exact. Translations and Vietnamese descriptions are maintained in `translations.mjs`; undocumented tables have an explicit translated missing-description notice.

Every connection is visible on load, using only horizontal and vertical segments through reserved gutters. Side connections attach to a mapped column; offscreen columns attach at the footer until scrolled into view. Composite keys remain a single connector with their complete ordered mappings in the inspector. Scrolling updates endpoints while preserving orthogonal routes. Connectors never cross a table interior.

- Click a table on the canvas, a navigator entry, or a related-table link to smoothly center it and see its purpose, incoming/outgoing relationships, full column definitions, defaults/computed expressions, primary key, and unique indexes. Centering animates pan and zoom together for 460 ms and respects reduced-motion preferences. A new selection replaces an unfinished animation; wheel input and dragging interrupt it.
- Click a line or use **Trace connection** to emphasize one FK and its endpoint columns. Ctrl/Command-click adds or removes individual FKs from the highlight. The other connections dim but remain drawn; Clear or Escape restores their full visibility. Relationship details include ordered composite mappings, cardinality, delete/update rules, and disabled/untrusted status.
- Search table or column names in Object explorer. Domain selection emphasizes a part of the schema. Both operations keep every table and connection on the canvas.
- Click a functional heading on the map or in the navigator to smoothly frame that area and highlight its relationships, including connections to other groups. Every table and FK stays on the canvas.
- Drag the canvas background, or hold **Shift + left mouse button** and drag anywhere on the map, including over tables and connections. Releasing a pan does not select a table. Scrollbars reach the entire scaled map; bounded side panels scroll independently, and wheel scrolling at a column list boundary continues onto the map. Canvas padding lets even outermost tables center exactly. Use zoom buttons or Ctrl/Command + wheel; **Fit all** shows the entire map, and **Locate selection** returns to a readable table view. Click the minimap to move smoothly.
- Press `/` to search and Escape to clear selection. Table buttons and connector lines support keyboard selection; detail tabs support arrow keys.

Table purposes are maintained in `descriptions.mjs`, checked against the application entities and context. They are explicitly labelled application descriptions, not database comments or inferred constraints. Unknown tables receive a visible missing-description note. A future refresh automatically includes new tables, columns, and foreign keys without requiring a handwritten diagram update.

The output includes:

- `images/README.md`: bilingual image gallery, with English and Vietnamese downloads for the grouped map, every-column print view, and seven function detail views.
- `images/map.png` and `images/map.vi.png`: **10800 × 8200** grouped images for this snapshot, with every table and FK and up to eight preview columns per table. Smaller `.preview.png` copies are for embedding in documents.
- `images/complete.png` and `images/complete.vi.png`: every captured table, column, and FK in a full-column print layout. Large images should be viewed at original size or zoomed in.
- `images/<group>.png` and `images/<group>.vi.png`: all columns in that group, outgoing FKs, and primary-key-only context tables. Incoming connections from other groups remain in the complete map. These use the print layout, not the compact map positions. SQL identifiers remain exact in both languages.
- `images/manifest.json`: source snapshot timestamp, renderer version, image dimensions, and explicit table/FK/column coverage.
- `index.html`: self-contained interactive explorer with every table, every column, and every FK.
- `map.svg`: compact export using the same card positions and obstacle-free connector routing as the explorer. All table objects and FKs are included; cards preview up to eight columns. The full-column export below preserves every column in printable form.
- `map.vi.svg`: the same grouped compact map with Vietnamese headings and captions. The viewer's map-download link follows the selected language.
- `layout.json`: table rectangles and orthogonal connector points for reproducible geometry and routing verification.
- `overview.svg`: every table and declared foreign key, with primary-key columns.
- `complete.svg`: every table, column, and declared foreign key.
- Seven module SVGs: accounts, availability, tasks, planning/execution, intelligence, notifications, and the assistant. Module views contain outbound FKs from the module's tables and compact referenced-table context. The grouping affects presentation only; unrecognized tables appear in an additional Other view.
- `schema.json`: the catalog snapshot, including exact SQL types, nullability, identity/computed flags, defaults, unique indexes, and ordered composite FK mappings.
- `manifest.json`: capture time, source database/server, counts, renderer version, and view coverage.
- `.dot` files: editable Graphviz sources for every diagram.

The capture timestamp distinguishes a generated snapshot from a live, continuously updating view. Run the command again after applying migrations. Pending migrations are deliberately not included. EF migration-history and SSMS diagram-storage tables are excluded; all other user tables are included. No relationships are inferred from names. Disabled/untrusted FKs are marked in the catalog and SVG tooltips; their declared cardinality does not guarantee the existing data conforms.

For another local development container/database:

```powershell
.\scripts\Export-DatabaseDiagram.ps1 -Container spwpos-sqlserver-1 -Database SPWPOS -OutputDirectory docs/database/current
```

The target container must supply `MSSQL_SA_PASSWORD` and `/opt/mssql-tools18/bin/sqlcmd`, as the project's SQL Server image does. This convenience wrapper is intended for that development setup. To render an existing metadata snapshot without connecting to SQL Server:

```powershell
node scripts/database-diagram/render.mjs --schema docs/database/current/schema.json --output docs/database/current
```

That command regenerates HTML, SVGs, and all PNGs. To regenerate **only images** from the existing SVGs and manifest, without SQL Server access:

```powershell
npm --prefix scripts/database-diagram run export:images
# For a different existing export:
node scripts/database-diagram/export-images.mjs --input path/to/export
```

PNGs are static exports; use `index.html` for selection, connection highlighting, and navigation. Both image languages use the same captured metadata and diagram geometry. Function-detail captions are translated while SQL identifiers and literal definitions remain unchanged. The PNG exporter processes one image at a time to bound memory consumption.

An existing trusted installation of `@viz-js/viz` can be selected through `-RendererModule C:\path\to\viz\dist\viz.js` or the renderer's `--renderer` option. `-NodePath` selects a Node executable when it is not on PATH.

Verification (uses the repository's existing frontend jsdom dependency for the offline viewer test):

```powershell
node --test scripts/database-diagram/render.test.mjs
```

The checks cover composite/self/parallel relationships, cardinality with unique/filtered keys, missing catalog references, SQL Server types, exact output coverage, and generated viewer interactions. They also check connector collisions (including group headings), group membership/focus, Vietnamese translation and English restoration, preservation of SQL literals/identifiers, bounded scrolling, centering animations, reduced motion, Shift-drag input, contrast, highlighting/dimming, and retention of all objects. The PNG gallery check verifies both languages, every function group, snapshot coverage, and actual image dimensions against its manifest. jsdom supplies browser geometry/scroll/input/animation boundaries; it does not validate final browser pixel rendering. They do not alter the running database.

References: [SQL Server foreign-key metadata](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-foreign-key-columns-transact-sql), [Viz.js renderer](https://viz-js.com/api/).
