const modules = [
  { id: 'accounts', title: 'Accounts & security', color: '#e2ecff', pattern: /^(AspNet|EmailOtps$|Password|RefreshTokens$|Security|account_status_logs$|user_profiles$)/i },
  { id: 'time', title: 'Availability & preferences', color: '#daf3e9', pattern: /^(AvailabilityExceptions|FixedEvents|WeeklyAvailability|WorkPreferences)$/ },
  { id: 'tasks', title: 'Tasks & categories', color: '#fff0cc', pattern: /^(WorkTasks|TaskCategories|TaskDependencies|task_types)$/ },
  { id: 'planning', title: 'Planning & execution', color: '#e5e4ff', pattern: /^(PlanProposals|plan_items|plan_item_reasons|plan_warnings|WorkSessions|session_executions|execution_segments|RecoveryCases|RecoverySettings)$/ },
  { id: 'intelligence', title: 'Intelligence & workload', color: '#d8eff8', pattern: /^(capacity_snapshots|estimation_observations|personal_capacity_profiles|risk_assessments|risk_reasons|task_type_biases)$/ },
  { id: 'notifications', title: 'Notifications & operations', color: '#fde2e5', pattern: /^(InAppNotifications|notification_settings|system_job_runs)$/ },
  { id: 'assistant', title: 'Notes & AI assistant', color: '#f6e3cf', pattern: /^(AgentConversations|AgentTurns|AiUsageRecords|PlanningNotes)$/ },
];
export const groups = [...modules, { id: 'other', title: 'Other tables', color: '#e8edf0', pattern: /./ }];
export const groupOf = table => groups.find(group => group.pattern.test(table.name));
export const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
export const qualified = table => `${table.schema}.${table.name}`;
export function sqlType(column) {
  const type = column.type;
  if (/^(n?varchar|n?char|varbinary|binary)$/.test(type)) return `${type}(${column.maxLength === -1 ? 'max' : column.maxLength / (/^n/.test(type) ? 2 : 1)})`;
  if (/^(decimal|numeric)$/.test(type)) return `${type}(${column.precision},${column.scale})`;
  if (/^(datetime2|datetimeoffset|time)$/.test(type)) return `${type}(${column.scale})`;
  return type;
}
export function validateSchema(schema) {
  if (!schema.databaseName || !schema.capturedAt || !Array.isArray(schema.tables) || !schema.tables.length || !Array.isArray(schema.foreignKeys)) throw new Error('Invalid or empty schema snapshot.');
  const byId = new Map(schema.tables.map(table => [table.id, table]));
  if (byId.size !== schema.tables.length) throw new Error('Duplicate table IDs.');
  for (const table of schema.tables) {
    if (!table.columns?.length) throw new Error(`No columns for ${qualified(table)}.`);
    if (new Set(table.columns.map(column => column.name)).size !== table.columns.length) throw new Error(`Duplicate columns in ${qualified(table)}.`);
  }
  for (const fk of schema.foreignKeys) {
    const child = byId.get(fk.childTableId), parent = byId.get(fk.parentTableId);
    if (!child || !parent || !fk.columns?.length) throw new Error(`Unresolved foreign key ${fk.name}.`);
    for (const column of fk.columns) {
      if (!child.columns.some(c => c.name === column.childColumn) || !parent.columns.some(c => c.name === column.parentColumn)) throw new Error(`Unresolved column in ${fk.name}.`);
    }
  }
  return byId;
}
export function relationship(schema, fk) {
  const child = schema.tables.find(table => table.id === fk.childTableId);
  const names = fk.columns.map(column => column.childColumn);
  // Any unfiltered unique key contained in the FK columns bounds child
  // cardinality, including composite owner/entity keys. Filtered or disabled
  // indexes cannot establish that bound for every row.
  const unique = (child.keys || []).some(key => key.unique && !key.filtered && !key.disabled && key.columns.length && key.columns.every(column => names.includes(column.name)));
  const nullable = child.columns.some(column => names.includes(column.name) && column.nullable);
  return { parent: nullable ? '0..1' : '1', child: unique ? '0..1' : '0..N' };
}
export function graphSpec(schema, { group, full = false, tableId } = {}) {
  const byId = validateSchema(schema);
  const focus = schema.tables.filter(table => tableId === undefined ? !group || groupOf(table).id === group : table.id === tableId);
  const focused = new Set(focus.map(table => table.id));
  const fks = schema.foreignKeys.filter(fk => !group && tableId === undefined || focused.has(fk.childTableId));
  const included = new Set([...focused, ...fks.flatMap(fk => [fk.childTableId, fk.parentTableId])]);
  const nodes = schema.tables.filter(table => included.has(table.id)).map(table => {
    const external = !focused.has(table.id);
    const detailed = full && !external;
    const columns = detailed ? table.columns : table.columns.filter(column => column.primaryKey);
    const fkColumns = new Set(schema.foreignKeys.filter(fk => fk.childTableId === table.id).flatMap(fk => fk.columns.map(column => column.childColumn)));
    const rows = columns.map(column => {
      const tags = [column.primaryKey && 'PK', fkColumns.has(column.name) && 'FK', column.identity && 'ID'].filter(Boolean).join(' ');
      return `<TR><TD ALIGN="LEFT"><FONT COLOR="#516078">${escapeHtml(tags) || '&#160;'}</FONT></TD><TD ALIGN="LEFT">${escapeHtml(column.name)}${column.nullable ? ' ?' : ''}</TD>${detailed ? `<TD ALIGN="LEFT"><FONT COLOR="#516078">${escapeHtml(sqlType(column))}</FONT></TD>` : ''}</TR>`;
    }).join('');
    const width = detailed ? 3 : 2;
    const label = `<TABLE BORDER="1" COLOR="#b4c2d1" CELLBORDER="0" CELLSPACING="0" CELLPADDING="5"><TR><TD COLSPAN="${width}" BGCOLOR="${external ? '#edf0f3' : groupOf(table).color}" ALIGN="LEFT"><B>${escapeHtml(qualified(table))}</B>${external ? ' [context]' : ''}</TD></TR>${rows}<TR><TD COLSPAN="${width}" ALIGN="LEFT"><FONT POINT-SIZE="9" COLOR="#64748b">${table.columns.length} columns</FONT></TD></TR></TABLE>`;
    return `t${table.id} [id="entity-${table.id}" URL="#table-${table.id}" tooltip=${JSON.stringify(qualified(table))} label=<${label}>];`;
  });
  const edges = fks.map(fk => {
    const card = relationship(schema, fk);
    const mapping = fk.columns.map(column => `${column.childColumn} -> ${column.parentColumn}`).join(', ');
    return `t${fk.childTableId} -> t${fk.parentTableId} [id="fk-${fk.id}" tooltip=${JSON.stringify(`${fk.name}: ${mapping}; child ${card.child}, parent ${card.parent}; delete ${fk.onDelete}${fk.notTrusted ? '; NOT TRUSTED' : ''}${fk.disabled ? '; DISABLED' : ''}`)} arrowhead=vee color="${fk.disabled || fk.notTrusted ? '#ce503c' : '#7890a7'}" ${full ? `headlabel=${JSON.stringify(card.parent)} taillabel=${JSON.stringify(card.child)} labeldistance=1.5` : ''} ${fk.disabled ? 'style=dashed' : ''}];`;
  });
  return {
    tableIds: [...included], foreignKeyIds: fks.map(fk => fk.id),
    dot: `digraph schema { graph [rankdir=LR bgcolor="white" pad=0.4 nodesep=0.35 ranksep=1.3 overlap=false splines=ortho outputorder=edgesfirst]; node [shape=plain fontname="Arial" fontsize=11]; edge [fontname="Arial" fontsize=8 arrowsize=0.6];\n${nodes.join('\n')}\n${edges.join('\n')}\n}`,
    focus: focus.map(table => table.id), title: group ? groups.find(item => item.id === group).title : tableId !== undefined ? qualified(byId.get(tableId)) : full ? 'All tables - complete columns' : 'All tables - relationship overview',
  };
}
