import { readFile } from "node:fs/promises";
import {
  escapeHtml as h,
  groups,
  groupOf,
  sqlType,
  relationship,
} from "./model.mjs";
import { layoutSchema, geometry } from "./layout.mjs";
import { descriptions } from "./descriptions.mjs";
import { messagesVi, patternsVi } from "./translations.mjs";

export async function buildViewer(schema) {
  const layout = layoutSchema(schema);
  const payload = {
    ...schema,
    layout,
    geometry,
    i18n: { messages: messagesVi, patterns: patternsVi },
    groups: groups
      .filter((group) =>
        schema.tables.some((table) => groupOf(table).id === group.id),
      )
      .map(({ id, title, color }) => ({ id, title, color })),
    tables: schema.tables.map((table) => ({
      ...table,
      group: groupOf(table).id,
      description:
        descriptions[table.name] ||
        "No application description has been documented for this table yet.",
      columns: table.columns.map((column) => ({
        ...column,
        sqlType: sqlType(column),
      })),
    })),
    foreignKeys: schema.foreignKeys.map((fk) => ({
      ...fk,
      cardinality: relationship(schema, fk),
    })),
  };
  const css = await readFile(new URL("viewer.css", import.meta.url), "utf8");
  const js = await readFile(new URL("viewer.js", import.meta.url), "utf8");
  const captured = new Date(schema.capturedAt).toLocaleString("en-GB", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour12: false,
  });
  return {
    layout,
    html: `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>${h(schema.databaseName)} · SQL schema explorer</title><style>${css}</style></head>
<body>
<svg class="symbols" aria-hidden="true"><defs>
<symbol id="icon-db" viewBox="0 0 24 24"><ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v7c0 4 16 4 16 0V5M4 12v7c0 4 16 4 16 0v-7"/></symbol>
<symbol id="icon-table" viewBox="0 0 24 24"><rect x="3" y="4" width="18" height="16" rx="1"/><path d="M3 9h18M3 14h18M9 9v11"/></symbol>
<symbol id="icon-search" viewBox="0 0 24 24"><circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/></symbol>
</defs></svg>
<header class="app-header"><div class="brand-mark"><svg><use href="#icon-db"/></svg></div><div class="app-title"><h1>${h(schema.databaseName)} <span>/ Schema explorer</span></h1><p>SQL Server · database catalog snapshot</p></div><div class="schema-stats"><span><b>${schema.tables.length}</b> tables</span><span><b>${schema.tables.reduce((sum, table) => sum + table.columns.length, 0)}</b> columns</span><span><b>${schema.foreignKeys.length}</b> foreign keys</span></div><label class="language-control"><span class="sr-only">Language</span><select id="language" aria-label="Language"><option value="en">English</option><option value="vi">Tiếng Việt</option></select></label><button id="help-toggle" aria-expanded="false" aria-controls="help">Help</button></header>
<div id="help" hidden><strong>Explore the schema</strong><p>Every connection starts visible. Click any table to smoothly center it, inspect it, and highlight incoming and outgoing relationships. Ctrl/⌘-click connections to highlight several; unselected connections dim without disappearing. Search locates tables without removing them from the canvas. Scroll inside a table to read every column. Side connections follow their mapped column; offscreen columns connect at the footer. Drag empty canvas space, or hold Shift + the left mouse button to pan anywhere on the map, including over tables. Use scrollbars to reach all content or the minimap to move smoothly. Ctrl/⌘ + wheel zooms. Escape clears selection. Keyboard: Tab to a table or relationship, then Enter.</p><p>PK = primary key · FK = foreign key · ID = identity · ? = nullable. Arrows point from child (FK) to referenced parent. Only declared constraints are drawn; disabled or untrusted constraints are dashed. Descriptions are application notes; columns and keys come from the captured catalog. EF migration history and SSMS diagram storage are excluded.</p></div>
<main class="workspace">
<aside class="navigator" aria-label="Table navigator"><div class="pane-heading"><h2>Object explorer</h2><span id="nav-count">${schema.tables.length}</span></div><label class="search-box"><svg aria-hidden="true"><use href="#icon-search"/></svg><input id="search" type="search" placeholder="Find table or column…" aria-label="Find table or column" autocomplete="off"><kbd>/</kbd></label><div class="nav-options"><label for="module">Domain</label><select id="module"><option value="">All domains</option>${payload.groups.map((group) => `<option value="${h(group.id)}">${h(group.title)}</option>`).join("")}</select></div><div id="table-list" class="table-list"></div><div class="navigator-footer"><span class="status-dot"></span> ${h(schema.databaseName)}<small>All ${schema.tables.length} table objects stay on the map.</small></div></aside>
<section class="canvas-pane" aria-label="Database diagram"><div class="map-toolbar"><div class="toolbar-heading"><strong>Relationship map</strong><span id="map-status" aria-live="polite">All tables</span></div><div class="zoom-controls"><button id="minus" aria-label="Zoom out">−</button><output id="zoom-value">100%</output><button id="plus" aria-label="Zoom in">+</button><button id="fit">Fit all</button><button id="actual">100%</button></div><button id="locate">Locate selection</button><button id="clear">Clear</button><a id="export-map" href="map.svg" download title="Compact map with every table and foreign key">Export map ↗</a></div>
<div id="diagram" class="diagram" tabindex="0" aria-label="Scrollable schema map. Shift and left mouse drag pans over tables and connections."><div id="world-space"><div id="world"><div id="group-layer"></div><svg id="connections" aria-label="Foreign key connections"><defs><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M1 1L9 5L1 9" fill="none" stroke="context-stroke" stroke-width="1.5"/></marker></defs><g id="edge-layer"></g></svg><div id="table-layer"></div></div></div></div>
<div class="canvas-caption"><span><i class="line-sample"></i> Child → parent <i class="line-sample selected"></i> Selected relationship</span><span>Shift + left-drag to pan · Ctrl/⌘-click to add connections</span></div>
<div class="minimap" aria-label="Map overview"><div><span>OVERVIEW</span><button id="mini-fit" title="Fit every table in view">Fit all</button></div><svg id="minimap" role="group" aria-label="Click a table to locate it"><g id="mini-groups"></g><g id="mini-nodes"></g><rect id="mini-viewport" pointer-events="none"/></svg></div>
</section>
<aside class="inspector" aria-label="Table information"><div class="pane-heading"><h2>Table details</h2><span class="inspector-badge">SQL</span></div><div id="inspector-body"></div></aside>
</main>
<footer class="app-footer"><span><span class="status-dot"></span> Read-only schema snapshot</span><span>Captured ${h(captured)} UTC+07 · <a href="complete.svg" download>Full columns SVG</a> · <a href="schema.json">Metadata</a> · <a href="manifest.json">Manifest</a></span></footer>
<div id="announcement" class="sr-only" aria-live="polite"></div>
<script id="schema-data" type="application/json">${JSON.stringify(payload).replace(/</g, "\\u003c")}</script><script>${js}</script></body></html>`,
  };
}
