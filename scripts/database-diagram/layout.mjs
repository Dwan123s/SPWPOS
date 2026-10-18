import { groupOf, groups, qualified, validateSchema } from "./model.mjs";

export const geometry = {
  cardWidth: 360,
  cardHeight: 284,
  headerHeight: 44,
  footerHeight: 30,
  rowHeight: 26,
  columnOffset: 58,
  gapX: 160,
  gapY: 148,
  margin: 180,
};

// Condense cycles before assigning dependency layers. Ownership links are still
// rendered; redundant UserId links do not pull every business object into the
// same layer when it also has more specific parents.
function dependencyLayers(schema) {
  const byId = new Map(schema.tables.map((table) => [table.id, table]));
  const parents = new Map(schema.tables.map((table) => [table.id, []]));
  for (const fk of schema.foreignKeys) {
    if (fk.childTableId !== fk.parentTableId)
      parents.get(fk.childTableId).push(fk);
  }
  const dependencies = new Map(
    [...parents].map(([id, keys]) => {
      const ownership = (fk) =>
        /^(AspNetUsers|Users)$/i.test(byId.get(fk.parentTableId).name) &&
        fk.columns.some((pair) => /^UserId$/i.test(pair.childColumn));
      const specific = keys.filter((fk) => !ownership(fk));
      return [
        id,
        (specific.length ? specific : keys).map((fk) => fk.parentTableId),
      ];
    }),
  );
  const indices = new Map(),
    low = new Map(),
    stack = [],
    onStack = new Set(),
    componentOf = new Map(),
    components = [];
  let nextIndex = 0;
  function visit(id) {
    indices.set(id, nextIndex);
    low.set(id, nextIndex++);
    stack.push(id);
    onStack.add(id);
    for (const parent of dependencies.get(id)) {
      if (!indices.has(parent)) {
        visit(parent);
        low.set(id, Math.min(low.get(id), low.get(parent)));
      } else if (onStack.has(parent))
        low.set(id, Math.min(low.get(id), indices.get(parent)));
    }
    if (low.get(id) === indices.get(id)) {
      const component = [];
      let item;
      do {
        item = stack.pop();
        onStack.delete(item);
        componentOf.set(item, components.length);
        component.push(item);
      } while (item !== id);
      components.push(component);
    }
  }
  for (const table of schema.tables)
    if (!indices.has(table.id)) visit(table.id);
  const ranks = new Map();
  function rank(componentId) {
    if (ranks.has(componentId)) return ranks.get(componentId);
    const upstream = components[componentId]
      .flatMap((id) => dependencies.get(id))
      .map((id) => componentOf.get(id))
      .filter((id) => id !== componentId);
    const value = upstream.length ? 1 + Math.max(...upstream.map(rank)) : 0;
    ranks.set(componentId, value);
    return value;
  }
  return new Map(
    schema.tables.map((table) => [table.id, rank(componentOf.get(table.id))]),
  );
}

export function layoutSchema(schema) {
  validateSchema(schema);
  const {
    cardWidth: width,
    cardHeight: maxHeight,
    gapX,
    gapY,
    margin,
  } = geometry;
  const ranks = dependencyLayers(schema);
  const laneByGroup = {
    accounts: 0,
    time: 0,
    tasks: 1,
    planning: 1,
    assistant: 1,
    intelligence: 2,
    notifications: 2,
    other: 2,
  };
  const laneRows = [0, 0, 0],
    nodes = [],
    frames = [];
  const activeLanes = [
    ...new Set(
      schema.tables.map((table) => laneByGroup[groupOf(table).id] ?? 2),
    ),
  ].sort((a, b) => a - b);
  for (const group of groups) {
    const items = schema.tables
      .filter((table) => groupOf(table).id === group.id)
      .sort(
        (a, b) =>
          ranks.get(a.id) - ranks.get(b.id) ||
          qualified(a).localeCompare(qualified(b)),
      );
    if (!items.length) continue;
    const lane = activeLanes.indexOf(laneByGroup[group.id] ?? 2);
    const cols = items.length >= 9 ? 3 : Math.min(2, items.length);
    const groupRows = Math.ceil(items.length / cols);
    const startCol = lane * 4,
      startRow = laneRows[lane];
    laneRows[lane] += groupRows + 1;
    const tableIds = [];
    items.forEach((table, index) => {
      const col = startCol + (index % cols),
        row = startRow + Math.floor(index / cols);
      tableIds.push(table.id);
      nodes.push({
        id: table.id,
        group: group.id,
        col,
        row,
        rank: ranks.get(table.id),
        x: margin + col * (width + gapX),
        y: margin + row * (maxHeight + gapY),
        width,
        height: Math.min(
          maxHeight,
          geometry.headerHeight +
            geometry.footerHeight +
            2 +
            Math.max(3, Math.min(8, table.columns.length)) * geometry.rowHeight,
        ),
      });
    });
    frames.push({
      id: group.id,
      title: group.title,
      color: group.color,
      tableIds,
      columns: items.reduce((sum, table) => sum + table.columns.length, 0),
      x: margin + startCol * (width + gapX) - 20,
      y: margin + startRow * (maxHeight + gapY) - 82,
      width: cols * width + (cols - 1) * gapX + 40,
      height: (groupRows - 1) * (maxHeight + gapY) + maxHeight + 102,
    });
  }
  const rows = Math.max(...nodes.map((node) => node.row)) + 1;
  const columns = Math.max(...nodes.map((node) => node.col)) + 1;
  const byId = new Map(nodes.map((node) => [node.id, node]));
  const tableById = new Map(schema.tables.map((table) => [table.id, table]));
  const portY = (node, columnName) => {
    const table = tableById.get(node.id);
    const ordinal = table.columns.findIndex(
      (column) => column.name === columnName,
    );
    const y = geometry.columnOffset + ordinal * geometry.rowHeight;
    return (
      node.y +
      (y > node.height - geometry.footerHeight - geometry.rowHeight / 2
        ? node.height - geometry.footerHeight / 2
        : y)
    );
  };
  const edges = [...schema.foreignKeys]
    .sort((a, b) => a.id - b.id)
    .map((fk, index, all) => {
      const child = byId.get(fk.childTableId),
        parent = byId.get(fk.parentTableId);
      const sameColumn = child.col === parent.col;
      const childSide =
        child.col >= parent.col && !sameColumn ? "left" : "right";
      const parentSide =
        child.col >= parent.col || sameColumn ? "right" : "left";
      const startX = child.x + (childSide === "right" ? width : 0);
      const endX = parent.x + (parentSide === "right" ? width : 0);
      const portPair =
        fk.columns.find((pair) => !/^UserId$/i.test(pair.childColumn)) ||
        fk.columns[0];
      const startY = portY(child, portPair.childColumn);
      const endY = portY(parent, portPair.parentColumn);
      const track = (index + 1) / (all.length + 1);
      const offset = 22 + track * (gapX - 44);
      const sx = startX + (childSide === "right" ? offset : -offset);
      const ex = endX + (parentSide === "right" ? offset : -offset);
      let points;
      if (sameColumn && child.id !== parent.id)
        points = [
          [startX, startY],
          [sx, startY],
          [sx, endY],
          [endX, endY],
        ];
      else {
        const laneY =
          margin + child.row * (maxHeight + gapY) + maxHeight + 18 + track * 50;
        const targetGutter =
          child.id === parent.id ? endX + 18 + track * 18 : ex;
        points = [
          [startX, startY],
          [sx, startY],
          [sx, laneY],
          [targetGutter, laneY],
          [targetGutter, endY],
          [endX, endY],
        ];
      }
      return {
        id: fk.id,
        childTableId: fk.childTableId,
        parentTableId: fk.parentTableId,
        childColumn: portPair.childColumn,
        parentColumn: portPair.parentColumn,
        childSide,
        parentSide,
        points,
      };
    });
  return {
    width: columns * width + (columns - 1) * gapX + 2 * margin,
    height: rows * maxHeight + (rows - 1) * gapY + 2 * margin,
    nodes,
    edges,
    groups: frames,
  };
}

// Used by regression checks and available to downstream diagram exporters.
export function segmentCrossesCard([a, b], card) {
  const epsilon = 0.01;
  if (Math.abs(a[0] - b[0]) < epsilon)
    return (
      a[0] > card.x + epsilon &&
      a[0] < card.x + card.width - epsilon &&
      Math.max(a[1], b[1]) > card.y + epsilon &&
      Math.min(a[1], b[1]) < card.y + card.height - epsilon
    );
  if (Math.abs(a[1] - b[1]) < epsilon)
    return (
      a[1] > card.y + epsilon &&
      a[1] < card.y + card.height - epsilon &&
      Math.max(a[0], b[0]) > card.x + epsilon &&
      Math.min(a[0], b[0]) < card.x + card.width - epsilon
    );
  throw new Error("Non-orthogonal connector segment.");
}
