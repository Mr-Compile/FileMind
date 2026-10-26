/* FileMind UI mockup — approved screens per docs/packs/ui-ux-foundation/screens/*.
   Each registers via FM.screen(name, fn). Behavior mirrors backend status:
   interactive = EXISTING, disabled+Planned = MISSING per docs/features/FEATURES.md. */
"use strict";

const S = (name) => (FM.state[name] ??= {});

/* shared: analysis cache per scope so Files/Organizer/Dashboard agree */
function analyzed(scope) {
  const files = FM.store.files.filter((f) => scope === "all" || f.sourceUri.startsWith(FM_DATA.roots[scope]));
  const key = `an-${scope}-${files.length}-${FM.store.rules.filter((r) => r.enabled).length}`;
  if (S("engine").key !== key) {
    S("engine").key = key;
    S("engine").result = FM.engine.analyze(files);
  }
  return S("engine").result;
}
const DECISION_CHIP = { Organize: "success", Review: "warning", Ignore: "neutral", NoOp: "offline" };
const decisionChip = (d) => `<span class="chip ${DECISION_CHIP[d] || "neutral"}"><span class="dot"></span>${FM.esc(d)}</span>`;
const esc = FM.esc, icon = FM.icon;

/* ============================= Dashboard ============================= */
FM.screen("dashboard", (m) => {
  const st = FM_DATA.storage, pct = Math.round((st.used / st.total) * 100);
  const ops = FM.store.ops, active = FM.store.activeOp;
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  const files = FM.store.files;
  const dupGroups = {};
  files.forEach((f) => { const k = f.sizeBytes; (dupGroups[k] ??= []).push(f); });
  const dupes = Object.values(dupGroups).filter((g) => g.length > 1);
  const leftovers = files.filter((f) => FM.engine.excluded(f));
  const big = files.filter((f) => f.sizeBytes > 3e9);
  const top = [...st.byKind].sort((a, b) => b[1] - a[1]).slice(0, 5);

  m.innerHTML = `
  <div class="page-head">
    <div><h1>Dashboard</h1><div class="sub">System and file health at a glance — every card links to a deeper surface.</div></div>
    <div class="actions"><button class="btn primary" id="qa-organize">${icon("organize", "sm")} Organize Downloads</button></div>
  </div>
  <div class="bento">
    <div class="card span2 clickable" data-go="files">
      <h3>${icon("db")} Storage <span class="muted tiny" style="font-weight:400">${st.drive}:</span></h3>
      ${FM.meter(pct, `C: ${pct}% used`)}
      <div class="tiny muted" style="margin:6px 0 12px">${FM.bytes(st.used)} of ${FM.bytes(st.total)} used · ${FM.bytes(st.total - st.used)} free</div>
      <div class="list">${top.map(([k, v]) => `<div class="li"><span class="grow">${k}</span><span class="muted tiny">${FM.bytes(v)}</span></div>`).join("")}</div>
    </div>
    <div class="card clickable" data-go="devices">
      <h3>${icon("devices")} Devices</h3>
      <div class="list">${FM.store.devices.map((d) => `
        <div class="li">${icon(d.icon)}<span class="grow">${esc(d.name)}</span>
        ${d.connected ? '<span class="chip success"><span class="dot"></span>Connected</span>' : '<span class="chip offline"><span class="dot"></span>Offline</span>'}</div>`).join("")}</div>
    </div>
    <div class="card">
      <h3>${icon("organize")} Quick actions</h3>
      <div class="list">
        <div class="li"><button class="btn tertiary" data-go="organizer?src=downloads" style="padding-left:0">${icon("organize", "sm")} Organize Downloads</button></div>
        <div class="li"><button class="btn tertiary" data-go="organizer?src=downloads&dry=1" style="padding-left:0">${icon("play", "sm")} Preview a dry run</button></div>
        <div class="li"><button class="btn tertiary" data-go="console" style="padding-left:0">${icon("console", "sm")} Open console host</button></div>
        <div class="li"><button class="btn tertiary" data-go="tools" style="padding-left:0">${icon("tools", "sm")} View organizer rules</button></div>
        <div class="li"><button class="btn tertiary" data-go="help" style="padding-left:0">${icon("info", "sm")} Help &amp; About</button></div>
      </div>
    </div>
    <div class="card clickable" data-go="activity">
      <h3>${icon("activity")} Active operations</h3>
      ${active
        ? `<div>${FM.statusChip(active.status)}<div class="tiny muted" style="margin:6px 0">${esc(active.label)}</div><div class="progress"><i style="width:${active.pct}%"></i></div></div>`
        : `<div class="tiny muted">No operations running right now.</div>
           <div style="margin-top:8px">Last run: ${FM.statusChip(ops[0].status)} <span class="tiny muted">${FM.dt(ops[0].createdAt)}</span></div>`}
      ${pending.length ? `<div style="margin-top:10px">${FM.statusChip("NeedsAttention")} <span class="tiny">${pending.length} Smart Inbox item(s)</span></div>` : ""}
    </div>
    <div class="card clickable" data-go="tools">
      <h3>${icon("tools")} Cleanup opportunities</h3>
      <div class="list">
        <div class="li">${icon("copy", "sm")}<span class="grow">${dupes.length} likely duplicate pair(s)</span><span class="badge">Host: dupes · F-P6</span></div>
        <div class="li">${icon("warn", "sm")}<span class="grow">${big.length} file(s) over 3 GB</span><span class="badge">Host: storage · F-P6</span></div>
        <div class="li">${icon("cancel", "sm")}<span class="grow">${leftovers.length} leftover partial download(s)</span><span class="tiny muted">auto-skipped</span></div>
      </div>
    </div>
    <div class="card span2 clickable" data-go="activity">
      <h3>${icon("clock")} Recent activity</h3>
      <div class="list">${FM.store.activity.slice(0, 4).map((a) => `
        <div class="li">${icon("activity", "sm")}<span class="grow">${esc(a.text)}</span><span class="tiny muted">${FM.dt(a.t)}</span></div>`).join("")}</div>
    </div>
  </div>`;
  m.querySelectorAll("[data-go]").forEach((el) => {
    el.addEventListener("click", (e) => { e.stopPropagation(); FM.go(el.dataset.go.replace("?", "?")); });
    el.addEventListener("keydown", (e) => e.key === "Enter" && FM.go(el.dataset.go));
  });
  m.querySelector("#qa-organize").onclick = () => FM.go("organizer?src=downloads");
});

/* ============================= Files ============================= */
FM.screen("files", (m, params) => {
  const st = S("files");
  st.scope ??= params.scope || "downloads";
  st.q ??= ""; st.kind ??= "All"; st.sort ??= "name"; st.sel ??= new Set(); st.inspect ??= null;
  const files = FM.store.files.filter((f) => f.sourceUri.startsWith(FM_DATA.roots[st.scope]));
  const res = analyzed(st.scope);
  const byUri = Object.fromEntries(res.map((a) => [a.candidate.sourceUri, a]));
  const kinds = ["All", ...new Set(files.map((f) => FM.engine.kindOf(f.extension)))];
  const scopes = Object.keys(FM_DATA.roots).filter((k) => k !== "protected");

  const apply = () => {
    let list = files;
    if (st.q) { const q = FM.engine.tokens(st.q).joined; list = list.filter((f) => FM.engine.tokens(f.displayName).joined.includes(q)); }
    if (st.kind !== "All") list = list.filter((f) => FM.engine.kindOf(f.extension) === st.kind);
    const k = st.sort;
    list = [...list].sort((a, b) =>
      k === "size" ? b.sizeBytes - a.sizeBytes
      : k === "modified" ? Date.parse(b.modifiedAt) - Date.parse(a.modifiedAt)
      : k === "kind" ? FM.engine.kindOf(a.extension).localeCompare(FM.engine.kindOf(b.extension))
      : a.displayName.localeCompare(b.displayName));
    return list;
  };

  const row = (f) => {
    const a = byUri[f.sourceUri], kind = FM.engine.kindOf(f.extension);
    const excluded = FM.engine.excluded(f);
    const status = excluded ? ["Excluded", "offline"] : [a?.decision.kind || "NoOp", DECISION_CHIP[a?.decision.kind] || "neutral"];
    return `<tr data-uri="${esc(f.sourceUri)}" class="${st.sel.has(f.sourceUri) ? "sel" : ""}">
      <td><div class="fname">${FM.fileIcon(kind)}<span class="n">${esc(f.displayName)}</span></div></td>
      <td class="muted">${esc(kind)}</td><td class="muted">${FM.bytes(f.sizeBytes)}</td>
      <td class="muted">${FM.dt(f.modifiedAt)}</td>
      <td><span class="chip ${status[1]}"><span class="dot"></span>${status[0] === "Excluded" ? "Excluded" : esc(status[0])}</span></td>
    </tr>`;
  };

  const render = () => {
    const list = apply();
    const insp = st.inspect && byUri[st.inspect] ? byUri[st.inspect] : null;
    m.innerHTML = `
    <div class="page-head">
      <div><h1>Files</h1>${FM.crumbs([{ t: "This PC", href: "#/devices" }, { t: "Demo" }, { t: st.scope[0].toUpperCase() + st.scope.slice(1) }])}</div>
      <div class="actions">
        <button class="btn" id="f-refresh">${icon("retry", "sm")} Rescan</button>
        <button class="btn primary" id="f-organize">${icon("organize", "sm")} Organize this folder</button>
      </div>
    </div>
    <div class="filters" style="margin-bottom:var(--sp-3)">
      ${scopes.map((s) => `<button class="chip clickable ${st.scope === s ? "on" : ""}" data-scope="${s}"><span class="dot"></span>${s[0].toUpperCase() + s.slice(1)}</button>`).join("")}
      <span style="flex:1"></span>
      <span class="tiny muted">${files.length} item(s)</span>
    </div>
    <div class="filters" style="margin-bottom:var(--sp-4)">
      <input class="input" id="f-q" placeholder="Search in this folder…" value="${esc(st.q)}" style="max-width:260px">
      ${kinds.map((k) => `<button class="chip clickable ${st.kind === k ? "on" : ""}" data-kind="${k}"><span class="dot"></span>${k}</button>`).join("")}
      <select class="select" id="f-sort" style="width:auto;margin-left:auto">
        ${["name", "kind", "size", "modified"].map((k) => `<option ${st.sort === k ? "selected" : ""}>${k}</option>`).join("")}
      </select>
      ${st.kind !== "All" || st.q ? `<button class="btn tertiary" id="f-clear">Clear all</button>` : ""}
    </div>
    <div class="split">
      <div class="tbl-wrap"><table class="tbl"><thead><tr>
        <th>Name</th><th>Type</th><th class="sortable">Size</th><th class="sortable">Modified</th><th>Decision</th>
      </tr></thead><tbody>${list.map(row).join("") || `<tr><td colspan="5">${st.q || st.kind !== "All" ? "Nothing matches the current filters." : "This folder is empty."}</td></tr>`}</tbody></table></div>
      <div class="card" id="inspector">
        ${insp ? `
          <h3>${icon("file")} ${esc(insp.candidate.displayName)}</h3>
          <dl class="kv">
            <dt>Location</dt><dd class="mono">${esc(insp.candidate.sourceUri)}</dd>
            <dt>Type</dt><dd>${esc(insp.classification.fileKind)} · ${esc(insp.candidate.mimeType || "unknown")}</dd>
            <dt>Size</dt><dd>${FM.bytes(insp.candidate.sizeBytes)}</dd>
            <dt>Modified</dt><dd>${FM.dt(insp.candidate.modifiedAt)}</dd>
            <dt>Decision</dt><dd>${decisionChip(insp.decision.kind)}</dd>
            <dt>Confidence</dt><dd>${FM.confBar(insp.classification.confidence)}</dd>
            ${insp.classification.subject ? `<dt>Subject</dt><dd>${esc(insp.classification.subject)}</dd>` : ""}
            ${insp.classification.project ? `<dt>Project</dt><dd>${esc(insp.classification.project)}</dd>` : ""}
            <dt>Reason</dt><dd class="tiny">${esc(insp.decision.reasons.join("; ") || insp.classification.explanation)}</dd>
          </dl>
          <details class="tech"><summary>Evidence (${insp.classification.evidence.length})</summary>
            <pre>${insp.classification.evidence.map((e) => `${e.kind}  ${e.scoreContribution > 0 ? "+" : ""}${e.scoreContribution}  ${e.detail}`).join("\n") || "none"}</pre></details>`
        : `<h3>${icon("info")} Details</h3><div class="tiny muted">Select a file to see its classification, decision and evidence — the same data <span class="mono">analyze</span> prints in the console.</div>`}
      </div>
    </div>
    ${st.sel.size ? `<div class="selbar"><strong>${st.sel.size} selected</strong>
      <button class="btn" data-act="organize">${icon("organize", "sm")} Organize</button>
      <button class="btn" data-act="move">${icon("move", "sm")} Move to…</button>
      <button class="btn" data-act="copy">${icon("copy", "sm")} Copy to…</button>
      <button class="btn" data-act="rename" ${st.sel.size > 1 ? "disabled" : ""}>${icon("rename", "sm")} Rename</button>
      <button class="btn" data-act="delete" disabled title="No delete operation exists in the core (by design)">${icon("trash", "sm")} Delete</button>
      <span style="flex:1"></span>
      <button class="btn tertiary" data-act="clear">Clear selection</button></div>` : ""}`;

    m.querySelectorAll("[data-scope]").forEach((b) => (b.onclick = () => { st.scope = b.dataset.scope; st.sel.clear(); st.inspect = null; render(); }));
    m.querySelectorAll("[data-kind]").forEach((b) => (b.onclick = () => { st.kind = b.dataset.kind; render(); }));
    m.querySelector("#f-q").oninput = FM.debounce((e) => { st.q = e.target.value; render(); m.querySelector("#f-q").focus(); }, 250);
    m.querySelector("#f-sort").onchange = (e) => { st.sort = e.target.value; render(); };
    m.querySelector("#f-clear")?.addEventListener("click", () => { st.q = ""; st.kind = "All"; render(); });
    m.querySelector("#f-refresh").onclick = () => { S("engine").key = null; render(); FM.toast("Folder rescanned", "success"); };
    m.querySelector("#f-organize").onclick = () => FM.go(`organizer?src=${st.scope}`);
    m.querySelectorAll("tbody tr[data-uri]").forEach((tr) => {
      tr.onclick = (e) => {
        const u = tr.dataset.uri;
        if (e.ctrlKey || e.metaKey) { st.sel.has(u) ? st.sel.delete(u) : st.sel.add(u); }
        else { st.inspect = u; }
        render();
      };
      tr.ondblclick = () => { st.inspect = tr.dataset.uri; render(); };
      tr.oncontextmenu = (e) => {
        e.preventDefault();
        const u = tr.dataset.uri, a = byUri[u];
        FM.menu([
          { label: "Organize", icon: "organize", run: () => FM.go(`organizer?src=${st.scope}`) },
          { label: "Copy to…", icon: "copy", run: () => FM.toast("Demo — copy is not wired in the mockup", "info") },
          { label: "Move to…", icon: "move", run: () => FM.toast("Demo — move is not wired in the mockup", "info") },
          { label: "Rename", icon: "rename", run: () => renameDialog(a.candidate) },
          { sep: true },
          { label: "Open location", icon: "open", run: () => FM.toast("Demo — would open Explorer at the folder", "info") },
          { label: "Delete", icon: "trash", disabled: true, note: "no delete op" },
        ], e.clientX, e.clientY);
      };
    });
    m.querySelectorAll("[data-act]").forEach((b) => (b.onclick = () => {
      const act = b.dataset.act;
      if (act === "clear") { st.sel.clear(); render(); }
      else if (act === "organize") FM.go(`organizer?src=${st.scope}`);
      else if (act === "rename") { const u = [...st.sel][0]; renameDialog(byUri[u].candidate); }
      else FM.toast(`Demo — '${act}' is not wired in the mockup`, "info");
    }));
  };

  const renameDialog = (f) => {
    const d = FM.dialog(`
      <h2>${icon("rename")} Rename</h2>
      <div class="field"><label>New name</label>
        <input class="input" id="rn" value="${esc(f.displayName)}">
        <div class="err" id="rn-err" hidden></div>
        <div class="hint">Validation is specific, like the real app: illegal characters are called out by name.</div></div>
      <div class="foot"><button class="btn" id="rn-x">Cancel</button><button class="btn primary" id="rn-ok">Rename</button></div>`);
    const inp = d.el.querySelector("#rn"), err = d.el.querySelector("#rn-err");
    d.el.querySelector("#rn-x").onclick = d.close;
    d.el.querySelector("#rn-ok").onclick = () => {
      const v = inp.value.trim();
      const bad = v.match(/[<>:"/\\|?*]/);
      if (!v) { err.hidden = false; err.textContent = "The name can't be empty."; return; }
      if (bad) { err.hidden = false; err.textContent = `That name contains '${bad[0]}', a character Windows can't use. Choose a different name.`; return; }
      d.close(); FM.toast(`Demo — renamed to '${v}' (not persisted)`, "success");
    };
  };
  render();
});

/* ============================= Tools ============================= */
FM.screen("tools", (m) => {
  const tool = (id, ic, title, desc, go, state) => `
    <div class="card ${go ? "clickable" : "disabled"}" ${go ? `data-go="${go}" tabindex="0" role="link"` : ""}>
      <h3>${icon(ic)} ${title} ${state ? `<span class="badge ${state === "Planned" ? "badge-missing" : ""}">${state}</span>` : ""}</h3>
      <div class="tiny muted">${desc}</div>
    </div>`;
  const humanCond = (g) => [
    ...(g.conditions || []).map((c) => `${c.field} ${c.operator} '${c.value}'`),
    ...(g.groups || []).map(humanCond),
  ].join(` ${g.operator} `);
  const humanAct = (a) =>
    a.kind === "RouteToCollection" ? `route to collection '${a.collection}'`
    : a.kind === "Move" ? `move to ${a.destinationTemplate}`
    : a.kind === "Rename" ? `rename to ${a.namingTemplate}`
    : a.kind.toLowerCase();

  m.innerHTML = `
  <div class="page-head"><div><h1>Tools</h1><div class="sub">Utilities grouped by goal. Availability reflects the real backend — nothing here is faked.</div></div></div>
  <h2>Organize</h2>
  <div class="tool-grid" style="margin-bottom:var(--sp-5)">
    ${tool("t-org", "organize", "Organize a folder", "Scan → classify → plan → safety-check → move files, with a dry-run preview first.", "organizer", null)}
    ${tool("t-inbox", "activity", "Smart Inbox", `${FM.store.inbox.filter((i) => i.status === "Pending").length} file(s) waiting for a human decision.`, "activity", `${FM.store.inbox.filter((i) => i.status === "Pending").length} pending`)}
    ${tool("t-analyze", "db", "Analyze storage", "Where the space went, by file kind.", null, "Demo")}
    ${tool("t-dupes", "copy", "Find duplicates", "Size prefilter + hashing + duplicate groups.", null, "Host: dupes · F-P6")}
    ${tool("t-clean", "trash", "Cleanup", "Large/old file analysis and safe removal suggestions.", null, "Host: storage · F-P6")}
    ${tool("t-rename", "rename", "Batch rename", "Template-based smart rename after proven extraction.", null, "Host: {Title}/{Author} tokens · F-P5")}
    ${tool("t-export", "open", "Export profile", "Versioned organizer-profile.json export/import.", null, "Host: profile · F-P10")}
  </div>
  <h2>Organizer rules</h2>
  <div class="tiny muted" style="margin-bottom:var(--sp-3)">Mirrors the Rule engine (<span class="mono">IRuleRepository</span>): priority → specificity → stop-processing → conflicts. Toggle a rule and the next analysis changes.</div>
  <div class="list" id="rule-list">
    ${FM.store.rules.map((r) => `
      <div class="li" style="align-items:flex-start;padding:var(--sp-3) 0">
        <label class="check" style="margin-top:2px"><input type="checkbox" data-rule="${r.id}" ${r.enabled ? "checked" : ""}></label>
        <div class="grow">
          <div><strong>${esc(r.name)}</strong> <span class="badge">${esc(r.provenance)}</span> <span class="badge">priority ${r.priority}</span> <span class="badge">schema v${r.schemaVersion}</span>${r.stopProcessing ? ' <span class="badge badge-warn">stop</span>' : ""}</div>
          <div class="tiny muted" style="margin-top:2px">when ${esc(humanCond(r.conditionRoot))} → ${r.actions.map(humanAct).join(", ")}</div>
        </div>
        <span class="chip ${r.enabled ? "success" : "offline"}"><span class="dot"></span>${r.enabled ? "Enabled" : "Off"}</span>
      </div>`).join("")}
  </div>
  <div id="storage-panel" style="margin-top:var(--sp-5)"></div>`;

  m.querySelectorAll("[data-go]").forEach((el) => (el.onclick = () => FM.go(el.dataset.go)));
  m.querySelectorAll("[data-rule]").forEach((cb) => (cb.onchange = () => {
    const r = FM.store.rules.find((x) => x.id === cb.dataset.rule);
    r.enabled = cb.checked;
    S("engine").key = null;
    FM.toast(`Rule '${r.name}' ${r.enabled ? "enabled" : "disabled"}`, "info");
    FM.screen("tools")(m, {});
  }));
  m.querySelector("#t-analyze").onclick = () => {
    const p = m.querySelector("#storage-panel"), st = FM_DATA.storage;
    p.innerHTML = `<div class="card"><h3>${icon("db")} Storage analysis — demo</h3>
      ${st.byKind.map(([k, v]) => `<div class="li" style="display:flex;gap:var(--sp-3);align-items:center;padding:6px 0">
        <span style="width:100px">${k}</span><div class="meter" style="flex:1"><i style="width:${Math.round((v / st.used) * 100)}%"></i></div>
        <span class="tiny muted" style="width:70px;text-align:right">${FM.bytes(v)}</span></div>`).join("")}
      <div class="tiny muted" style="margin-top:8px">Full breakdown requires hashing/metadata pipelines (F-P6) — this card shows the mock snapshot.</div></div>`;
    p.scrollIntoView({ behavior: "smooth", block: "nearest" });
  };
});

/* ============================= Search ============================= */
FM.screen("search", (m) => {
  const st = S("search");
  st.q ??= ""; st.kind ??= "All"; st.scope ??= "all"; st.size ??= "any";
  const res = analyzed("all");
  const byUri = Object.fromEntries(res.map((a) => [a.candidate.sourceUri, a]));
  const sizes = { any: [0, 1e20], small: [0, 1e6], medium: [1e6, 100e6], large: [100e6, 1e20] };

  const run = () => {
    if (!st.q && st.kind === "All" && st.scope === "all" && st.size === "any") return null;
    const q = FM.engine.tokens(st.q).joined;
    const [lo, hi] = sizes[st.size];
    return FM.store.files.filter((f) =>
      (st.scope === "all" || f.sourceUri.startsWith(FM_DATA.roots[st.scope])) &&
      (st.kind === "All" || FM.engine.kindOf(f.extension) === st.kind) &&
      f.sizeBytes >= lo && f.sizeBytes < hi &&
      (!q || FM.engine.tokens(f.displayName).joined.includes(q) || FM.engine.tokens(f.sourceUri).joined.includes(q)));
  };

  const render = () => {
    const list = run();
    const kinds = ["All", ...new Set(FM.store.files.map((f) => FM.engine.kindOf(f.extension)))];
    const chips = [
      ["scope", ["all", "downloads", "documents", "desktop"]],
      ["kind", kinds],
      ["size", ["any", "small", "medium", "large"]],
    ];
    m.innerHTML = `
    <div class="page-head"><div><h1>Search</h1><div class="sub">Forgiving search over names, types, scopes and metadata.</div></div></div>
    ${FM.banner("info", "info", `This searches the <strong>mock index</strong> only. Real full-text search exists — SQLite FTS5 via <code>search</code>/<code>ai-search</code> verbs and the Windows app (F-P9).`)}
    <div class="filters" style="margin-bottom:var(--sp-3)">
      <input class="input" id="s-q" placeholder="Search files…" value="${esc(st.q)}" style="max-width:340px" autofocus>
    </div>
    ${chips.map(([key, vals]) => `<div class="filters" style="margin-bottom:var(--sp-2)">
      <span class="tiny muted" style="width:52px">${key}</span>
      ${vals.map((v) => `<button class="chip clickable ${st[key] === v ? "on" : ""}" data-f="${key}" data-v="${v}"><span class="dot"></span>${v === "all" || v === "any" ? "Any" : v[0].toUpperCase() + v.slice(1)}</button>`).join("")}
    </div>`).join("")}
    <div style="margin-top:var(--sp-4)">
      ${list === null
        ? FM.empty("search", "Search your files", "Try 'IT421', 'alpha', 'mp4', or filter by kind, scope and size.")
        : list.length === 0
          ? FM.empty("search", "Nothing matches", st.q ? `No file in the selected scope matches '${esc(st.q)}'.` : "Nothing exists in the selected scope.", `<button class="btn" id="s-clear">Clear all filters</button>`)
          : `<div class="tiny muted" style="margin-bottom:var(--sp-2)">${list.length} result(s)</div>
             <div class="tbl-wrap"><table class="tbl"><thead><tr><th>Name</th><th>Type</th><th>Size</th><th>Modified</th><th>Location</th></tr></thead>
             <tbody>${list.map((f) => `<tr><td><div class="fname">${FM.fileIcon(FM.engine.kindOf(f.extension))}<span class="n">${esc(f.displayName)}</span></div></td>
               <td class="muted">${esc(FM.engine.kindOf(f.extension))}</td><td class="muted">${FM.bytes(f.sizeBytes)}</td>
               <td class="muted">${FM.dt(f.modifiedAt)}</td><td class="muted mono tiny">${esc(f.sourceUri)}</td></tr>`).join("")}</tbody></table></div>`}
    </div>`;
    m.querySelector("#s-q").oninput = FM.debounce((e) => { st.q = e.target.value; render(); const i = m.querySelector("#s-q"); i.focus(); i.setSelectionRange(i.value.length, i.value.length); }, 220);
    m.querySelectorAll("[data-f]").forEach((b) => (b.onclick = () => { st[b.dataset.f] = b.dataset.v; render(); }));
    m.querySelector("#s-clear")?.addEventListener("click", () => { st.q = ""; st.kind = "All"; st.scope = "all"; st.size = "any"; render(); });
  };
  render();
});

/* ============================= Devices ============================= */
FM.screen("devices", (m) => {
  m.innerHTML = `
  <div class="page-head"><div><h1>Devices</h1><div class="sub">Connected storage and what FileMind is allowed to do on each.</div></div></div>
  <div class="tool-grid">
    ${FM.store.devices.map((d) => {
      const pct = Math.round((d.storage.used / d.storage.total) * 100);
      return `<div class="card ${d.connected ? "" : "disabled"}">
        <h3>${icon(d.icon)} ${esc(d.name)}
          ${d.connected ? '<span class="chip success"><span class="dot"></span>Connected</span>' : '<span class="chip offline"><span class="dot"></span>Disconnected</span>'}
        </h3>
        ${FM.meter(pct, `${esc(d.name)} storage`)}
        <div class="tiny muted" style="margin:6px 0 10px">${FM.bytes(d.storage.used)} of ${FM.bytes(d.storage.total)} used</div>
        <dl class="kv">
          <dt>Capabilities</dt><dd>${d.capabilities.map(esc).join(" · ")}</dd>
          ${d.restrictions.length ? `<dt>Restrictions</dt><dd>${d.restrictions.map((r) => `<span class="badge badge-warn">${esc(r)}</span>`).join(" ")}</dd>` : ""}
        </dl>
        ${d.connected
          ? `<div style="margin-top:var(--sp-3);display:flex;gap:var(--sp-2)">
              <button class="btn" data-dev="${d.id}" data-act="browse">${icon("files", "sm")} Browse</button>
              <button class="btn" data-dev="${d.id}" data-act="organize">${icon("organize", "sm")} Organize</button></div>`
          : `<div class="tiny" style="margin-top:var(--sp-3)">${icon("info", "sm")} ${esc(d.reconnect || "Reconnect this device to use it.")}</div>
             <div style="margin-top:var(--sp-3)"><button class="btn" disabled>Browse</button> <button class="btn" disabled>Organize</button></div>`}
      </div>`;
    }).join("")}
  </div>`;
  m.querySelectorAll("[data-act]").forEach((b) => (b.onclick = () =>
    b.dataset.act === "organize" ? FM.go("organizer?src=downloads") : FM.go("files")));
});

/* ============================= Activity & Operation Center ============================= */
FM.screen("activity", (m) => {
  const active = FM.store.activeOp;
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  const done = (o) => o.items.filter((i) => i.status === "Committed").length;
  const failed = (o) => o.items.filter((i) => i.status === "Failed").length;
  const canUndo = (o) => o.status === "Committed" && !o.undoOfOperationId && !FM.store.ops.some((x) => x.undoOfOperationId === o.operationId);

  m.innerHTML = `
  <div class="page-head"><div><h1>Activity</h1><div class="sub">Operation center — active work, history, undo, and the Smart Inbox.</div></div></div>

  ${active ? `<div class="card" style="margin-bottom:var(--sp-4)"><h3>${icon("activity")} ${esc(active.label)}</h3>
    <div style="display:flex;align-items:center;gap:var(--sp-3)">
      ${FM.statusChip(active.status)}<div class="progress" style="flex:1"><i style="width:${active.pct}%"></i></div>
      <span class="tiny muted">${active.done}/${active.total}</span></div>
    <div class="tiny muted" style="margin-top:6px">${esc(active.current || "")}</div></div>` : ""}

  <h2>Operations</h2>
  <div class="list" style="margin-bottom:var(--sp-6)">
    ${FM.store.ops.map((o) => `
      <div class="li" style="align-items:flex-start;padding:var(--sp-3) 0">
        <div class="grow">
          <div style="display:flex;gap:var(--sp-2);align-items:center;flex-wrap:wrap">
            ${FM.statusChip(o.status)}
            <span class="mono tiny">${o.operationId.slice(0, 12)}…</span>
            ${o.undoOfOperationId ? '<span class="badge">undo</span>' : ""}
            <span class="tiny muted">${FM.dt(o.createdAt)}</span>
          </div>
          <div class="tiny muted" style="margin-top:3px">
            ${done(o)} moved${failed(o) ? ` · <span style="color:var(--fm-error)">${failed(o)} failed</span>` : ""} · plan <span class="mono">${o.planId.slice(0, 8)}</span>
          </div>
          <details class="tech"><summary>Items (${o.items.length})</summary>
            <pre>${o.items.map((i) => `${i.operation.padEnd(12)} ${i.originalLocation}\n  -> ${i.resultingLocation ?? "—"}  [${i.status}]${i.error ? `\n  error ${i.error.code}: ${i.error.message}` : ""}`).join("\n")}</pre></details>
        </div>
        <div style="display:flex;gap:var(--sp-2);flex:none">
          ${canUndo(o) ? `<button class="btn" data-undo="${o.operationId}">${icon("undo", "sm")} Undo</button>` : ""}
          ${o.status === "NeedsAttention" ? `<button class="btn" data-retry="${o.operationId}">${icon("retry", "sm")} Retry failed</button>` : ""}
        </div>
      </div>`).join("")}
  </div>

  <h2>Smart Inbox ${pending.length ? `<span class="badge badge-warn">${pending.length} pending</span>` : ""}</h2>
  <div class="tiny muted" style="margin-bottom:var(--sp-3)">Files the engine couldn't place confidently — a human decides. Mirrors <span class="mono">DecisionKind.Review</span>.</div>
  <div class="list" id="inbox">
    ${pending.length ? pending.map((i) => `
      <div class="li" style="align-items:flex-start;padding:var(--sp-3) 0">
        <div class="grow">
          <div>${FM.fileIcon("Document")} <strong>${esc(i.fileName)}</strong></div>
          <div class="tiny muted" style="margin-top:2px">${esc(i.summary)}</div>
          <div class="tiny" style="margin-top:2px;color:var(--fm-warning)">${i.reasons.map(esc).join(" ")}</div>
        </div>
        <div style="display:flex;gap:var(--sp-2);flex:none">
          <button class="btn primary" data-ok="${i.id}">${icon("check", "sm")} Send to ${esc(i.suggestedDest)}</button>
          <button class="btn" data-no="${i.id}">Keep in place</button>
        </div>
      </div>`).join("")
      : `<div class="li"><span class="tiny muted">Inbox is empty — everything pending your call is handled.</span></div>`}
  </div>

  <h2 style="margin-top:var(--sp-6)">Canonical operation states</h2>
  <div class="tiny muted" style="margin-bottom:var(--sp-2)">Every state pairs color with icon and label — never color alone (components/03).</div>
  <div class="legend">${["Ready", "Queued", "Checking", "Running", "Paused", "Completed", "Completed with warnings", "Failed", "Cancelled", "Offline", "Unsupported", "Permission required"].map(FM.statusChip).join(" ")}</div>`;

  m.querySelectorAll("[data-undo]").forEach((b) => (b.onclick = async () => {
    const o = FM.store.ops.find((x) => x.operationId === b.dataset.undo);
    const okc = await FM.confirm({
      title: "Undo this operation?",
      body: `${done(o)} moved file(s) will go back to their original locations. Undo is fingerprint-checked; a file that changed since gets flagged NeedsReview instead of silently moved.`, ok: "Undo operation",
    });
    if (!okc) return;
    const undoRec = {
      operationId: FM.uid(32), planId: o.planId, status: "RolledBack",
      createdAt: new Date().toISOString(), committedAt: new Date().toISOString(),
      undoOfOperationId: o.operationId, error: null,
      items: o.items.filter((i) => i.status === "Committed").map((i) => ({
        itemId: FM.uid(8), operation: "Move", originalLocation: i.resultingLocation,
        resultingLocation: i.originalLocation, status: "Committed", verification: "verified", error: null,
      })),
    };
    FM.store.ops.unshift(undoRec);
    FM.store.activity.unshift({ t: undoRec.createdAt, text: `Undo ${o.operationId.slice(0, 8)} — ${undoRec.items.length} file(s) restored`, opId: undoRec.operationId });
    FM.bus.emit("ops");
    FM.toast("Operation undone — files restored", "success");
    FM.screen("activity")(m, {});
  }));
  m.querySelectorAll("[data-retry]").forEach((b) => (b.onclick = () => FM.toast("Demo — retry would re-run only the failed item", "info")));
  m.querySelectorAll("[data-ok]").forEach((b) => (b.onclick = () => {
    const i = FM.store.inbox.find((x) => x.id === b.dataset.ok);
    i.status = "Approved";
    FM.store.activity.unshift({ t: new Date().toISOString(), text: `Smart Inbox: '${i.fileName}' sent to ${i.suggestedDest}`, opId: null });
    FM.bus.emit("ops"); FM.toast(`'${i.fileName}' queued for ${i.suggestedDest}`, "success");
    FM.screen("activity")(m, {});
  }));
  m.querySelectorAll("[data-no]").forEach((b) => (b.onclick = () => {
    const i = FM.store.inbox.find((x) => x.id === b.dataset.no);
    i.status = "Dismissed";
    FM.bus.emit("ops"); FM.toast(`'${i.fileName}' stays where it is`, "info");
    FM.screen("activity")(m, {});
  }));
});

/* ============================= Settings ============================= */
FM.screen("settings", (m) => {
  const s = FM.store.settings;
  const sect = (t, desc, body) => `<div class="sect"><div class="sect-head"><h2>${t}</h2><span class="tiny muted">${desc}</span></div>${body}</div>`;
  const radio = (name, vals, cur, disabled = false) =>
    `<div class="radio-row">${vals.map((v) => `<label class="check"><input type="radio" name="${name}" value="${v}" ${v === cur ? "checked" : ""} ${disabled ? "disabled" : ""}>${v[0].toUpperCase() + v.slice(1)}</label>`).join("")}</div>`;

  m.innerHTML = `
  <div class="page-head"><div><h1>Settings</h1><div class="sub">Preferences with plain-language consequences. Dangerous things live under Advanced.</div></div></div>

  ${sect("General", "How FileMind behaves on start", `
    <div class="field"><label>Default folder shown on open</label>
      <select class="select" id="st-scope" style="max-width:280px">${["downloads", "documents", "desktop"].map((v) => `<option ${v === (s.defaultScope || "downloads") ? "selected" : ""}>${v[0].toUpperCase() + v.slice(1)}</option>`).join("")}</select></div>`)}

  ${sect("Appearance", "Theme changes never alter meaning", `
    <div class="field"><label>Theme</label>${radio("st-theme", ["system", "light", "dark"], FM.theme.get())}</div>
    <label class="check"><input type="checkbox" id="st-density" ${FM.density.get() ? "checked" : ""}> Compact density (Windows file-heavy views)</label>`)}

  ${sect("File management", "Conflicts and organization mode", `
    <div class="field"><label>When a destination already exists</label>
      <select class="select" id="st-conflict" style="max-width:280px">${FM_DATA.enums.ConflictPolicy.map((v) => `<option ${v === s.conflictPolicy ? "selected" : ""}>${v}</option>`).join("")}</select>
      <div class="hint">Default is <strong>Keep Both</strong> — FileMind never silently overwrites.</div></div>
    <div class="field"><label>Organization mode <span class="badge">${s.organizationMode}</span></label>
      ${radio("st-mode", ["Manual", "Smart", "Automatic", "AI-assisted", "Local-only"], s.organizationMode)}
      <div class="hint">Saved to <code>organization.mode</code> — the same key the Android app writes; the host reads it too.</div></div>`)}

  ${sect("Privacy & safety", "What FileMind may touch", `
    <div class="tiny muted" style="margin-bottom:var(--sp-2)">Watched folders (FileSystemWatcher automation exists via the host <code>watch</code> verbs — <strong>F-P4</strong>):</div>
    ${s.watchedFolders.length ? s.watchedFolders.map((f) => `<div class="li"><span class="grow mono tiny">${esc(f)}</span></div>`).join("") : `<div class="tiny muted" style="margin-bottom:var(--sp-2)">No watched folders yet.</div>`}
    <button class="btn" disabled title="Managed via host watch add">${icon("plus", "sm")} Add watched folder</button>
    <div class="li" style="margin-top:var(--sp-2)">${icon("lock", "sm")}<span class="grow">On-device only — no cloud account; names and metadata stay on this PC</span></div>`)}

  ${sect("Notifications", "Batched, never per-file spam", `
    ${[["operationDone", "Operation completed"], ["warnings", "Warnings and failures"], ["smartInbox", "Smart Inbox arrivals"]].map(([k, l]) =>
      `<label class="check" style="margin-bottom:6px"><input type="checkbox" data-notif="${k}" ${s.notifications[k] ? "checked" : ""}> ${l}</label>`).join("")}`)}

  ${sect("Advanced", "Technical details behind an explicit disclosure", `
    <dl class="kv">
      <dt>Database</dt><dd class="mono">${esc(s.dbPath)}</dd>
      <dt>Profile schema</dt><dd class="mono">schemaVersion ${s.schemaVersion}</dd>
      <dt>AI provider</dt><dd>Groq — optional, off by default <span class="badge">Existing · opt-in · F-P7</span></dd>
    </dl>
    <div style="display:flex;gap:var(--sp-2);margin-top:var(--sp-3)">
      <button class="btn" disabled title="Exists via host profile verbs">${icon("open", "sm")} Export profile <span class="badge">Host: profile</span></button>
      <button class="btn destructive" id="st-reset">Reset demo data</button>
    </div>
    <div class="li" style="margin-top:var(--sp-3)"><button class="btn tertiary" id="st-diag" style="padding-left:0">${icon("db", "sm")} Diagnostics — device info, storage, support copy</button></div>
    <div class="hint" style="margin-top:6px">Reset only clears this session's mock operations and inbox — medium-tier, reversible on reload.</div>`)}

  ${sect("Updates", "How new versions arrive", `
    <dl class="kv">
      <dt>Version</dt><dd class="mono">mockup 1.0</dd>
    </dl>
    <div class="tiny muted">No auto-update channel is wired — new builds are installed manually.</div>`)}

  ${sect("Help & About", "Guides, support info, and app details", `
    <div class="li"><button class="btn tertiary" id="st-help" style="padding-left:0">${icon("info", "sm")} Help &amp; guides — how organizing, the inbox, and undo work</button></div>
    <dl class="kv" style="margin-top:var(--sp-3)">
      <dt>App</dt><dd>FileMind — offline-first, privacy-first, reversible file organizer</dd>
      <dt>This preview</dt><dd>UI mockup 1.0 · mirrors backend M0–M3 + approved ui-ux-foundation 1.0.0</dd>
      <dt>Source</dt><dd class="mono">mockup/ — sync enforced by docs/rules/MOCKUP_RULES.md</dd>
    </dl>`)}`;

  m.querySelector("#st-scope").onchange = (e) => { s.defaultScope = e.target.value; FM.toast("Default folder saved", "success"); };
  m.querySelectorAll("[name=st-theme]").forEach((r) => (r.onchange = () => { FM.theme.set(r.value); FM.toast(`Theme: ${r.value}`, "success"); }));
  m.querySelector("#st-density").onchange = (e) => FM.density.set(e.target.checked);
  m.querySelector("#st-conflict").onchange = (e) => { s.conflictPolicy = e.target.value; FM.toast(`Conflict policy: ${s.conflictPolicy}`, "success"); };
  m.querySelectorAll("[data-notif]").forEach((cb) => (cb.onchange = () => { s.notifications[cb.dataset.notif] = cb.checked; FM.toast("Notification preference saved", "success"); }));
  m.querySelectorAll("[name=st-mode]").forEach((r) => (r.onchange = () => { s.organizationMode = r.value; FM.toast(`Mode: ${r.value}`, "success"); FM.screen("settings")(m, {}); }));
  m.querySelector("#st-help").onclick = () => FM.go("help");
  m.querySelector("#st-diag").onclick = () => FM.go("help?topic=diagnostics");
  m.querySelector("#st-reset").onclick = async () => {
    const ok = await FM.confirm({ title: "Reset demo data?", body: "Mock operations and Smart Inbox items return to their seeded state on next reload. Nothing real is affected.", ok: "Reset demo", kind: "destructive" });
    if (ok) { FM.store = null; FM.initStore(); FM.toast("Demo data reset", "success"); FM.screen("settings")(m, {}); }
  };
});

/* ============================= Help & About ============================= */
const HELP_TOPICS = [
  ["about", "About FileMind", "Version, purpose, and project links"],
  ["getting-started", "Getting started", "From install to your first safe organize"],
  ["guide", "User guide", "Organizing, rules, Smart Inbox, undo, and more"],
  ["whats-new", "What's new", "Release highlights"],
  ["troubleshooting", "Troubleshooting", "Fix common problems"],
  ["diagnostics", "Diagnostics", "Device info, storage, support copy"],
  ["feedback", "Feedback", "Send a report or suggestion"],
  ["credits", "Credits", "Who built FileMind"],
  ["licenses", "Licenses", "App and dependency licenses"],
];
const HELP_BODIES = {
  about: `<dl class="kv"><dt>FileMind</dt><dd>Offline-first, privacy-first, reversible file organizer.</dd><dt>Version</dt><dd class="mono">mockup 1.0 (demo data)</dd><dt>Source</dt><dd class="mono">github.com/FileMind (demo link)</dd></dl>`,
  "getting-started": `<ol style="margin:0;padding-left:18px" class="tiny">
    <li>Pick a folder to watch or scan (Files).</li>
    <li>Preview an organize run — nothing moves without your confirm.</li>
    <li>Review the plan: adds, moves, conflicts, skips.</li>
    <li>Confirm to execute — every move is journaled.</li>
    <li>Undo any run from Activity.</li></ol>
    <button class="btn primary" data-go="organizer?src=downloads" style="margin-top:12px">Start organizing</button>`,
  guide: `<div class="list">
    <div class="li">${icon("organize","sm")}<span class="grow"><strong>Organize</strong> — rules classify files into destination folders. Dry-run first.</span></div>
    <div class="li">${icon("info","sm")}<span class="grow"><strong>Smart Inbox</strong> — low-confidence matches wait for your review.</span></div>
    <div class="li">${icon("undo","sm")}<span class="grow"><strong>Undo</strong> — every executed plan is reversible from Activity.</span></div>
    <div class="li">${icon("tools","sm")}<span class="grow"><strong>Rules</strong> — per-kind targets in Tools; conflicts default to Keep Both.</span></div></div>`,
  "whats-new": `<dl class="kv"><dt>mockup 1.0</dt><dd>Dashboard, Files, Organizer preview, Tools, Activity, Settings, Help &amp; About.</dd></dl>`,
  troubleshooting: `<div class="list">
    <div class="li">${icon("warn","sm")}<span class="grow"><strong>A scan found nothing</strong> — check the scope in Files; mock demo only indexes seeded folders.</span></div>
    <div class="li">${icon("error","sm")}<span class="grow"><strong>A move was skipped</strong> — conflicts default to Keep Both; check the plan's skip list.</span></div>
    <div class="li">${icon("db","sm")}<span class="grow"><strong>Still stuck</strong> — copy Diagnostics and attach it to a report.</span></div></div>
    <button class="btn" data-topic="diagnostics" style="margin-top:12px">Open diagnostics</button>`,
  feedback: `<div class="tiny">Found a bug or have a suggestion? Attach diagnostics and describe what you expected vs what happened.</div>
    <div style="display:flex;gap:var(--sp-2);margin-top:12px"><button class="btn" id="fb-copy">Copy diagnostics + send</button></div>`,
  credits: `<dl class="kv"><dt>Product</dt><dd>FileMind team</dd><dt>Mockup</dt><dd class="mono">mockup/ — enforced by docs/rules/MOCKUP_RULES.md</dd></dl>`,
  licenses: `<dl class="kv"><dt>FileMind</dt><dd>Proprietary — demo mockup</dd><dt>Dependencies</dt><dd>None — vanilla HTML/CSS/JS, runs from file://</dd></dl>`,
};
FM.screen("help", (m, params) => {
  const topic = params.topic;
  if (topic && (HELP_BODIES[topic] || topic === "diagnostics")) {
    const title = (HELP_TOPICS.find((t) => t[0] === topic) || [topic, topic])[1];
    m.innerHTML = `
    <div class="page-head"><div><button class="btn tertiary" data-go="help" style="padding-left:0">${icon("back","sm")} Help</button>
      <h1>${title}</h1></div></div>
    <div class="card" style="max-width:720px">${topic === "diagnostics" ? `
      <dl class="kv"><dt>Device</dt><dd class="mono">Demo desktop · ${navigator.userAgent.includes("Win") ? "Windows" : "unknown OS"}</dd>
      <dt>Version</dt><dd class="mono">mockup 1.0</dd>
      <dt>Catalog</dt><dd class="mono">${FM.store.files.length} files · ${FM.store.ops.length} ops · ${FM.store.inbox.length} inbox</dd></dl>
      <label class="li" style="gap:8px"><input type="checkbox" id="dg-paths"> Include local paths (off by default — privacy)</label>
      <button class="btn" id="dg-copy" style="margin-top:10px">${icon("copy","sm")} Copy diagnostic info</button>
      <div class="tiny muted" style="margin-top:6px">Excludes paths unless you opt in.</div>` : HELP_BODIES[topic]}</div>`;
    m.querySelectorAll("[data-go]").forEach((el) => (el.onclick = () => FM.go(el.dataset.go)));
    m.querySelectorAll("[data-topic]").forEach((el) => (el.onclick = () => FM.go("help?topic=" + el.dataset.topic)));
    const cp = m.querySelector("#dg-copy");
    if (cp) cp.onclick = async () => {
      const paths = m.querySelector("#dg-paths")?.checked;
      const txt = `FileMind mockup 1.0 | ${navigator.userAgent} | files:${FM.store.files.length} ops:${FM.store.ops.length}${paths ? " | scope:" + FM_DATA.roots.downloads : ""}`;
      try { await navigator.clipboard.writeText(txt); FM.toast("Diagnostics copied", "success"); }
      catch { FM.toast("Clipboard blocked — select the info above manually", "warn"); }
    };
    const fb = m.querySelector("#fb-copy");
    if (fb) fb.onclick = () => FM.toast("Demo — no real feedback channel in the mockup", "info");
    return;
  }
  m.innerHTML = `
  <div class="page-head"><div><h1>Help &amp; About</h1>
    <div class="sub">Guides, support info, and app details — every topic is a real destination.</div></div></div>
  <div class="card" style="max-width:720px"><div class="list">${HELP_TOPICS.map(([id, t, d]) => `
    <div class="li clickable" data-topic="${id}" tabindex="0">${icon("info","sm")}<span class="grow"><strong>${t}</strong><div class="tiny muted">${d}</div></span>${icon("chevR","sm")}</div>`).join("")}
  </div></div>`;
  m.querySelectorAll("[data-topic]").forEach((el) => {
    el.onclick = () => FM.go("help?topic=" + el.dataset.topic);
    el.onkeydown = (e) => e.key === "Enter" && FM.go("help?topic=" + el.dataset.topic);
  });
});
