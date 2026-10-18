(() => {
  "use strict";
  const data = JSON.parse(document.getElementById("schema-data").textContent);
  const { layout, geometry } = data;
  const tables = new Map(data.tables.map((table) => [table.id, table]));
  const nodes = new Map(layout.nodes.map((node) => [node.id, node]));
  const foreignKeys = new Map(data.foreignKeys.map((fk) => [fk.id, fk]));
  const groups = new Map(data.groups.map((group) => [group.id, group]));
  const $ = (id) => document.getElementById(id);
  const h = (value) =>
    String(value ?? "").replace(
      /[&<>"']/g,
      (character) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[character],
    );
  const relatedKeys = (id) =>
    data.foreignKeys.filter(
      (fk) => fk.childTableId === id || fk.parentTableId === id,
    );
  const name = (id) => `${tables.get(id).schema}.${tables.get(id).name}`;
  const icon = '<svg aria-hidden="true"><use href="#icon-table"/></svg>';
  const host = $("diagram");
  const state = {
    tableId: null,
    fkId: null,
    fkIds: new Set(),
    group: "",
    query: "",
    tab: "relationships",
    scale: 1,
  };
  const tableLayer = $("table-layer"),
    edgeLayer = $("edge-layer");

  let language = "en";
  try {
    if (localStorage.getItem("planora-schema-language") === "vi")
      language = "vi";
  } catch {}
  const textSources = new WeakMap(),
    attributeSources = new WeakMap();
  const translationPatterns = data.i18n.patterns.map(
    ([source, replacement]) => [new RegExp(source), replacement],
  );
  function t(message) {
    if (language !== "vi") return message;
    if (data.i18n.messages[message]) return data.i18n.messages[message];
    let translated = message;
    for (const [pattern, replacement] of translationPatterns)
      translated = translated.replace(pattern, replacement);
    for (const group of data.groups)
      translated = translated.replace(
        group.title,
        data.i18n.messages[group.title] || group.title,
      );
    return translated;
  }
  // Translate presentation text only. Never translate SQL identifiers, literal
  // defaults/computed definitions, metadata, or embedded source code.
  function localize(root = document.body, force = false) {
    if (language === "en" && !force) return;
    const excluded =
      "script,style,code,[data-raw],.entity-heading,.nav-name,.detail-title,.relationship-target,.relationship-name,.column-row,.key-detail code";
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
    let node;
    while ((node = walker.nextNode())) {
      if (node.parentElement?.closest(excluded)) continue;
      const current = node.data,
        saved = textSources.get(node);
      const original =
        !saved || current !== saved.last ? current : saved.original;
      const trimmed = original.trim(),
        translated = t(trimmed);
      const value = trimmed ? original.replace(trimmed, translated) : original;
      textSources.set(node, { original, last: value });
      if (node.data !== value) node.data = value;
    }
    const elements = [
      ...root.querySelectorAll("[title],[aria-label],[placeholder]"),
    ];
    if (root.matches?.("[title],[aria-label],[placeholder]"))
      elements.unshift(root);
    for (const element of elements) {
      if (element.closest("script,style,[data-raw]")) continue;
      const sources = attributeSources.get(element) || {};
      for (const attribute of ["title", "aria-label", "placeholder"]) {
        if (!element.hasAttribute(attribute)) continue;
        const current = element.getAttribute(attribute),
          saved = sources[attribute];
        const original =
          !saved || current !== saved.last ? current : saved.original;
        const value = t(original);
        sources[attribute] = { original, last: value };
        if (current !== value) element.setAttribute(attribute, value);
      }
      attributeSources.set(element, sources);
    }
  }
  function setLanguage(value) {
    const previousLanguage = language;
    language = value === "vi" ? "vi" : "en";
    document.documentElement.lang = language;
    document.title =
      data.databaseName +
      (language === "vi"
        ? " · Khám phá cơ sở dữ liệu"
        : " · SQL schema explorer");
    $("language").value = language;
    try {
      localStorage.setItem("planora-schema-language", language);
    } catch {}
    localize(document.body, previousLanguage === "vi" && language === "en");
    $("export-map").href = language === "vi" ? "map.vi.svg" : "map.svg";
  }

  function drawMap() {
    $("world").style.setProperty(
      "--table-row-height",
      geometry.rowHeight + "px",
    );
    $("world").style.setProperty(
      "--table-header-height",
      geometry.headerHeight + "px",
    );
    $("world").style.setProperty(
      "--table-footer-height",
      geometry.footerHeight + "px",
    );
    $("world").style.width = layout.width + "px";
    $("world").style.height = layout.height + "px";
    $("connections").setAttribute("width", layout.width);
    $("connections").setAttribute("height", layout.height);
    $("group-layer").innerHTML = layout.groups
      .map(
        (group) =>
          `<section class="functional-group" data-function-group="${group.id}" style="left:${group.x}px;top:${group.y}px;width:${group.width}px;height:${group.height}px;--group-fill:${group.color}55"><div class="functional-heading"><button data-group-focus="${group.id}">${h(group.title)}</button><p>${group.tableIds.length} tables · ${group.columns} columns</p></div></section>`,
      )
      .join("");
    tableLayer.innerHTML = layout.nodes
      .map((node) => {
        const table = tables.get(node.id);
        const fkColumns = new Set(
          data.foreignKeys
            .filter((fk) => fk.childTableId === table.id)
            .flatMap((fk) => fk.columns.map((column) => column.childColumn)),
        );
        return `<article class="entity" id="table-${table.id}" data-table="${table.id}" aria-label="${h(name(table.id))}" style="left:${node.x}px;top:${node.y}px;width:${node.width}px;height:${node.height}px">
        <button class="entity-heading" data-select="${table.id}" aria-pressed="false" title="Inspect ${h(name(table.id))}">${icon}<span><small>${h(table.schema)}.</small>${h(table.name)}</span></button>
        <div class="entity-columns" tabindex="0" aria-label="All ${table.columns.length} columns of ${h(table.name)}; scroll for more">${table.columns.map((column) => `<div class="column-row${column.primaryKey ? " pk-column" : ""}" data-column="${h(column.name)}" title="${h(column.name)} · ${h(column.sqlType)} · ${column.nullable ? "NULL" : "NOT NULL"}${column.identity ? " · IDENTITY" : ""}${column.computed ? " · COMPUTED" : ""}"><span class="column-tags">${column.primaryKey ? "<b>PK</b> " : ""}${fkColumns.has(column.name) ? "<em>FK</em> " : ""}${column.identity ? "ID" : ""}${column.computed ? "C" : ""}</span><span class="column-name">${h(column.name)}${column.nullable ? " ?" : ""}</span><span class="column-type">${h(column.sqlType)}</span></div>`).join("")}</div>
        <div class="entity-footer"><span>${h(groups.get(table.group).title)}</span><span>${table.columns.length} cols · ${relatedKeys(table.id).length} FKs</span></div></article>`;
      })
      .join("");
    edgeLayer.innerHTML = layout.edges
      .map((edge) => {
        const fk = foreignKeys.get(edge.id),
          points = edge.points.map((point) => point.join(",")).join(" ");
        const first = edge.points[0],
          last = edge.points.at(-1);
        const tooltip = `${fk.name}: ${name(fk.childTableId)} → ${name(fk.parentTableId)}; ${fk.columns.map((pair) => pair.childColumn + " → " + pair.parentColumn).join(", ")}; child ${fk.cardinality.child}, parent ${fk.cardinality.parent}; delete ${fk.onDelete}; ${fk.disabled ? "DISABLED" : fk.notTrusted ? "NOT TRUSTED" : "enabled / trusted"}`;
        return `<g id="fk-${fk.id}" class="edge${fk.disabled || fk.notTrusted ? " invalid" : ""}" data-fk="${fk.id}" tabindex="0" role="button" aria-label="${h(tooltip)}"><title>${h(tooltip)}</title><polyline class="edge-hit" points="${points}"/><polyline class="edge-line" points="${points}"/><text class="edge-label" x="${first[0] + (edge.childSide === "right" ? 8 : -28)}" y="${first[1] - 7}">${h(fk.cardinality.child)}</text><text class="edge-label" x="${last[0] + (edge.parentSide === "right" ? 8 : -28)}" y="${last[1] - 7}">${h(fk.cardinality.parent)}</text></g>`;
      })
      .join("");
    $("minimap").setAttribute(
      "viewBox",
      `0 0 ${layout.width} ${layout.height}`,
    );
    $("mini-groups").innerHTML = layout.groups
      .map(
        (group) =>
          `<rect x="${group.x}" y="${group.y}" width="${group.width}" height="${group.height}" fill="${group.color}" stroke="#9bb0c5" stroke-width="4"/>`,
      )
      .join("");
    $("mini-nodes").innerHTML = layout.nodes
      .map(
        (node) =>
          `<rect class="mini-node" data-select="${node.id}" x="${node.x}" y="${node.y}" width="${node.width}" height="${node.height}"><title>${h(name(node.id))}</title></rect>`,
      )
      .join("");
    document
      .querySelectorAll(".entity-columns")
      .forEach((scroller) =>
        scroller.addEventListener(
          "scroll",
          () => updatePorts(Number(scroller.closest(".entity").dataset.table)),
          { passive: true },
        ),
      );
  }

  function updatePorts(tableId) {
    const portY = (id, columnName) => {
      const node = nodes.get(id),
        table = tables.get(id);
      const scroller = $(`table-${id}`).querySelector(".entity-columns");
      const ordinal = table.columns.findIndex(
        (column) => column.name === columnName,
      );
      const y =
        geometry.columnOffset +
        ordinal * geometry.rowHeight -
        scroller.scrollTop;
      return (
        node.y +
        (y < geometry.columnOffset ||
        y > node.height - geometry.footerHeight - geometry.rowHeight / 2
          ? node.height - geometry.footerHeight / 2
          : y)
      );
    };
    for (const edge of layout.edges.filter(
      (edge) => edge.childTableId === tableId || edge.parentTableId === tableId,
    )) {
      const fk = foreignKeys.get(edge.id),
        points = edge.points.map((point) => [...point]);
      const startY = portY(fk.childTableId, edge.childColumn),
        endY = portY(fk.parentTableId, edge.parentColumn);
      points[0][1] = points[1][1] = startY;
      points.at(-1)[1] = points.at(-2)[1] = endY;
      const element = $(`fk-${fk.id}`);
      element
        .querySelectorAll("polyline")
        .forEach((line) =>
          line.setAttribute(
            "points",
            points.map((point) => point.join(",")).join(" "),
          ),
        );
      const labels = element.querySelectorAll(".edge-label");
      labels[0].setAttribute("y", startY - 7);
      labels[1].setAttribute("y", endY - 7);
    }
  }

  function renderNavigator() {
    const matching = data.tables.filter(
      (table) =>
        (!state.group || table.group === state.group) &&
        (!state.query ||
          `${name(table.id)} ${table.columns.map((column) => column.name).join(" ")}`
            .toLowerCase()
            .includes(state.query)),
    );
    $("nav-count").textContent = `${matching.length} / ${data.tables.length}`;
    $("table-list").innerHTML = matching.length
      ? data.groups
          .map((group) => {
            const items = matching
              .filter((table) => table.group === group.id)
              .sort((a, b) => a.name.localeCompare(b.name));
            return items.length
              ? `<button class="domain-heading" data-group-focus="${group.id}"><i style="background:${group.color}"></i>${h(group.title)}<span>${items.length}</span></button>${items.map((table) => `<button class="table-link" data-select="${table.id}" aria-pressed="${state.tableId === table.id}" title="${h(name(table.id))}">${icon}<span class="nav-name">${h(table.name)}</span><small>${table.columns.length}</small></button>`).join("")}`
              : "";
          })
          .join("")
      : '<p class="empty-search">No matching table or column.<br>All tables remain on the map.</p>';
    localize($("table-list"));
  }

  function highlight() {
    document
      .querySelectorAll(".functional-group")
      .forEach((panel) =>
        panel.classList.toggle(
          "focused",
          panel.dataset.functionGroup ===
            (state.tableId !== null
              ? tables.get(state.tableId).group
              : state.group),
        ),
      );
    const focused = new Set(
      state.tableId !== null
        ? [state.tableId]
        : state.group
          ? data.tables
              .filter((table) => table.group === state.group)
              .map((table) => table.id)
          : [],
    );
    const keys = state.fkIds.size
      ? [...state.fkIds].map((id) => foreignKeys.get(id))
      : state.tableId !== null
        ? relatedKeys(state.tableId)
        : state.group
          ? data.foreignKeys.filter(
              (fk) =>
                focused.has(fk.childTableId) || focused.has(fk.parentTableId),
            )
          : [];
    const related = new Set(
      keys.flatMap((fk) => [fk.childTableId, fk.parentTableId]),
    );
    const selectedKeys = new Set(keys.map((fk) => fk.id));
    const hasFocus = focused.size > 0;
    document.querySelectorAll(".entity").forEach((card) => {
      const id = Number(card.dataset.table);
      card.classList.toggle("selected", focused.has(id));
      card.classList.toggle("related", !focused.has(id) && related.has(id));
      card.classList.toggle(
        "unrelated",
        hasFocus && !focused.has(id) && !related.has(id),
      );
      card
        .querySelector(".entity-heading")
        .setAttribute("aria-pressed", String(focused.has(id)));
    });
    document.querySelectorAll(".edge").forEach((edge) => {
      const selected = selectedKeys.has(Number(edge.dataset.fk));
      edge.classList.toggle("selected", selected);
      edge.classList.toggle("unrelated", hasFocus && !selected);
      edge.setAttribute("aria-pressed", String(selected));
      // Relevant lines are painted last, but still underneath every table.
      if (selected) edgeLayer.append(edge);
    });
    document.querySelectorAll(".table-link").forEach((button) => {
      const id = Number(button.dataset.select);
      button.setAttribute("aria-pressed", String(id === state.tableId));
      button.classList.toggle(
        "is-related",
        related.has(id) && id !== state.tableId,
      );
    });
    document.querySelectorAll(".mini-node").forEach((rect) => {
      const id = Number(rect.dataset.select);
      rect.classList.toggle("selected", focused.has(id));
      rect.classList.toggle("related", related.has(id) && !focused.has(id));
    });
    document
      .querySelectorAll(".fk-column")
      .forEach((row) => row.classList.remove("fk-column"));
    for (const id of state.fkIds) {
      const fk = foreignKeys.get(id);
      for (const [id, field] of [
        [fk.childTableId, "childColumn"],
        [fk.parentTableId, "parentColumn"],
      ]) {
        const names = new Set(fk.columns.map((pair) => pair[field]));
        $(`table-${id}`)
          .querySelectorAll("[data-column]")
          .forEach((row) => {
            if (names.has(row.dataset.column)) row.classList.add("fk-column");
          });
      }
    }
    $("map-status").textContent = state.fkIds.size
      ? `Tracing ${state.fkIds.size} foreign key${state.fkIds.size === 1 ? "" : "s"} · all connections retained`
      : state.tableId !== null
        ? `${keys.length} connected foreign keys · all tables retained`
        : state.group
          ? `${groups.get(state.group).title} · all tables retained`
          : `${data.tables.length} tables · ${data.foreignKeys.length} foreign keys`;
    $("locate").disabled = state.tableId === null;
    localize($("map-status"));
  }

  function relationshipCard(fk, id) {
    const outgoing = fk.childTableId === id,
      other = outgoing ? fk.parentTableId : fk.childTableId;
    return `<div class="relationship-card${state.fkIds.has(fk.id) ? " is-active" : ""}"><div class="relationship-direction">${fk.childTableId === fk.parentTableId ? "Self reference" : outgoing ? "Outgoing · references parent" : "Incoming · referenced by"}</div><button class="relationship-target" data-select="${other}" title="Locate ${h(name(other))}">${h(tables.get(other).name)} ${outgoing ? "↗" : "↙"}</button><div class="relationship-mapping">${fk.columns.map((pair) => `<code>${h(tables.get(fk.childTableId).name)}.${h(pair.childColumn)}<br>→ ${h(tables.get(fk.parentTableId).name)}.${h(pair.parentColumn)}</code>`).join("")}</div><div class="relationship-meta">Child ${h(fk.cardinality.child)} : parent ${h(fk.cardinality.parent)} · DELETE ${h(fk.onDelete)} · UPDATE ${h(fk.onUpdate || "NO_ACTION")}${fk.disabled ? " · DISABLED" : fk.notTrusted ? " · NOT TRUSTED" : ""}</div><div class="relationship-name">${h(fk.name)}</div><button class="trace-button" data-trace="${fk.id}" aria-pressed="${state.fkIds.has(fk.id)}">${state.fkIds.has(fk.id) ? "Tracing connection" : "Trace connection"}</button></div>`;
  }

  function renderInspector() {
    if (state.tableId === null) {
      $("inspector-body").innerHTML =
        `<div class="inspector-empty"><svg style="width:32px;height:32px;color:#8297ac;margin-bottom:14px"><use href="#icon-table"/></svg><h3>Select a table</h3><p style="margin-top:10px">Click a table on the map or in Object explorer to view its purpose, every column, keys, and incoming and outgoing connections.</p><p style="margin-top:14px">${data.tables.length} tables and ${data.foreignKeys.length} declared relationships are available on this map.</p></div>`;
      localize($("inspector-body"));
      return;
    }
    const table = tables.get(state.tableId),
      fks = relatedKeys(table.id);
    const outgoing = fks.filter((fk) => fk.childTableId === table.id);
    const incoming = fks.filter(
      (fk) => fk.parentTableId === table.id && fk.childTableId !== table.id,
    );
    let content;
    if (state.tab === "columns") {
      const fkColumns = new Set(
        outgoing.flatMap((fk) => fk.columns.map((pair) => pair.childColumn)),
      );
      content = `<p class="muted">Every captured column, in database ordinal order. Expand a row for definitions.</p>${table.columns.map((column) => `<details class="column-detail"><summary><code>${h(column.name)}</code><small>${h(column.sqlType)} · ${column.nullable ? "NULL" : "NOT NULL"}${column.primaryKey ? " · PK" : ""}${fkColumns.has(column.name) ? " · FK" : ""}</small></summary><dl><dt>Ordinal</dt><dd>${h(column.id)}</dd><dt>Identity</dt><dd>${column.identity ? "Yes" : "No"}</dd><dt>Computed</dt><dd>${column.computed ? "Yes" : "No"}</dd><dt>Default</dt><dd data-raw>${h(column.defaultDefinition || "—")}</dd><dt>Expression</dt><dd data-raw>${h(column.computedDefinition || "—")}</dd></dl></details>`).join("")}`;
    } else if (state.tab === "keys") {
      content = `<p class="muted">Captured primary keys and unique indexes.</p>${table.keys.length ? table.keys.map((key) => `<div class="key-detail"><code>${h(key.name)}</code><div>${key.primaryKey ? "Primary key" : "Unique index"}${key.disabled ? " · DISABLED" : ""}</div><small data-raw>${key.columns.map((column) => h(column.name)).join(", ")}</small>${key.filtered ? `<small><span>Filter:</span> <code>${h(key.filterDefinition)}</code></small>` : ""}</div>`).join("") : '<p class="muted">No primary key or unique index declared.</p>'}`;
    } else {
      content = `<h3 class="section-label">Outgoing references <span>(${outgoing.length})</span></h3>${outgoing.map((fk) => relationshipCard(fk, table.id)).join("") || '<p class="muted">No outgoing foreign keys.</p>'}<h3 class="section-label">Incoming references <span>(${incoming.length})</span></h3>${incoming.map((fk) => relationshipCard(fk, table.id)).join("") || '<p class="muted">No other table references this table.</p>'}<p class="muted">Minimum child count is zero. Cardinality uses nullable FK columns and enabled, unfiltered unique indexes. Disabled or untrusted constraints may not hold for existing data.</p>`;
    }
    $("inspector-body").innerHTML =
      `<div class="detail-schema">${h(table.schema)} · USER TABLE</div><h3 class="detail-title">${h(table.name)}</h3><span class="domain-tag">${h(groups.get(table.group).title)}</span><p class="detail-description">${h(table.description)}</p><p class="description-source">Application description · structure from SQL catalog</p><div class="detail-stats"><div><strong>${table.columns.length}</strong>Columns</div><div><strong>${outgoing.length}</strong>Outgoing FKs</div><div><strong>${incoming.length}</strong>Incoming FKs</div></div>${state.fkIds.size ? `<p class="trace-summary">${state.fkIds.size} connection${state.fkIds.size === 1 ? "" : "s"} highlighted · Ctrl/⌘-click to add or remove.</p>` : ""}<div class="detail-tabs" role="tablist" aria-label="Table detail sections">${[
        ["relationships", "Relationships"],
        ["columns", "Columns"],
        ["keys", "Keys"],
      ]
        .map(
          ([id, label]) =>
            `<button id="tab-${id}" role="tab" data-tab="${id}" aria-selected="${state.tab === id}" aria-controls="detail-content" tabindex="${state.tab === id ? 0 : -1}">${label}</button>`,
        )
        .join(
          "",
        )}</div><div id="detail-content" role="tabpanel" aria-labelledby="tab-${state.tab}">${content}</div>`;
    localize($("inspector-body"));
  }

  function selectTable(id, locate = true) {
    if (!tables.has(id)) return;
    state.tableId = id;
    state.fkId = null;
    state.fkIds.clear();
    state.tab = "relationships";
    highlight();
    renderInspector();
    $("inspector-body").scrollTop = 0;
    $("announcement").textContent =
      `${name(id)} selected. ${relatedKeys(id).length} relationships highlighted.`;
    localize($("announcement"));
    if (locate) locateTable(id);
  }
  function trace(id, additive = false) {
    const fk = foreignKeys.get(id);
    if (!fk) return;
    if (!additive) state.fkIds.clear();
    if (additive && state.fkIds.has(id)) state.fkIds.delete(id);
    else state.fkIds.add(id);
    if (!state.fkIds.size) {
      clear();
      return;
    }
    if (state.tableId !== fk.childTableId && state.tableId !== fk.parentTableId)
      state.tableId = fk.childTableId;
    state.fkId = state.fkIds.has(id) ? id : [...state.fkIds].at(-1);
    state.tab = "relationships";
    const scrollTop = $("inspector-body").scrollTop;
    highlight();
    renderInspector();
    $("inspector-body").scrollTop = scrollTop;
    document.querySelectorAll(".fk-column").forEach((row) => {
      const scroller = row.closest(".entity-columns");
      scroller.scrollTop =
        row.offsetTop - scroller.offsetTop - scroller.clientHeight / 2;
      updatePorts(Number(scroller.closest(".entity").dataset.table));
    });
    $("announcement").textContent =
      `${fk.name}. ${name(fk.childTableId)} references ${name(fk.parentTableId)}.`;
    localize($("announcement"));
  }
  function clear() {
    stopCamera();
    state.tableId = null;
    state.fkId = null;
    state.fkIds.clear();
    state.group = "";
    $("module").value = "";
    renderNavigator();
    highlight();
    renderInspector();
    $("announcement").textContent =
      "Selection cleared. All table relationships shown.";
    localize($("announcement"));
  }
  let cameraFrame = null;
  let paddingX = 0,
    paddingY = 0;
  const reducedMotion = () =>
    window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
  function stopCamera() {
    if (cameraFrame !== null) window.cancelAnimationFrame(cameraFrame);
    cameraFrame = null;
  }
  function sizeCanvas(value) {
    state.scale = Math.max(0.02, Math.min(1.8, value));
    paddingX = host.clientWidth / 2;
    paddingY = host.clientHeight / 2;
    const world = $("world"),
      space = $("world-space");
    world.style.left = paddingX + "px";
    world.style.top = paddingY + "px";
    world.style.transform = "scale(" + state.scale + ")";
    space.style.width = layout.width * state.scale + 2 * paddingX + "px";
    space.style.height = layout.height * state.scale + 2 * paddingY + "px";
    $("zoom-value").textContent = Math.round(state.scale * 100) + "%";
  }
  function updateMiniViewport() {
    const clamp = (value, max) => Math.max(0, Math.min(max, value));
    const x = clamp((host.scrollLeft - paddingX) / state.scale, layout.width);
    const y = clamp((host.scrollTop - paddingY) / state.scale, layout.height);
    const right = clamp(
      (host.scrollLeft + host.clientWidth - paddingX) / state.scale,
      layout.width,
    );
    const bottom = clamp(
      (host.scrollTop + host.clientHeight - paddingY) / state.scale,
      layout.height,
    );
    const rect = $("mini-viewport");
    rect.setAttribute("x", x);
    rect.setAttribute("y", y);
    rect.setAttribute("width", Math.max(0, right - x));
    rect.setAttribute("height", Math.max(0, bottom - y));
  }
  function applyCamera(scale, centerX, centerY) {
    sizeCanvas(scale);
    host.scrollTo({
      left: Math.max(
        0,
        centerX * state.scale + paddingX - host.clientWidth / 2,
      ),
      top: Math.max(
        0,
        centerY * state.scale + paddingY - host.clientHeight / 2,
      ),
      behavior: "instant",
    });
    updateMiniViewport();
  }
  function moveCamera(centerX, centerY, scale, smooth = true) {
    stopCamera();
    const startScale = state.scale;
    const startX =
      (host.scrollLeft - paddingX + host.clientWidth / 2) / startScale;
    const startY =
      (host.scrollTop - paddingY + host.clientHeight / 2) / startScale;
    if (!smooth || reducedMotion()) {
      applyCamera(scale, centerX, centerY);
      return;
    }
    const started = performance.now(),
      duration = 460;
    const tick = (now) => {
      const progress = Math.max(0, Math.min(1, (now - started) / duration));
      const eased = 1 - Math.pow(1 - progress, 3);
      applyCamera(
        startScale + (scale - startScale) * eased,
        startX + (centerX - startX) * eased,
        startY + (centerY - startY) * eased,
      );
      cameraFrame = progress < 1 ? window.requestAnimationFrame(tick) : null;
    };
    cameraFrame = window.requestAnimationFrame(tick);
  }
  function zoom(value, x = host.clientWidth / 2, y = host.clientHeight / 2) {
    stopCamera();
    const wx = (host.scrollLeft + x - paddingX) / state.scale;
    const wy = (host.scrollTop + y - paddingY) / state.scale;
    sizeCanvas(value);
    host.scrollTo({
      left: Math.max(0, wx * state.scale + paddingX - x),
      top: Math.max(0, wy * state.scale + paddingY - y),
      behavior: "instant",
    });
    updateMiniViewport();
  }
  function locateTable(id) {
    const node = nodes.get(id);
    moveCamera(
      node.x + node.width / 2,
      node.y + node.height / 2,
      Math.max(state.scale, 1),
    );
  }
  function fit() {
    moveCamera(
      layout.width / 2,
      layout.height / 2,
      Math.min(
        (host.clientWidth - 24) / layout.width,
        (host.clientHeight - 24) / layout.height,
        1,
      ),
      false,
    );
  }

  document.addEventListener("click", (event) => {
    if (event.shiftKey && event.target.closest("#diagram")) return;
    const group = event.target.closest("[data-group-focus]");
    if (group) {
      focusGroup(group.dataset.groupFocus);
      return;
    }
    const selected = event.target.closest("[data-select]");
    if (selected) {
      selectTable(Number(selected.dataset.select));
      return;
    }
    const edge = event.target.closest("[data-fk]");
    if (edge) {
      trace(Number(edge.dataset.fk), event.ctrlKey || event.metaKey);
      return;
    }
    const connection = event.target.closest("[data-trace]");
    if (connection) {
      trace(Number(connection.dataset.trace), event.ctrlKey || event.metaKey);
      return;
    }
    const card = event.target.closest(".entity");
    if (card) {
      if (state.tableId !== Number(card.dataset.table))
        selectTable(Number(card.dataset.table));
      else locateTable(Number(card.dataset.table));
      return;
    }
    const tab = event.target.closest("[data-tab]");
    if (tab) {
      state.tab = tab.dataset.tab;
      renderInspector();
      $(`tab-${state.tab}`).focus();
    }
  });
  document.addEventListener("keydown", (event) => {
    const target =
      event.target instanceof Element ? event.target : document.body;
    if (event.key === "Escape") {
      clear();
      return;
    }
    if (event.key === "/" && !target.matches("input,select,textarea")) {
      event.preventDefault();
      $("search").focus();
    }
    const edge = target.closest("[data-fk]");
    if (edge && (event.key === "Enter" || event.key === " ")) {
      event.preventDefault();
      trace(Number(edge.dataset.fk), event.ctrlKey || event.metaKey);
    }
    const tab = target.closest("[data-tab]");
    if (tab && ["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) {
      event.preventDefault();
      const ids = ["relationships", "columns", "keys"];
      const index =
        event.key === "Home"
          ? 0
          : event.key === "End"
            ? 2
            : (ids.indexOf(state.tab) + (event.key === "ArrowRight" ? 1 : 2)) %
              3;
      state.tab = ids[index];
      renderInspector();
      $(`tab-${state.tab}`).focus();
    }
  });
  $("search").addEventListener("input", (event) => {
    state.query = event.target.value.toLowerCase().trim();
    renderNavigator();
    highlight();
  });
  $("search").addEventListener("keydown", (event) => {
    if (event.key === "Enter") document.querySelector(".table-link")?.click();
  });
  function focusGroup(id) {
    stopCamera();
    state.group = id;
    state.tableId = null;
    state.fkId = null;
    state.fkIds.clear();
    $("module").value = id;
    renderNavigator();
    highlight();
    renderInspector();
    const group = layout.groups.find((group) => group.id === id);
    if (group)
      moveCamera(
        group.x + group.width / 2,
        group.y + group.height / 2,
        Math.min(
          (host.clientWidth - 40) / group.width,
          (host.clientHeight - 40) / group.height,
          1,
        ),
      );
    else fit();
  }
  $("module").addEventListener("change", (event) =>
    focusGroup(event.target.value),
  );
  $("language").addEventListener("change", (event) =>
    setLanguage(event.target.value),
  );
  $("fit").addEventListener("click", fit);
  $("mini-fit").addEventListener("click", fit);
  $("plus").addEventListener("click", () => zoom(state.scale * 1.2));
  $("minus").addEventListener("click", () => zoom(state.scale / 1.2));
  $("actual").addEventListener("click", () => zoom(1));
  $("locate").addEventListener("click", () => {
    if (state.tableId !== null) locateTable(state.tableId);
  });
  $("clear").addEventListener("click", clear);
  $("help-toggle").addEventListener("click", () => {
    $("help").hidden = !$("help").hidden;
    $("help-toggle").setAttribute("aria-expanded", String(!$("help").hidden));
    updateMiniViewport();
  });
  host.addEventListener(
    "wheel",
    (event) => {
      stopCamera();
      if (!event.ctrlKey && !event.metaKey) return;
      event.preventDefault();
      const rect = host.getBoundingClientRect();
      zoom(
        state.scale * (event.deltaY < 0 ? 1.1 : 1 / 1.1),
        event.clientX - rect.left,
        event.clientY - rect.top,
      );
    },
    { passive: false },
  );
  let drag = null,
    suppressDragClick = false;
  host.addEventListener(
    "click",
    (event) => {
      if (suppressDragClick && event.detail > 0) {
        suppressDragClick = false;
        event.preventDefault();
        event.stopPropagation();
      }
    },
    true,
  );
  host.addEventListener("pointerdown", (event) => {
    if (
      event.button !== 0 ||
      (!event.shiftKey &&
        event.target.closest(".entity,.edge,.functional-heading"))
    )
      return;
    stopCamera();
    suppressDragClick = false;
    event.preventDefault();
    drag = {
      pointerId: event.pointerId,
      x: event.clientX,
      y: event.clientY,
      left: host.scrollLeft,
      top: host.scrollTop,
      moved: false,
    };
    host.setPointerCapture?.(event.pointerId);
    host.classList.add("is-dragging");
  });
  host.addEventListener("pointermove", (event) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    const dx = event.clientX - drag.x,
      dy = event.clientY - drag.y;
    if (Math.hypot(dx, dy) > 4) drag.moved = true;
    if (!drag.moved) return;
    event.preventDefault();
    host.scrollLeft = drag.left - dx;
    host.scrollTop = drag.top - dy;
    updateMiniViewport();
  });
  const endDrag = (event) => {
    if (
      !drag ||
      (event.pointerId !== undefined && event.pointerId !== drag.pointerId)
    )
      return;
    suppressDragClick = event.type === "pointerup" && drag.moved;
    const pointerId = drag.pointerId;
    drag = null;
    if (host.hasPointerCapture?.(pointerId))
      host.releasePointerCapture(pointerId);
    host.classList.remove("is-dragging");
  };
  host.addEventListener("pointerup", endDrag);
  host.addEventListener("pointercancel", endDrag);
  host.addEventListener("lostpointercapture", endDrag);
  document.addEventListener("keydown", (event) => {
    if (event.key === "Shift") host.classList.add("shift-pan");
  });
  document.addEventListener("keyup", (event) => {
    if (event.key === "Shift") host.classList.remove("shift-pan");
  });
  window.addEventListener("blur", (event) => {
    endDrag(event);
    host.classList.remove("shift-pan");
    stopCamera();
  });
  host.addEventListener("keydown", (event) => {
    if (
      [
        "ArrowDown",
        "ArrowUp",
        "ArrowLeft",
        "ArrowRight",
        "PageDown",
        "PageUp",
        "Home",
        "End",
      ].includes(event.key)
    )
      stopCamera();
  });
  host.addEventListener("scroll", updateMiniViewport, { passive: true });
  $("minimap").addEventListener("click", (event) => {
    if (event.target.closest("[data-select]")) return;
    const rect = $("minimap").getBoundingClientRect();
    moveCamera(
      ((event.clientX - rect.left) / rect.width) * layout.width,
      ((event.clientY - rect.top) / rect.height) * layout.height,
      state.scale,
    );
  });
  function resized() {
    const centerX = host.scrollLeft / state.scale,
      centerY = host.scrollTop / state.scale;
    stopCamera();
    applyCamera(state.scale, centerX, centerY);
  }
  window.addEventListener("resize", resized);
  if (typeof ResizeObserver !== "undefined")
    new ResizeObserver(resized).observe(host);

  drawMap();
  renderNavigator();
  zoom(1);
  const initial = data.tables.find(
    (table) => `#table-${table.id}` === location.hash,
  );
  if (initial) selectTable(initial.id, true);
  else {
    highlight();
    renderInspector();
    fit();
  }
  setLanguage(language);
})();
