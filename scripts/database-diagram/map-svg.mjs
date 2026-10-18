import { escapeHtml as h, groupOf, qualified, sqlType } from "./model.mjs";
import { geometry } from "./layout.mjs";
import { translate } from "./translations.mjs";

// Compact, portable export of the same geometry used by the interactive map.
// All table objects and FKs are retained. The HTML explorer contains every
// scrollable column; complete.svg is the separate full-column print export.
export function mapSvg(schema, layout, selectedId = null, language = "en") {
  const tr = (message) => translate(message, language);
  const panels = layout.groups
    .map(
      (group) =>
        `<g data-function-group="${group.id}"><rect x="${group.x}" y="${group.y}" width="${group.width}" height="${group.height}" rx="7" fill="${group.color}" fill-opacity=".3" stroke="#aebfd0"/><text x="${group.x + 20}" y="${group.y + 32}" font-size="18" font-weight="bold" fill="#203f5c">${h(tr(group.title))}</text><text x="${group.x + 20}" y="${group.y + 54}" font-size="13" fill="#465b70">${h(tr(`${group.tableIds.length} tables · ${group.columns} columns`))}</text></g>`,
    )
    .join("");
  const byId = new Map(schema.tables.map((table) => [table.id, table]));
  const keys = schema.foreignKeys.filter(
    (fk) => fk.childTableId === selectedId || fk.parentTableId === selectedId,
  );
  const related = new Set(
    keys.flatMap((fk) => [fk.childTableId, fk.parentTableId]),
  );
  const active = new Set(keys.map((fk) => fk.id));
  const edges = [...layout.edges]
    .sort((a, b) => Number(active.has(a.id)) - Number(active.has(b.id)))
    .map((edge) => {
      const fk = schema.foreignKeys.find((fk) => fk.id === edge.id);
      return `<g><title>${h(fk.name)}</title><polyline points="${edge.points.map((point) => point.join(",")).join(" ")}" fill="none" stroke="${active.has(edge.id) ? "#1769b5" : "#667d91"}" stroke-width="${active.has(edge.id) ? 2.3 : 1.2}" opacity="${selectedId !== null && !active.has(edge.id) ? 0.25 : 1}" marker-end="url(#arrow)"${fk.disabled || fk.notTrusted ? ' stroke-dasharray="6 4"' : ""}/></g>`;
    })
    .join("");
  const nodes = layout.nodes
    .map((node) => {
      const table = byId.get(node.id),
        selected = node.id === selectedId;
      const fkColumns = new Set(
        schema.foreignKeys
          .filter((fk) => fk.childTableId === table.id)
          .flatMap((fk) => fk.columns.map((pair) => pair.childColumn)),
      );
      const shown = table.columns.slice(0, 8);
      const footerY = node.height - geometry.footerHeight;
      return `<g transform="translate(${node.x} ${node.y})" id="map-table-${node.id}"><title>${h(qualified(table))}: ${h(table.columns.map((column) => column.name + " " + sqlType(column)).join("; "))}</title><rect width="${node.width}" height="${node.height}" rx="3" fill="white" stroke="${selected ? "#1769b5" : related.has(node.id) ? "#6c98c2" : "#9caec0"}" stroke-width="${selected || related.has(node.id) ? 2 : 1}"/><rect x="1" y="1" width="${node.width - 2}" height="${geometry.headerHeight - 1}" fill="${selected ? "#15588b" : "#32688d"}"/><path d="M12 15h15v13H12ZM12 19h15M17 19v9" fill="none" stroke="#d9e7f3"/><text x="36" y="26" font-family="Consolas,monospace" font-size="13" font-weight="bold" fill="#ffffff">${h(qualified(table))}</text><line y1="${geometry.headerHeight}" y2="${geometry.headerHeight}" x2="${node.width}" stroke="#c9d3de"/>${shown.map((column, index) => `<rect x="1" y="${geometry.headerHeight + 1 + index * geometry.rowHeight}" width="${node.width - 2}" height="${geometry.rowHeight}" fill="${index % 2 ? "#fafbfc" : "#fff"}"/><text x="9" y="${geometry.columnOffset + 4 + index * geometry.rowHeight}" font-size="9" font-weight="bold" fill="${column.primaryKey ? "#8b620e" : "#1264a8"}">${column.primaryKey ? "PK " : ""}${fkColumns.has(column.name) ? "FK" : ""}</text><text x="44" y="${geometry.columnOffset + 4 + index * geometry.rowHeight}" font-family="Consolas,monospace" font-size="12" fill="#263445">${h(column.name.length > 25 ? column.name.slice(0, 24) + "…" : column.name)}${column.nullable ? " ?" : ""}</text><text x="${node.width - 10}" y="${geometry.columnOffset + 4 + index * geometry.rowHeight}" text-anchor="end" font-family="Consolas,monospace" font-size="12" fill="#465b70">${h(sqlType(column))}</text>`).join("")}<rect x="1" y="${footerY}" width="${node.width - 2}" height="${geometry.footerHeight - 1}" fill="#f6f8fa"/><text x="10" y="${node.height - 10}" font-size="10" fill="#465b70">${h(tr(groupOf(table).title))}</text><text x="${node.width - 10}" y="${node.height - 10}" text-anchor="end" font-size="10" fill="#465b70">${table.columns.length} ${h(tr("columns"))}${table.columns.length > 8 ? " · ↕" : ""}</text></g>`;
    })
    .join("");
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${layout.width}" height="${layout.height}" viewBox="0 0 ${layout.width} ${layout.height}" role="img" lang="${language}"><title>${h(schema.databaseName)} · ${language === "vi" ? "Sơ đồ đầy đủ các bảng và quan hệ" : "complete table relationship map"}</title><desc>${language === "vi" ? `Toàn bộ ${schema.tables.length} bảng và ${schema.foreignKeys.length} khóa ngoại. Mỗi bảng xem trước tối đa tám cột. Mở index.html để cuộn xem mọi cột hoặc complete.svg để in đầy đủ các cột.` : `All ${schema.tables.length} table objects and ${schema.foreignKeys.length} foreign keys. Compact cards preview up to eight columns. Open index.html to scroll every column, or complete.svg for all-column print output.`}</desc><defs><pattern id="grid" width="16" height="16" patternUnits="userSpaceOnUse"><circle cx="1" cy="1" r=".75" fill="#cbd3dd"/></pattern><marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto"><path d="M1 1L9 5L1 9" fill="none" stroke="#6c8daf" stroke-width="1.5"/></marker></defs><rect width="100%" height="100%" fill="#fbfbfc"/><text x="110" y="47" font-family="Segoe UI,Arial,sans-serif" font-size="20" font-weight="bold" fill="#263445">${h(schema.databaseName)} / ${language === "vi" ? "Sơ đồ cơ sở dữ liệu" : "SQL schema map"}</text><text x="110" y="72" font-family="Segoe UI,Arial,sans-serif" font-size="12" fill="#59697b">${h(tr(`${schema.tables.length} tables · ${schema.foreignKeys.length} foreign keys`))} · ${language === "vi" ? "Mở index.html để tương tác với sơ đồ" : "Open index.html for interactive exploration"}</text><g font-family="Segoe UI,Arial,sans-serif">${panels}${edges}${nodes}</g></svg>`;
}
