import { readFile, writeFile, mkdir } from "node:fs/promises";
import { resolve, join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";
import { escapeHtml as h } from "./model.mjs";
import { translate } from "./translations.mjs";

// Only editorial captions change. SQL identifiers, types, and definitions stay exact.
export function printImageSvg(svg, { title, subtitle, language }) {
  const bounds = svg.match(/viewBox="([\d.]+) ([\d.]+) ([\d.]+) ([\d.]+)"/);
  if (!bounds) throw new Error("Print SVG is missing its viewBox.");
  const width = Number(bounds[3]),
    height = Number(bounds[4]);
  let body = svg.slice(
    svg.indexOf(">", svg.indexOf("<svg")) + 1,
    svg.lastIndexOf("</svg>"),
  );
  if (language === "vi") {
    body = body.replace(
      /(<text\b[^>]*>)([^<]*)(<\/text>)/g,
      (match, open, text, close) => {
        const caption = /^\d+ columns$/.test(text)
          ? text.replace(" columns", " cột")
          : text === " [context]"
            ? " [TC]"
            : text;
        return open + caption + close;
      },
    );
  }
  return `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="${width}" height="${height + 92}" viewBox="0 0 ${width} ${height + 92}" lang="${language}" role="img"><title>${h(title)}</title><rect width="100%" height="100%" fill="white"/><g font-family="Segoe UI,Arial,sans-serif" fill="#203f5c"><text x="28" y="32" font-size="20" font-weight="bold">${h(title)}</text><text x="28" y="56" font-size="11">${h(subtitle)}</text></g><g transform="translate(0 92)">${body}</g></svg>`;
}

export async function exportImages(directory) {
  const output = resolve(directory),
    imageDirectory = join(output, "images");
  const manifest = JSON.parse(
    await readFile(join(output, "manifest.json"), "utf8"),
  );
  await mkdir(imageDirectory, { recursive: true });
  const entries = [];
  // Serial rendering bounds memory even for the complete 356-column print image.
  for (const language of ["en", "vi"]) {
    const suffix = language === "vi" ? ".vi" : "";
    const map = await readFile(join(output, `map${suffix}.svg`));
    const plans = [
      {
        name: `map${suffix}.png`,
        kind: "grouped-map",
        svg: map,
        tables: manifest.tables,
        foreignKeys: manifest.foreignKeys,
        columnCoverage: "up-to-eight-preview-columns-per-table",
      },
      {
        name: `map${suffix}.preview.png`,
        kind: "preview",
        svg: map,
        width: 1800,
        tables: manifest.tables,
        foreignKeys: manifest.foreignKeys,
        columnCoverage: "up-to-eight-preview-columns-per-table",
      },
      ...manifest.views
        .filter((view) => view.id !== "overview")
        .map((view) => ({
          name: `${view.id}${suffix}.png`,
          kind: view.id === "complete" ? "all-columns" : "function-detail",
          view,
          tables: view.tables,
          foreignKeys: view.foreignKeys,
          columnCoverage:
            view.id === "complete"
              ? "all-captured-columns"
              : "all-group-columns-with-primary-key-context-tables",
        })),
    ];
    for (const plan of plans) {
      let source = plan.svg;
      if (plan.view) {
        const full = plan.view.id === "complete";
        const title = `${manifest.databaseName} / ${
          full
            ? language === "vi"
              ? "Toàn bộ bảng và cột"
              : "All tables and columns"
            : translate(plan.view.title, language)
        }`;
        const subtitle = full
          ? translate(
              `${manifest.tables} tables · ${manifest.columns} columns`,
              language,
            ) + ` · ${manifest.foreignKeys} FK`
          : language === "vi"
            ? "Đầy đủ cột của nhóm. [TC]: bảng tham chiếu, chỉ hiện khóa chính. Quan hệ đi ra từ nhóm."
            : "All group columns; referenced context tables show primary keys. Outbound relationships from this group.";
        source = Buffer.from(
          printImageSvg(
            await readFile(join(output, `${plan.view.id}.svg`), "utf8"),
            { title, subtitle, language },
          ),
        );
      }
      let raster = sharp(source, { density: plan.width ? 72 : 144 });
      if (plan.width) raster = raster.resize({ width: plan.width });
      const info = await raster
        .flatten({ background: "#ffffff" })
        .png()
        .toFile(join(imageDirectory, plan.name));
      entries.push({
        file: plan.name,
        language,
        kind: plan.kind,
        width: info.width,
        height: info.height,
        tables: plan.tables,
        foreignKeys: plan.foreignKeys,
        columnCoverage: plan.columnCoverage,
      });
    }
  }
  await writeFile(
    join(imageDirectory, "manifest.json"),
    JSON.stringify(
      {
        capturedAt: manifest.capturedAt,
        databaseName: manifest.databaseName,
        tables: manifest.tables,
        columns: manifest.columns,
        foreignKeys: manifest.foreignKeys,
        renderer: "sharp",
        rendererVersion: sharp.versions.sharp,
        images: entries,
      },
      null,
      2,
    ) + "\n",
  );
  const groupRows = manifest.functionalGroups
    .map(
      (group) =>
        `| ${group.title} / ${group.titleVi} | ${group.tables} | [English](${group.id}.png) | [Tiếng Việt](${group.id}.vi.png) |`,
    )
    .join("\n");
  await writeFile(
    join(imageDirectory, "README.md"),
    `# Database images / Hình ảnh cơ sở dữ liệu

Snapshot / Chụp lúc: **${manifest.capturedAt}** (UTC). **${manifest.tables} tables / bảng · ${manifest.columns} columns / cột · ${manifest.foreignKeys} foreign keys / khóa ngoại.**

| Export / Bản xuất | English | Tiếng Việt |
| --- | --- | --- |
| Grouped map / Sơ đồ nhóm chức năng | [PNG](map.png) | [PNG](map.vi.png) |
| All columns / Đầy đủ cột | [PNG](complete.png) | [PNG](complete.vi.png) |

The grouped map retains every table and relationship, with up to eight preview columns per card. Use the **all-column image** for every captured column or the [interactive explorer](../index.html) to inspect and scroll tables. PNGs are static: selection and connection highlighting are available in the explorer.

Sơ đồ nhóm giữ toàn bộ bảng và quan hệ, mỗi bảng xem trước tối đa tám cột. Dùng **ảnh đầy đủ cột** để xem mọi cột hoặc [sơ đồ tương tác](../index.html) để chọn, cuộn bảng và làm nổi bật kết nối. Ảnh PNG là ảnh tĩnh.

## Function details / Chi tiết theo chức năng

These print views show every column in the selected group and its outgoing FKs; referenced tables outside the group show primary keys for context. Cross-group incoming FKs remain available in the complete map. These are print layouts, not crops of the grouped overview.

Các ảnh chi tiết hiển thị mọi cột trong nhóm và khóa ngoại đi ra từ nhóm; bảng ngoài nhóm chỉ hiện khóa chính để tham chiếu. Quan hệ đi vào từ nhóm khác có trong sơ đồ toàn bộ. Đây là bố cục in chi tiết, không phải ảnh cắt từ sơ đồ tổng quan.

| Function / Chức năng | Tables / Bảng | English | Tiếng Việt |
| --- | ---: | --- | --- |
${groupRows}

![Grouped map / Sơ đồ nhóm chức năng](map.vi.preview.png)

[Image dimensions and coverage / Kích thước và phạm vi](manifest.json) · [Regeneration guide / Hướng dẫn tạo lại](${relative(imageDirectory, fileURLToPath(new URL("README.md", import.meta.url))).replaceAll("\\", "/")})
`,
  );
  console.log(`Exported ${entries.length} PNG images to ${imageDirectory}`);
  return entries;
}

if (
  process.argv[1] &&
  resolve(process.argv[1]) === fileURLToPath(import.meta.url)
) {
  const inputIndex = process.argv.indexOf("--input");
  const directory =
    inputIndex >= 0
      ? process.argv[inputIndex + 1]
      : fileURLToPath(new URL("../../docs/database/current/", import.meta.url));
  if (!directory)
    throw new Error("Usage: node export-images.mjs [--input directory]");
  await exportImages(directory);
}
