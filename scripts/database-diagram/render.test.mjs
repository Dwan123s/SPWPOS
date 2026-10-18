import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { layoutSchema, segmentCrossesCard, geometry } from "./layout.mjs";
import { buildViewer } from "./viewer.mjs";
import { descriptionsVi, translate } from "./translations.mjs";
import { instance } from "@viz-js/viz";
import sharp from "sharp";
import { validateSchema, graphSpec, relationship, sqlType } from "./model.mjs";

test("PNG gallery covers both languages, all functions, and the complete captured catalog", async () => {
  const directory = new URL("../../docs/database/current/", import.meta.url);
  const schema = JSON.parse(
    readFileSync(new URL("schema.json", directory), "utf8"),
  );
  const catalog = JSON.parse(
    readFileSync(new URL("manifest.json", directory), "utf8"),
  );
  const gallery = JSON.parse(
    readFileSync(new URL("images/manifest.json", directory), "utf8"),
  );
  assert.equal(gallery.capturedAt, schema.capturedAt);
  assert.equal(
    gallery.columns,
    schema.tables.reduce((sum, table) => sum + table.columns.length, 0),
  );
  for (const language of ["en", "vi"]) {
    const images = gallery.images.filter(
      (image) => image.language === language,
    );
    const map = images.find((image) => image.kind === "grouped-map");
    const complete = images.find((image) => image.kind === "all-columns");
    for (const image of [map, complete]) {
      assert.equal(image.tables, schema.tables.length);
      assert.equal(image.foreignKeys, schema.foreignKeys.length);
    }
    assert.equal(complete.columnCoverage, "all-captured-columns");
    assert.equal(
      images.filter((image) => image.kind === "function-detail").length,
      catalog.functionalGroups.length,
    );
    const suffix = language === "vi" ? ".vi" : "";
    for (const group of catalog.functionalGroups)
      assert.ok(
        images.some((image) => image.file === `${group.id}${suffix}.png`),
      );
    for (const image of images) {
      const pixels = await sharp(
        readFileSync(new URL(`images/${image.file}`, directory)),
      ).metadata();
      assert.equal(pixels.format, "png");
      assert.equal(pixels.width, image.width);
      assert.equal(pixels.height, image.height);
      assert.ok(pixels.width >= 1000 && pixels.height >= 500, image.file);
    }
  }
});

const column = (name, nullable = false, primaryKey = false) => ({
  id: name,
  name,
  nullable,
  primaryKey,
  type: "uniqueidentifier",
  identity: false,
  computed: false,
});
function fixture() {
  return {
    databaseName: "Test",
    capturedAt: "2026-10-18T14:00:00Z",
    tables: [
      {
        id: 1,
        schema: "dbo",
        name: "WorkTasks",
        columns: [
          column("UserId"),
          column("Id", false, true),
          column("Name & notes"),
        ],
        keys: [],
      },
      {
        id: 2,
        schema: "dbo",
        name: "WorkSessions",
        columns: [
          column("Id", false, true),
          column("UserId"),
          column("TaskId", true),
        ],
        keys: [{ name: "PK", unique: true, columns: [{ name: "Id" }] }],
      },
    ],
    foreignKeys: [
      {
        id: 3,
        name: "FK_Composite",
        childTableId: 2,
        parentTableId: 1,
        onDelete: "NO_ACTION",
        columns: [
          { childColumn: "UserId", parentColumn: "UserId" },
          { childColumn: "TaskId", parentColumn: "Id" },
        ],
      },
    ],
  };
}

test("composite FK stays one edge, renders non-key columns, and escapes SQL identifiers", async () => {
  const schema = fixture(),
    graph = graphSpec(schema, { full: true });
  assert.deepEqual(graph.foreignKeyIds, [3]);
  const rendered = (await instance()).render(graph.dot, { format: "svg" });
  assert.equal(rendered.status, "success");
  assert.deepEqual(
    rendered.errors.filter((error) => error.level === "error"),
    [],
  );
  assert.match(rendered.output, /Name &amp; notes/);
  assert.match(rendered.output, /UserId/);
  assert.match(rendered.output, /TaskId/);
  assert.equal((rendered.output.match(/class="edge"/g) || []).length, 1);
});

test("nullable composite keys and unfiltered uniqueness produce correct declared cardinality", () => {
  const schema = fixture(),
    fk = schema.foreignKeys[0],
    child = schema.tables[1];
  assert.deepEqual(relationship(schema, fk), { child: "0..N", parent: "0..1" });
  child.keys.push({
    unique: true,
    columns: [{ name: "UserId" }, { name: "TaskId" }],
  });
  assert.deepEqual(relationship(schema, fk), { child: "0..1", parent: "0..1" });
  child.keys[1].filtered = true;
  assert.equal(relationship(schema, fk).child, "0..N");
  child.keys[1].filtered = false;
  child.keys[1].disabled = true;
  assert.equal(relationship(schema, fk).child, "0..N");
  child.columns.find((item) => item.name === "TaskId").nullable = false;
  assert.equal(relationship(schema, fk).parent, "1");
});

test("unresolved catalog references fail instead of silently omitting a relationship", () => {
  const schema = fixture();
  schema.foreignKeys[0].columns[1].parentColumn = "Missing";
  assert.throws(() => validateSchema(schema), /Unresolved column/);
});

test("SQL Server types preserve Unicode lengths, MAX, decimal precision and time scale", () => {
  for (const [input, expected] of [
    [{ type: "nvarchar", maxLength: 400 }, "nvarchar(200)"],
    [{ type: "nvarchar", maxLength: -1 }, "nvarchar(max)"],
    [{ type: "decimal", precision: 18, scale: 4 }, "decimal(18,4)"],
    [{ type: "datetimeoffset", scale: 7 }, "datetimeoffset(7)"],
  ])
    assert.equal(sqlType(input), expected);
});

test("generated complete live diagram contains exactly every captured table, FK and column", () => {
  const root = new URL("../../docs/database/current/", import.meta.url);
  const schema = JSON.parse(readFileSync(new URL("schema.json", root), "utf8"));
  const svg = readFileSync(new URL("complete.svg", root), "utf8").replace(
    /&#(\d+);/g,
    (_, number) => String.fromCodePoint(Number(number)),
  );
  const tables = [...svg.matchAll(/<g id="entity-(\d+)" class="node"/g)]
    .map((match) => Number(match[1]))
    .sort((a, b) => a - b);
  const edges = [...svg.matchAll(/<g id="fk-(\d+)" class="edge"/g)]
    .map((match) => Number(match[1]))
    .sort((a, b) => a - b);
  assert.deepEqual(
    tables,
    schema.tables.map((table) => table.id).sort((a, b) => a - b),
  );
  assert.deepEqual(
    edges,
    schema.foreignKeys.map((fk) => fk.id).sort((a, b) => a - b),
  );
  const names = new Set(
    [...svg.matchAll(/<text[^>]*>([^<]*)<\/text>/g)].map((match) => match[1]),
  );
  for (const table of schema.tables)
    for (const column of table.columns) {
      assert.ok(
        names.has(column.name) || names.has(`${column.name} ?`),
        `${table.name}.${column.name} is missing`,
      );
    }
});

const liveRoot = new URL("../../docs/database/current/", import.meta.url);
const liveSchema = JSON.parse(
  readFileSync(new URL("schema.json", liveRoot), "utf8"),
);

function openViewer(
  html = readFileSync(new URL("index.html", liveRoot), "utf8"),
  options = {},
) {
  const require = createRequire(
    new URL("../../frontend/package.json", import.meta.url),
  );
  const { JSDOM } = require("jsdom");
  let clock = 0,
    frameId = 0;
  const frames = new Map();
  const dom = new JSDOM(html, {
    runScripts: "dangerously",
    pretendToBeVisual: true,
    beforeParse(window) {
      // Browser layout, input capture, media preferences and animation clocks
      // are system boundaries absent from jsdom. Run the real viewer script.
      window.matchMedia = (query) => ({
        matches: options.reducedMotion !== false,
        media: query,
      });
      window.requestAnimationFrame = (callback) => {
        frames.set(++frameId, callback);
        return frameId;
      };
      window.cancelAnimationFrame = (id) => frames.delete(id);
      Object.defineProperty(window.performance, "now", { value: () => clock });
      Object.defineProperty(window.HTMLElement.prototype, "clientWidth", {
        get: () => 1200,
      });
      Object.defineProperty(window.HTMLElement.prototype, "clientHeight", {
        get() {
          return this.classList.contains("entity-columns")
            ? parseFloat(this.closest(".entity").style.height) -
                geometry.headerHeight -
                geometry.footerHeight -
                2
            : 720;
        },
      });
      Object.defineProperty(window.HTMLElement.prototype, "scrollWidth", {
        get() {
          return this.id === "diagram"
            ? parseFloat(
                window.document.getElementById("world-space").style.width,
              ) || this.clientWidth
            : this.clientWidth;
        },
      });
      Object.defineProperty(window.HTMLElement.prototype, "scrollHeight", {
        get() {
          if (this.id === "diagram")
            return (
              parseFloat(
                window.document.getElementById("world-space").style.height,
              ) || this.clientHeight
            );
          if (this.classList.contains("entity-columns"))
            return (
              this.querySelectorAll(".column-row").length * geometry.rowHeight
            );
          return this.clientHeight;
        },
      });
      const positions = new WeakMap();
      for (const [field, total, visible] of [
        ["scrollLeft", "scrollWidth", "clientWidth"],
        ["scrollTop", "scrollHeight", "clientHeight"],
      ]) {
        Object.defineProperty(window.HTMLElement.prototype, field, {
          get() {
            return positions.get(this)?.[field] || 0;
          },
          set(value) {
            const stored = positions.get(this) || {};
            stored[field] = Math.max(
              0,
              Math.min(
                Math.max(0, this[total] - this[visible]),
                Number(value) || 0,
              ),
            );
            positions.set(this, stored);
          },
        });
      }
      window.HTMLElement.prototype.scrollTo = function (first, top) {
        this.scrollLeft = typeof first === "object" ? first.left : first;
        this.scrollTop = typeof first === "object" ? first.top : top;
      };
      const captures = new WeakMap();
      window.HTMLElement.prototype.setPointerCapture = function (id) {
        captures.set(this, id);
      };
      window.HTMLElement.prototype.hasPointerCapture = function (id) {
        return captures.has(this) && captures.get(this) === id;
      };
      window.HTMLElement.prototype.releasePointerCapture = function () {
        captures.delete(this);
      };
    },
  });
  dom.advanceAnimation = (elapsed) => {
    clock += elapsed;
    const pending = [...frames.values()];
    frames.clear();
    pending.forEach((callback) => callback(clock));
  };
  return dom;
}

test("every live connector avoids every table rectangle, including its own endpoints", () => {
  const layout = layoutSchema(liveSchema);
  assert.equal(layout.nodes.length, liveSchema.tables.length);
  assert.equal(layout.edges.length, liveSchema.foreignKeys.length);
  assert.equal(
    new Set(layout.nodes.map((node) => node.x + ":" + node.y)).size,
    liveSchema.tables.length,
  );
  assert.ok(layout.nodes.every((node) => node.height <= geometry.cardHeight));
  assert.ok(new Set(layout.nodes.map((node) => node.height)).size > 1);
  assert.ok(
    layout.width / layout.height > 0.8 && layout.width / layout.height < 1.6,
  );
  const col = (name) =>
    layout.nodes.find(
      (node) =>
        liveSchema.tables.find((table) => table.id === node.id).name === name,
    ).col;
  assert.ok(col("AspNetUsers") < col("WorkTasks"));
  assert.ok(col("WorkTasks") < col("WorkSessions"));
  const session = layout.nodes.find(
    (node) =>
      liveSchema.tables.find((table) => table.id === node.id).name ===
      "WorkSessions",
  );
  const segment = layout.nodes.find(
    (node) =>
      liveSchema.tables.find((table) => table.id === node.id).name ===
      "execution_segments",
  );
  assert.equal(session.group, segment.group);
  assert.ok(session.row < segment.row || session.col < segment.col);
  for (const edge of layout.edges)
    for (const [x, y] of edge.points)
      assert.ok(x >= 0 && y >= 0 && x <= layout.width && y <= layout.height);
  for (const edge of layout.edges)
    for (let i = 1; i < edge.points.length; i++)
      for (const node of layout.nodes) {
        assert.equal(
          segmentCrossesCard([edge.points[i - 1], edge.points[i]], node),
          false,
          `FK ${edge.id} intersects table ${node.id}`,
        );
      }
});

test("self references and parallel composite constraints retain separate routed edges", () => {
  const schema = fixture();
  schema.foreignKeys.push({
    ...schema.foreignKeys[0],
    id: 4,
    name: "Self",
    parentTableId: 2,
    columns: [{ childColumn: "TaskId", parentColumn: "Id" }],
  });
  schema.foreignKeys.push({
    ...schema.foreignKeys[0],
    id: 5,
    name: "Parallel",
  });
  const layout = layoutSchema(schema);
  assert.equal(layout.edges.length, 3);
  assert.equal(
    new Set(layout.edges.map((edge) => JSON.stringify(edge.points))).size,
    3,
  );
  for (const edge of layout.edges)
    for (let i = 1; i < edge.points.length; i++)
      for (const node of layout.nodes) {
        assert.equal(
          segmentCrossesCard([edge.points[i - 1], edge.points[i]], node),
          false,
        );
      }
});

test("the default map shows every connection without an automatic highlight", () => {
  const dom = openViewer();
  try {
    const { document, getComputedStyle } = dom.window;
    assert.equal(
      document.querySelectorAll(".edge").length,
      liveSchema.foreignKeys.length,
    );
    assert.equal(
      document.querySelectorAll(".edge.selected,.edge.unrelated").length,
      0,
    );
    assert.equal(document.querySelectorAll(".entity.selected").length, 0);
    for (const line of document.querySelectorAll(".edge-line"))
      assert.equal(getComputedStyle(line).opacity, "1");
  } finally {
    dom.window.close();
  }
});

test("functional panels contain every table exactly once, with clear headings and intact connections", () => {
  const layout = layoutSchema(liveSchema);
  assert.equal(layout.groups.length, 7);
  const membership = layout.groups.flatMap((group) => group.tableIds);
  assert.deepEqual(
    [...membership].sort(),
    liveSchema.tables.map((table) => table.id).sort(),
  );
  assert.equal(new Set(membership).size, liveSchema.tables.length);
  for (const group of layout.groups) {
    for (const id of group.tableIds) {
      const node = layout.nodes.find((node) => node.id === id);
      assert.equal(node.group, group.id);
      assert.ok(
        node.x >= group.x && node.x + node.width <= group.x + group.width,
      );
      assert.ok(
        node.y >= group.y + 82 &&
          node.y + node.height <= group.y + group.height,
      );
      assert.ok(
        descriptionsVi[liveSchema.tables.find((table) => table.id === id).name],
      );
    }
    const heading = {
      x: group.x + 20,
      y: group.y + 14,
      width: geometry.cardWidth,
      height: 54,
    };
    for (const edge of layout.edges)
      for (let i = 1; i < edge.points.length; i++)
        assert.equal(
          segmentCrossesCard([edge.points[i - 1], edge.points[i]], heading),
          false,
          "Connector crosses " + group.id + " heading",
        );
  }
  assert.equal(layout.edges.length, liveSchema.foreignKeys.length);
});

test("Vietnamese translates controls, group labels, descriptions and detail tabs without moving or rebuilding the selection", () => {
  const dom = openViewer();
  try {
    const { document, Event } = dom.window;
    const task = liveSchema.tables.find((table) => table.name === "WorkTasks");
    document.querySelector("#table-" + task.id + " .entity-heading").click();
    document.querySelector('[data-tab="columns"]').click();
    const expanded = document.querySelector(".column-detail");
    expanded.open = true;
    const host = document.getElementById("diagram"),
      camera = [
        host.scrollLeft,
        host.scrollTop,
        document.getElementById("world").style.transform,
      ];
    const language = document.getElementById("language");
    language.value = "vi";
    language.dispatchEvent(new Event("change"));
    assert.equal(document.documentElement.lang, "vi");
    assert.equal(document.getElementById("fit").textContent, "Xem toàn bộ");
    assert.equal(
      document.querySelector('.functional-heading [data-group-focus="tasks"]')
        .textContent,
      "Công việc và danh mục",
    );
    assert.equal(
      document.querySelector(".detail-description").textContent,
      descriptionsVi.WorkTasks,
    );
    assert.equal(
      document.querySelector('[data-tab="columns"]').textContent,
      "Cột",
    );
    assert.match(
      document.querySelector(".column-detail").textContent,
      /Giá trị mặc định/,
    );
    assert.equal(document.querySelector(".column-detail"), expanded);
    assert.equal(expanded.open, true);
    assert.deepEqual(
      [
        host.scrollLeft,
        host.scrollTop,
        document.getElementById("world").style.transform,
      ],
      camera,
    );
    assert.equal(
      document.querySelector(".detail-title").textContent,
      "WorkTasks",
    );
    assert.equal(
      document.getElementById("export-map").getAttribute("href"),
      "map.vi.svg",
    );
    assert.match(document.getElementById("help").textContent, /Mọi kết nối/);
    assert.equal(
      document.querySelectorAll(".entity").length,
      liveSchema.tables.length,
    );
    for (const table of liveSchema.tables)
      assert.equal(
        document.querySelector("#table-" + table.id + " .entity-heading span")
          .textContent,
        table.schema + "." + table.name,
      );
    document.querySelector('[data-tab="keys"]').click();
    assert.match(
      document.getElementById("detail-content").textContent,
      /Khóa chính/,
    );
    language.value = "en";
    language.dispatchEvent(new Event("change"));
    assert.equal(document.getElementById("fit").textContent, "Fit all");
    assert.equal(
      document.querySelector(".detail-description").textContent,
      JSON.parse(
        document.getElementById("schema-data").textContent,
      ).tables.find((table) => table.id === task.id).description,
    );
    assert.equal(
      document.querySelector('[data-tab="keys"]').textContent,
      "Keys",
    );
    assert.equal(
      document.getElementById("export-map").getAttribute("href"),
      "map.svg",
    );
  } finally {
    dom.window.close();
  }
});

test("clicking a functional group focuses that area while keeping cross-group connections and all tables", () => {
  const dom = openViewer();
  try {
    const { document, Event, MouseEvent } = dom.window;
    const language = document.getElementById("language");
    language.value = "vi";
    language.dispatchEvent(new Event("change"));
    const heading = document.querySelector(
      '.functional-heading [data-group-focus="tasks"]',
    );
    heading.dispatchEvent(
      new MouseEvent("pointerdown", { bubbles: true, button: 0 }),
    );
    assert.equal(
      document.getElementById("diagram").classList.contains("is-dragging"),
      false,
    );
    heading.click();
    assert.equal(document.getElementById("module").value, "tasks");
    assert.ok(
      document
        .querySelector('[data-function-group="tasks"]')
        .classList.contains("focused"),
    );
    assert.equal(
      document.querySelectorAll(".entity").length,
      liveSchema.tables.length,
    );
    assert.equal(
      document.querySelectorAll(".edge").length,
      liveSchema.foreignKeys.length,
    );
    assert.ok(document.querySelectorAll(".edge.selected").length > 0);
    assert.ok(document.querySelectorAll(".edge.unrelated").length > 0);
    assert.match(
      document.getElementById("map-status").textContent,
      /Công việc và danh mục.*giữ toàn bộ bảng/,
    );
    document.getElementById("clear").click();
    assert.equal(document.querySelectorAll(".edge.unrelated").length, 0);
  } finally {
    dom.window.close();
  }
});

test("Vietnamese leaves SQL identifiers and literal definitions unchanged, including words also used by the UI", async () => {
  const schema = fixture();
  schema.tables.push({
    id: 10,
    schema: "dbo",
    name: "Columns",
    columns: [
      { ...column("Keys"), defaultDefinition: "Yes", computedDefinition: "No" },
    ],
    keys: [
      { name: "Primary key", primaryKey: true, columns: [{ name: "Keys" }] },
    ],
  });
  const { html } = await buildViewer(schema),
    dom = openViewer(html);
  try {
    const { document, Event } = dom.window;
    const language = document.getElementById("language");
    language.value = "vi";
    language.dispatchEvent(new Event("change"));
    document.querySelector("#table-10 .entity-heading").click();
    assert.equal(
      document.querySelector(".detail-title").textContent,
      "Columns",
    );
    assert.equal(
      document.querySelector("#table-10 .column-name").textContent,
      "Keys",
    );
    assert.match(
      document.querySelector(".detail-description").textContent,
      /Chưa có tài liệu/,
    );
    document.querySelector('[data-tab="columns"]').click();
    assert.equal(
      document.querySelector(".column-detail summary code").textContent,
      "Keys",
    );
    assert.deepEqual(
      [...document.querySelectorAll("[data-raw]")].map(
        (element) => element.textContent,
      ),
      ["Yes", "No"],
    );
    assert.equal(
      document.querySelector(".column-detail dt").textContent,
      "Thứ tự",
    );
    document.querySelector('[data-tab="keys"]').click();
    assert.equal(
      document.querySelector(".key-detail code").textContent,
      "Primary key",
    );
    assert.equal(
      document.querySelector(".key-detail small").textContent,
      "Keys",
    );
    const svg = readFileSync(new URL("map.vi.svg", liveRoot), "utf8");
    assert.match(svg, /Tài khoản và bảo mật/);
    assert.match(svg, /Công việc và danh mục/);
    assert.match(svg, /dbo.WorkTasks/);
    assert.equal((svg.match(/data-function-group=/g) || []).length, 7);
  } finally {
    dom.window.close();
  }
});

test("scrolling reaches the final map row and final column at different zoom levels", () => {
  const dom = openViewer();
  try {
    const { document, getComputedStyle, Event } = dom.window;
    const host = document.getElementById("diagram"),
      world = document.getElementById("world");
    const layout = JSON.parse(
      document.getElementById("schema-data").textContent,
    ).layout;
    document.getElementById("actual").click();
    for (let level = 0; level < 2; level++) {
      const scale = Number(world.style.transform.match(/scale\(([^)]+)\)/)[1]);
      assert.ok(
        Math.abs(
          host.scrollHeight - (layout.height * scale + host.clientHeight),
        ) < 0.01,
      );
      const lastBottom =
        parseFloat(world.style.top) +
        Math.max(...layout.nodes.map((node) => node.y + node.height)) * scale;
      host.scrollTo({
        left: host.scrollWidth,
        top: lastBottom - host.clientHeight + 24,
      });
      assert.ok(
        Math.abs(lastBottom - host.scrollTop - (host.clientHeight - 24)) < 0.01,
      );
      assert.ok(host.scrollTop > 0);
      document.getElementById("minus").click();
    }
    const task = liveSchema.tables.find((table) => table.name === "WorkTasks");
    const columns = document.querySelector(
      "#table-" + task.id + " .entity-columns",
    );
    columns.scrollTop = Number.MAX_SAFE_INTEGER;
    columns.dispatchEvent(new Event("scroll"));
    assert.equal(
      columns.scrollTop + columns.clientHeight,
      columns.scrollHeight,
    );
    assert.equal(getComputedStyle(columns).overscrollBehavior, "auto");
    assert.equal(
      getComputedStyle(document.querySelector(".workspace")).gridTemplateRows,
      "minmax(0, 1fr)",
    );
    for (const area of [
      ".navigator",
      ".inspector",
      "#inspector-body",
      ".table-list",
    ])
      assert.equal(
        parseFloat(getComputedStyle(document.querySelector(area)).minHeight),
        0,
      );
  } finally {
    dom.window.close();
  }
});

test("canvas table clicks animate smoothly to the center and a new selection replaces the animation", () => {
  const dom = openViewer(undefined, { reducedMotion: false });
  try {
    const { document } = dom.window;
    const host = document.getElementById("diagram"),
      world = document.getElementById("world");
    const layout = JSON.parse(
      document.getElementById("schema-data").textContent,
    ).layout;
    const [first, second] = [layout.nodes[0], layout.nodes.at(-1)];
    const initial = [host.scrollLeft, host.scrollTop, world.style.transform];
    document.querySelector("#table-" + first.id + " .entity-heading").click();
    assert.deepEqual(
      [host.scrollLeft, host.scrollTop, world.style.transform],
      initial,
    );
    dom.advanceAnimation(180);
    assert.notDeepEqual(
      [host.scrollLeft, host.scrollTop, world.style.transform],
      initial,
    );
    const halfway = [host.scrollLeft, host.scrollTop, world.style.transform];
    document.querySelector("#table-" + second.id + " .entity-heading").click();
    assert.deepEqual(
      [host.scrollLeft, host.scrollTop, world.style.transform],
      halfway,
    );
    dom.advanceAnimation(230);
    dom.advanceAnimation(230);
    const scale = Number(world.style.transform.match(/scale\(([^)]+)\)/)[1]);
    assert.equal(scale, 1);
    assert.ok(
      Math.abs(
        parseFloat(world.style.left) +
          (second.x + second.width / 2) * scale -
          host.scrollLeft -
          host.clientWidth / 2,
      ) < 0.01,
    );
    assert.ok(
      Math.abs(
        parseFloat(world.style.top) +
          (second.y + second.height / 2) * scale -
          host.scrollTop -
          host.clientHeight / 2,
      ) < 0.01,
    );
    assert.ok(
      document
        .getElementById("table-" + second.id)
        .classList.contains("selected"),
    );
  } finally {
    dom.window.close();
  }
});

test("edge tables can center immediately when reduced motion is requested", () => {
  const dom = openViewer();
  try {
    const { document } = dom.window;
    const layout = JSON.parse(
      document.getElementById("schema-data").textContent,
    ).layout;
    const node = layout.nodes.reduce((best, node) =>
      node.x < best.x ? node : best,
    );
    document.querySelector("#table-" + node.id + " .entity-heading").click();
    const host = document.getElementById("diagram"),
      world = document.getElementById("world");
    assert.ok(
      Math.abs(
        parseFloat(world.style.left) +
          node.x +
          node.width / 2 -
          host.scrollLeft -
          host.clientWidth / 2,
      ) < 0.01,
    );
    const settled = [host.scrollLeft, host.scrollTop];
    dom.advanceAnimation(1000);
    assert.deepEqual([host.scrollLeft, host.scrollTop], settled);
  } finally {
    dom.window.close();
  }
});

test("Shift and left-drag pan over tables without selecting them on release", () => {
  const dom = openViewer();
  try {
    const { document, MouseEvent, KeyboardEvent } = dom.window;
    const host = document.getElementById("diagram"),
      heading = document.querySelector(".entity-heading");
    document.getElementById("actual").click();
    const start = [host.scrollLeft, host.scrollTop];
    const pointer = (type, target, params) => {
      const event = new MouseEvent(type, {
        bubbles: true,
        cancelable: true,
        button: 0,
        ...params,
      });
      Object.defineProperty(event, "pointerId", { value: 7 });
      target.dispatchEvent(event);
    };
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Shift" }));
    pointer("pointerdown", heading, {
      shiftKey: true,
      clientX: 300,
      clientY: 300,
    });
    pointer("pointermove", host, {
      shiftKey: true,
      clientX: 220,
      clientY: 180,
    });
    assert.equal(host.scrollLeft, start[0] + 80);
    assert.equal(host.scrollTop, start[1] + 120);
    assert.ok(host.classList.contains("is-dragging"));
    pointer("pointerup", host, { clientX: 220, clientY: 180 });
    heading.dispatchEvent(
      new MouseEvent("click", { bubbles: true, cancelable: true, detail: 1 }),
    );
    assert.equal(document.querySelectorAll(".entity.selected").length, 0);
    assert.equal(host.classList.contains("is-dragging"), false);
    document.dispatchEvent(new KeyboardEvent("keyup", { key: "Shift" }));
    assert.equal(host.classList.contains("shift-pan"), false);
    heading.click();
    assert.equal(document.querySelectorAll(".entity.selected").length, 1);
  } finally {
    dom.window.close();
  }
});

test("larger schema text and primary labels maintain readable contrast", () => {
  const dom = openViewer();
  try {
    const { document, getComputedStyle } = dom.window;
    const luminance = (color) => {
      const channels = color
        .match(/[\d.]+/g)
        .slice(0, 3)
        .map(Number)
        .map((value) => {
          const s = value / 255;
          return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
        });
      return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
    };
    for (const selector of [".entity-heading", ".column-type"]) {
      const style = getComputedStyle(document.querySelector(selector));
      const foreground = luminance(style.color),
        background =
          selector === ".entity-heading" ? luminance(style.backgroundColor) : 1;
      assert.ok(
        (Math.max(foreground, background) + 0.05) /
          (Math.min(foreground, background) + 0.05) >=
          4.5,
        selector + " contrast",
      );
    }
    assert.ok(
      parseFloat(
        getComputedStyle(document.querySelector(".column-row")).fontSize,
      ) >= 12,
    );
    assert.ok(
      parseFloat(getComputedStyle(document.documentElement).fontSize) >= 14,
    );
  } finally {
    dom.window.close();
  }
});

test("Ctrl-click accumulates highlighted FKs, dims the others and toggles connections off", () => {
  const dom = openViewer();
  try {
    const { document, MouseEvent, getComputedStyle } = dom.window;
    const [first, second] = [...document.querySelectorAll(".edge")];
    first.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    second.dispatchEvent(
      new MouseEvent("click", { bubbles: true, ctrlKey: true }),
    );
    assert.equal(document.querySelectorAll(".edge.selected").length, 2);
    assert.equal(
      document.querySelectorAll(".edge.unrelated").length,
      liveSchema.foreignKeys.length - 2,
    );
    assert.match(
      document.querySelector(".trace-summary").textContent,
      /2 connections highlighted/,
    );
    assert.equal(
      getComputedStyle(document.querySelector(".edge.unrelated .edge-line"))
        .opacity,
      "0.25",
    );
    first.dispatchEvent(
      new MouseEvent("click", { bubbles: true, metaKey: true }),
    );
    assert.equal(document.querySelectorAll(".edge.selected").length, 1);
    assert.equal(document.querySelector(".edge.selected").id, second.id);
    second.dispatchEvent(
      new MouseEvent("click", { bubbles: true, ctrlKey: true }),
    );
    assert.equal(
      document.querySelectorAll(".edge.selected,.edge.unrelated").length,
      0,
    );
    assert.equal(
      document.querySelectorAll(".edge").length,
      liveSchema.foreignKeys.length,
    );
  } finally {
    dom.window.close();
  }
});

test("scrolling table columns keeps side connectors orthogonal and outside every card", async () => {
  const schema = fixture();
  schema.tables[1].columns.push(
    ...Array.from({ length: 15 }, (_, index) => column("Extra" + index)),
  );
  const { html, layout } = await buildViewer(schema),
    dom = openViewer(html);
  try {
    const { document, Event } = dom.window;
    const line = document.querySelector("#fk-3 .edge-line");
    const before = line.getAttribute("points");
    const scroller = document.querySelector("#table-2 .entity-columns");
    scroller.scrollTop = 44;
    scroller.dispatchEvent(new Event("scroll"));
    assert.notEqual(line.getAttribute("points"), before);
    const points = line
      .getAttribute("points")
      .split(" ")
      .map((point) => point.split(",").map(Number));
    for (let i = 1; i < points.length; i++)
      for (const node of layout.nodes)
        assert.equal(
          segmentCrossesCard([points[i - 1], points[i]], node),
          false,
        );
  } finally {
    dom.window.close();
  }
});

test("viewer retains every live table and column while searching and focusing a domain", () => {
  const dom = openViewer();
  try {
    const { document, Event } = dom.window;
    assert.equal(
      document.querySelectorAll(".entity").length,
      liveSchema.tables.length,
    );
    for (const table of liveSchema.tables) {
      const card = document.getElementById("table-" + table.id);
      assert.deepEqual(
        [...card.querySelectorAll("[data-column]")].map(
          (row) => row.dataset.column,
        ),
        table.columns.map((column) => column.name),
      );
    }
    const search = document.getElementById("search");
    search.value = "WorkTasks";
    search.dispatchEvent(new Event("input"));
    assert.equal(document.querySelectorAll(".table-link").length, 1);
    search.value = "no-such-table";
    search.dispatchEvent(new Event("input"));
    assert.match(
      document.querySelector(".empty-search").textContent,
      /No matching/,
    );
    assert.equal(
      document.querySelectorAll(".entity:not([hidden])").length,
      liveSchema.tables.length,
    );
    search.value = "";
    search.dispatchEvent(new Event("input"));
    const module = document.getElementById("module");
    module.value = "tasks";
    module.dispatchEvent(new Event("change"));
    assert.equal(
      document.querySelectorAll(".entity:not([hidden])").length,
      liveSchema.tables.length,
    );
    assert.equal(
      document.querySelectorAll(".edge:not([hidden])").length,
      liveSchema.foreignKeys.length,
    );
    assert.ok(document.querySelectorAll(".entity.selected").length > 0);
    document.getElementById("clear").click();
    assert.equal(document.querySelectorAll(".entity.unrelated").length, 0);
    assert.equal(document.querySelectorAll(".edge.unrelated").length, 0);
    const ids = [...document.querySelectorAll("[id]")].map(
      (element) => element.id,
    );
    assert.equal(new Set(ids).size, ids.length);
  } finally {
    dom.window.close();
  }
});

test("table selection shows purpose and all incoming and outgoing connections with exact highlighting", () => {
  const dom = openViewer();
  try {
    const { document } = dom.window;
    const table = liveSchema.tables.find((table) => table.name === "WorkTasks");
    document.querySelector(`#table-${table.id} .entity-heading`).click();
    assert.equal(
      document.querySelector(".detail-title").textContent,
      table.name,
    );
    assert.match(
      document.querySelector(".detail-description").textContent,
      /deadlines/,
    );
    const fks = liveSchema.foreignKeys.filter(
      (fk) => fk.childTableId === table.id || fk.parentTableId === table.id,
    );
    assert.deepEqual(
      [...document.querySelectorAll(".edge.selected")]
        .map((edge) => Number(edge.dataset.fk))
        .sort(),
      fks.map((fk) => fk.id).sort(),
    );
    assert.equal(
      document.querySelectorAll(".relationship-card").length,
      fks.length,
    );
    const expectedRelated = new Set(
      fks
        .flatMap((fk) => [fk.childTableId, fk.parentTableId])
        .filter((id) => id !== table.id),
    );
    assert.deepEqual(
      new Set(
        [...document.querySelectorAll(".entity.related")].map((card) =>
          Number(card.dataset.table),
        ),
      ),
      expectedRelated,
    );
    document.querySelector('[data-tab="columns"]').click();
    assert.equal(
      document.querySelectorAll(".column-detail").length,
      table.columns.length,
    );
    document.querySelector('[data-tab="keys"]').click();
    assert.equal(
      document.querySelectorAll(".key-detail").length,
      table.keys.length,
    );
  } finally {
    dom.window.close();
  }
});

test("tracing a constraint highlights just that edge, its endpoint columns and related table", () => {
  const dom = openViewer();
  try {
    const { document, KeyboardEvent } = dom.window;
    const fk = liveSchema.foreignKeys.find(
      (fk) => fk.childTableId !== fk.parentTableId,
    );
    const edge = document.getElementById("fk-" + fk.id);
    edge.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", bubbles: true }),
    );
    assert.equal(document.querySelectorAll(".edge.selected").length, 1);
    assert.equal(document.querySelector(".edge.selected").id, edge.id);
    assert.ok(
      document
        .getElementById("table-" + fk.childTableId)
        .classList.contains("selected"),
    );
    assert.ok(
      document
        .getElementById("table-" + fk.parentTableId)
        .classList.contains("related"),
    );
    for (const [id, field] of [
      [fk.childTableId, "childColumn"],
      [fk.parentTableId, "parentColumn"],
    ]) {
      const actual = [
        ...document
          .getElementById("table-" + id)
          .querySelectorAll(".fk-column"),
      ].map((row) => row.dataset.column);
      assert.deepEqual(
        new Set(actual),
        new Set(fk.columns.map((pair) => pair[field])),
      );
    }
    document.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Escape", bubbles: true }),
    );
    assert.equal(document.querySelectorAll(".edge.selected").length, 0);
    assert.equal(document.querySelectorAll(".fk-column").length, 0);
  } finally {
    dom.window.close();
  }
});

test("zoom, locate, fit, help, and keyboard detail tabs update the actual generated viewer", () => {
  const dom = openViewer();
  try {
    const { document, KeyboardEvent } = dom.window;
    const before = document.getElementById("world-space").style.width;
    document.getElementById("plus").click();
    assert.ok(
      parseFloat(document.getElementById("world-space").style.width) >
        parseFloat(before),
    );
    document.getElementById("fit").click();
    assert.ok(
      parseFloat(document.getElementById("zoom-value").textContent) < 80,
    );
    const table = liveSchema.tables.find((table) => table.name === "WorkTasks");
    document.querySelector(`.table-link[data-select="${table.id}"]`).click();
    assert.equal(
      document.querySelector(".detail-title").textContent,
      table.name,
    );
    assert.ok(
      parseFloat(document.getElementById("zoom-value").textContent) >= 55,
    );
    document
      .querySelector('[data-tab="relationships"]')
      .dispatchEvent(
        new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }),
      );
    assert.equal(
      document
        .querySelector('[data-tab="columns"]')
        .getAttribute("aria-selected"),
      "true",
    );
    assert.equal(document.activeElement.id, "tab-columns");
    document.getElementById("help-toggle").click();
    assert.equal(document.getElementById("help").hidden, false);
    assert.equal(
      document.getElementById("help-toggle").getAttribute("aria-expanded"),
      "true",
    );
  } finally {
    dom.window.close();
  }
});

test("isolated tables, unknown names and HTML-like identifiers remain accessible without inferred relationships", async () => {
  const schema = fixture();
  schema.tables.push({
    id: 10,
    schema: "dbo",
    name: "<Isolated & custom>",
    columns: [column("x < y")],
    keys: [],
  });
  const { html } = await buildViewer(schema);
  const dom = openViewer(html);
  try {
    const { document } = dom.window;
    document.querySelector("#table-10 .entity-heading").click();
    assert.equal(
      document.querySelector(".detail-title").textContent,
      "<Isolated & custom>",
    );
    assert.equal(document.querySelectorAll(".edge.selected").length, 0);
    assert.equal(document.querySelectorAll(".relationship-card").length, 0);
    assert.equal(document.querySelectorAll(".entity").length, 3);
    assert.match(
      document.querySelector(".detail-description").textContent,
      /No application description/,
    );
  } finally {
    dom.window.close();
  }
});
