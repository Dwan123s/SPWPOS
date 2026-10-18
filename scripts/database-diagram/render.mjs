import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve, join, relative } from "node:path";
import { pathToFileURL, fileURLToPath } from "node:url";
import { createRequire } from "node:module";
import {
  groups,
  groupOf,
  escapeHtml as h,
  validateSchema,
  graphSpec,
} from "./model.mjs";

import { buildViewer } from "./viewer.mjs";
import { mapSvg } from "./map-svg.mjs";
import { translate } from "./translations.mjs";
import { exportImages } from "./export-images.mjs";

const argumentsByName = Object.fromEntries(
  process.argv
    .slice(2)
    .reduce(
      (pairs, arg, index, all) =>
        arg.startsWith("--")
          ? [...pairs, [arg.slice(2), all[index + 1]]]
          : pairs,
      [],
    ),
);
if (!argumentsByName.schema || !argumentsByName.output)
  throw new Error(
    "Usage: node render.mjs --schema schema.json --output directory [--renderer /path/to/viz.js]",
  );
const schema = JSON.parse(
  (await readFile(argumentsByName.schema, "utf8")).replace(/^\uFEFF/, ""),
);
validateSchema(schema);
const output = resolve(argumentsByName.output);
await mkdir(output, { recursive: true });
const require = createRequire(import.meta.url);
const renderer = argumentsByName.renderer || process.env.PLANORA_VIZ_MODULE;
let vizModule;
try {
  vizModule = await import(
    pathToFileURL(renderer ? resolve(renderer) : require.resolve("@viz-js/viz"))
      .href
  );
} catch (error) {
  throw new Error(
    "Install the renderer first: npm --prefix scripts/database-diagram ci; or pass --renderer with the path to @viz-js/viz/dist/viz.js.",
    { cause: error },
  );
}
const viz = await vizModule.instance();
const activeGroups = groups.filter((group) =>
  schema.tables.some((table) => groupOf(table).id === group.id),
);
const views = [
  { id: "overview", full: false },
  { id: "complete", full: true },
  ...activeGroups.map((group) => ({
    id: group.id,
    group: group.id,
    full: true,
  })),
];
const results = [];
for (const view of views) {
  const spec = graphSpec(schema, view);
  const rendered = viz.render(spec.dot, {
    format: "svg",
    engine: view.id === "overview" ? "neato" : "dot",
  });
  const errors = rendered.errors.filter((error) => error.level === "error");
  if (rendered.status !== "success" || errors.length)
    throw new Error(
      `Could not render ${view.id}: ${errors.map((error) => error.message).join("; ")}`,
    );
  const svg = rendered.output;
  // Give standalone diagrams a useful accessible title as well as tooltips.
  const labelled = svg
    .replace(/<svg\b/, '<svg role="img"')
    .replace(/xlink:href=/g, "href=")
    .replace(
      /(<svg[^>]*>)/,
      `$1\n<title>${h(schema.databaseName)} — ${h(spec.title)}</title>`,
    );
  await writeFile(join(output, `${view.id}.svg`), labelled);
  await writeFile(join(output, `${view.id}.dot`), spec.dot);
  results.push({
    id: view.id,
    title: spec.title,
    tables: spec.tableIds.length,
    foreignKeys: spec.foreignKeyIds.length,
  });
}
const metadata = {
  databaseName: schema.databaseName,
  serverName: schema.serverName,
  capturedAt: schema.capturedAt,
  tables: schema.tables.length,
  columns: schema.tables.reduce((sum, table) => sum + table.columns.length, 0),
  foreignKeys: schema.foreignKeys.length,
  interactiveLayout: "functional-groups-side-ports-v4",
  languages: ["en", "vi"],
  functionalGroups: activeGroups.map((group) => ({
    id: group.id,
    title: group.title,
    titleVi: translate(group.title, "vi"),
    tables: schema.tables.filter((table) => groupOf(table).id === group.id)
      .length,
  })),
  interactiveMap: "index.html",
  routing: "layout.json",
  compactExport: "map.svg",
  imageGallery: "images/README.md",
  renderer: "@viz-js/viz",
  graphvizVersion: viz.graphvizVersion,
  views: results,
};
await writeFile(
  join(output, "manifest.json"),
  JSON.stringify(metadata, null, 2) + "\n",
);
await writeFile(
  join(output, "README.md"),
  `# ${schema.databaseName} — database schema explorer

Captured from the running SQL Server database at **${schema.capturedAt}** (UTC).

**${metadata.tables} tables · ${metadata.columns} columns · ${metadata.foreignKeys} declared foreign keys.**

Open [index.html](index.html) for the SQL-style interactive explorer. Every table belongs to one of seven labeled functional areas, with all cross-group connections retained. Click a group heading to focus its area. Choose **English** or **Tiếng Việt** in the top bar; controls, group labels, descriptions, and relationship details are translated while SQL identifiers and literal definitions stay unchanged. Short cards fit their columns and long cards use bounded internal scrolling. Connectors use horizontal and vertical segments. Selecting a table smoothly centers it and highlights its relationships; Ctrl/Command-click connections to highlight several together and dim the rest. Search, zoom, Shift + left-drag panning, and the minimap navigate the complete schema.

This is a captured snapshot. Regenerate after database changes.

- [Compact table map](map.svg) — every table and FK; up to eight preview columns per card.
- [Sơ đồ nhóm chức năng bằng tiếng Việt](map.vi.svg) — đầy đủ bảng và khóa ngoại.
- [Full-column print export](complete.svg) — every captured column.
- [PNG image gallery / Thư viện ảnh PNG](images/README.md) — English and Vietnamese maps, full columns, and function details.
- [Grouped map PNG](images/map.png) · [Sơ đồ nhóm PNG tiếng Việt](images/map.vi.png)
- [Every column PNG](images/complete.png) · [Ảnh đầy đủ cột](images/complete.vi.png)
- [Legacy relationship overview](overview.svg)
${results
  .slice(2)
  .map((view) => `- [${view.title} print export](${view.id}.svg)`)
  .join("\n")}
- [Raw catalog metadata](schema.json)
- [Routing geometry](layout.json)
- [Generation manifest](manifest.json)

Regenerate from the repository root:

\x60\x60\x60powershell
npm --prefix scripts/database-diagram ci --ignore-scripts
.\\scripts\\Export-DatabaseDiagram.ps1
\x60\x60\x60

See [the generator guide](${relative(output, fileURLToPath(new URL("README.md", import.meta.url))).replaceAll("\\", "/")}). The query reads metadata only and does not change the database or apply migrations. Application descriptions are editorial notes; the captured SQL catalog is authoritative for structure.
`,
);

const viewer = await buildViewer(schema);
await writeFile(join(output, "map.svg"), mapSvg(schema, viewer.layout));
await writeFile(
  join(output, "map.vi.svg"),
  mapSvg(schema, viewer.layout, null, "vi"),
);
await writeFile(
  join(output, "layout.json"),
  JSON.stringify(viewer.layout, null, 2) + "\n",
);
await writeFile(join(output, "index.html"), viewer.html);
await exportImages(output);
console.log(
  `Rendered ${metadata.tables} tables, ${metadata.columns} columns and ${metadata.foreignKeys} foreign keys.\n${join(output, "index.html")}`,
);
