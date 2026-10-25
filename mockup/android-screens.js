/* ============================================================================
   FileMind mockup — Android shell + screens
   PLAN-021/022 presentation layer: Material 3 Minimal Utility, phone-first.
   Reuses FM_DATA + FM.engine + FM helpers from app.js — the deterministic
   pipeline semantics (scan → analyze → plan → safety → execute → undo)
   are identical; only the shell and presentation are Android-specific.
   PLAN-022: zero horizontal scroll — pickers and filters live in M3 bottom
   sheets; feature parity with the Windows mockup via Android idioms
   (sheets, FAB, selection bar, system Back), never a visual clone.
   ============================================================================ */
"use strict";
const A = {};
window.A = A;

/* SAF-style path display — the same URIs, shown as Android trees */
A.saf = (uri) =>
  String(uri ?? "")
    .replace(/^C:\\Users\\demo\\/i, "Internal storage/")
    .replace(/^C:\\/, "Internal storage/")
    .replaceAll("\\", "/");

A.title = (t) => {
  document.getElementById("appbar-title").textContent = t;
};

/* ---------- back stack (system Back integration) ---------- */
/* No in-app back button — Android's system Back unwinds this stack; the
   mockup maps it to Escape (sheets → selection → screens). */
A.backStack = [];
A.popBack = () => { const top = A.backStack.pop(); top?.fn(); };
/* route change: drop the stack AND remove any open sheet DOM — otherwise a
   scrim left over from the previous screen stays on top of the new route */
A.clearBack = () => {
  A.backStack.length = 0;
  document.querySelectorAll("#dialog-root .a-scrim").forEach((x) => x.remove());
};

/* ---------- M3 modal bottom sheet ---------- */
A.sheet = ({ title, body = "", actions = [] }) => {
  const ov = document.createElement("div");
  ov.className = "a-scrim";
  ov.innerHTML = `
    <div class="a-sheet" role="dialog" aria-modal="true" aria-label="${FM.esc(title)}">
      <div class="a-sheet-handle"></div>
      <div class="a-sheet-title">${FM.esc(title)}</div>
      <div class="a-sheet-body">${body}</div>
      <div class="a-sheet-actions"></div>
    </div>`;
  const close = () => { ov.remove(); const i = A.backStack.indexOf(rec); if (i >= 0) A.backStack.splice(i, 1); };
  const rec = { fn: close, label: "sheet" };
  ov.addEventListener("click", (e) => e.target === ov && close());
  const box = ov.querySelector(".a-sheet-actions");
  actions.forEach((a) => {
    const b = document.createElement("button");
    b.className = `btn ${a.kind || ""}`;
    b.innerHTML = (a.icon ? FM.icon(a.icon, "sm") : "") + FM.esc(a.label);
    b.onclick = () => { close(); a.run?.(); };
    box.append(b);
  });
  document.getElementById("dialog-root").append(ov);
  A.backStack.push(rec);
  return { el: ov, close };
};

/* ---------- picker sheet — the phone answer to dropdowns/chip strips ----------
   A single-select list in a bottom sheet. Replaces horizontally scrolling
   chip rows; used for folder scope, sort, and single pickers. */
A.picker = ({ title, items, selected, onPick }) => {
  const sh = A.sheet({ title });
  const body = sh.el.querySelector(".a-sheet-body");
  body.innerHTML = `<div class="a-list tight">${items.map((it, i) => `
    <button class="a-opt${it.value === selected ? " on" : ""}" data-i="${i}" ${it.disabled ? "disabled" : ""}>
      <span class="a-opt-txt"><span class="a-opt-title">${FM.esc(it.label)}</span>
      ${it.sub ? `<span class="a-opt-sub">${it.sub}</span>` : ""}</span>
      ${it.right ? `<span class="a-opt-right">${it.right}</span>` : ""}
      <span class="a-opt-check">${it.value === selected ? FM.icon("check", "sm") : ""}</span>
    </button>`).join("")}</div>`;
  body.querySelectorAll("[data-i]").forEach((b) => (b.onclick = () => {
    const it = items[+b.dataset.i];
    sh.close(); onPick(it.value);
  }));
};

/* ---------- filter sheet — multi-group single-select (kind/size/sort) --------
   One Filters button opens this; each group is a radio list. Stays open so
   several groups can be set; "Done"/scrim/Back closes, "Clear all" resets. */
A.filterSheet = ({ groups, state, onChange, onClear }) => {
  const sh = A.sheet({ title: "Filters" });
  const body = sh.el.querySelector(".a-sheet-body");
  const paint = () => {
    body.innerHTML = groups.map((g) => `
      <div class="a-sec">${FM.esc(g.title)}</div>
      <div class="a-list tight">${g.options.map((o) => `
        <button class="a-opt${state[g.key] === o.value ? " on" : ""}" data-g="${g.key}" data-v="${FM.esc(o.value)}">
          <span class="a-opt-txt"><span class="a-opt-title">${FM.esc(o.label)}</span></span>
          <span class="a-opt-check">${state[g.key] === o.value ? FM.icon("check", "sm") : ""}</span>
        </button>`).join("")}</div>`).join("");
    body.querySelectorAll("[data-g]").forEach((b) => (b.onclick = () => {
      state[b.dataset.g] = b.dataset.v; onChange?.(b.dataset.g, b.dataset.v); paint();
    }));
  };
  paint();
  const box = sh.el.querySelector(".a-sheet-actions");
  box.innerHTML = "";
  const clr = document.createElement("button");
  clr.className = "btn ghost";
  clr.textContent = "Clear all";
  clr.onclick = () => { onClear(); paint(); };
  const done = document.createElement("button");
  done.className = "btn primary";
  done.textContent = "Done";
  done.onclick = () => sh.close();
  box.append(clr, done);
};

/* ---------- filter button with active-count badge ---------- */
A.filterBtn = (count, onTap) => {
  const b = document.createElement("button");
  b.className = "a-filterbtn";
  b.setAttribute("aria-label", `Filters${count ? `, ${count} active` : ""}`);
  b.innerHTML = `${FM.icon("filter", "sm")}<span>Filters</span>${count ? `<span class="a-badge">${count}</span>` : ""}`;
  b.onclick = onTap;
  return b;
};

/* ---------- M3 list rows ---------- */
A.row = ({ icon, title, sub = "", right = "", onTap, onLong, selectable = false, selected = false }) => {
  const d = document.createElement("button");
  d.className = `a-row${selected ? " selected" : ""}`;
  if (selectable) d.setAttribute("aria-pressed", selected ? "true" : "false");
  d.innerHTML = `
    ${selectable ? `<span class="a-check">${selected ? FM.icon("check", "sm") : ""}</span>` : ""}
    ${icon ? `<span class="a-row-ic">${icon}</span>` : ""}
    <span class="a-row-txt"><span class="a-row-title">${title}</span>${sub ? `<span class="a-row-sub">${sub}</span>` : ""}</span>
    ${right ? `<span class="a-row-right">${right}</span>` : ""}`;
  d.onclick = () => onTap?.(d);
  d.oncontextmenu = (e) => { e.preventDefault(); onLong?.(d); };
  return d;
};

/* ---------- switch row (Settings/Organizer toggles) ---------- */
A.switchRow = ({ icon, title, sub = "", checked, onChange }) => {
  const d = document.createElement("div");
  d.className = "a-row";
  d.innerHTML = `
    ${icon ? `<span class="a-row-ic">${icon}</span>` : ""}
    <span class="a-row-txt"><span class="a-row-title">${title}</span>${sub ? `<span class="a-row-sub">${sub}</span>` : ""}</span>
    <input class="a-switch" type="checkbox" role="switch" ${checked ? "checked" : ""} aria-label="${FM.esc(title)}">`;
  const cb = d.querySelector(".a-switch");
  cb.onchange = () => onChange(cb.checked);
  return d;
};

/* ---------- selection model (contextual app bar) ---------- */
A.sel = {
  active: false, items: new Set(), rerender: null,
  enter(rerender) { this.active = true; this.rerender = rerender; this.sync(); },
  toggle(id) { this.items.has(id) ? this.items.delete(id) : this.items.add(id); if (!this.items.size) this.exit(); else this.sync(); this.rerender?.(); },
  exit() { this.active = false; this.items.clear(); this.rerender?.(); this.sync(); },
  sync() {
    const bar = document.getElementById("selbar");
    bar.hidden = !this.active;
    document.getElementById("sel-count").textContent = `${this.items.size} selected`;
  },
};

/* ---------- shared fragments ---------- */
A.demoNote = () =>
  `<div class="a-note">${FM.icon("info", "sm")}<span>Mock data — no real files are touched. Native app: Compose (PLAN-021).</span></div>`;

/* SAF grant list — mirrors UriGrantStore.grants() on device */
A.grants = ["Downloads", "Documents"];

A.scopes = () => [
  ["Downloads", FM_DATA.roots.downloads],
  ["Documents", FM_DATA.roots.documents],
  ["Desktop", FM_DATA.roots.desktop],
];

A.storageCard = () => {
  const phone = FM.store.devices.find((d) => d.kind === "android") || FM.store.devices[0];
  const pct = Math.round((phone.storage.used / phone.storage.total) * 100);
  return `<section class="a-card">
    <div class="a-card-head">${FM.icon("phone")}<h2>This device</h2><span class="grow"></span>${FM.statusChip(pct > 85 ? "NeedsAttention" : "Connected")}</div>
    ${FM.meter(pct, "Phone storage")}
    <div class="a-card-sub">${FM.bytes(phone.storage.used)} used of ${FM.bytes(phone.storage.total)} · Folder access: ${A.grants.length} of ${A.scopes().length} trees granted</div>
  </section>`;
};

/* Active-operation card — shared by Dashboard + Activity (Task Center) */
A.activeOpCard = () => {
  const op = FM.store.activeOp;
  if (!op) return "";
  return `<section class="a-card">
    <div class="a-card-head">${FM.icon("activity")}<h2>Running now</h2><span class="grow"></span>${FM.statusChip(op.status)}</div>
    <div class="a-card-sub">${FM.esc(op.label)} · ${op.done}/${op.total}${op.current ? ` — ${FM.esc(op.current)}` : ""}</div>
    <div class="a-progress" role="progressbar" aria-valuenow="${op.pct}" aria-valuemin="0" aria-valuemax="100"><i style="width:${op.pct}%"></i></div>
  </section>`;
};

A.fileRow = (f, { selecting, selected, onToggle, onOpen } = {}) => {
  const kind = FM.engine.kindOf(f.extension);
  const el = A.row({
    icon: FM.fileIcon(kind),
    title: FM.esc(f.displayName),
    sub: `${FM.esc(kind)} · ${FM.bytes(f.sizeBytes)} · ${FM.dt(f.modifiedAt)}`,
    right: selecting ? "" : FM.icon("chevR", "sm"),
    selectable: selecting, selected,
    onTap: () => (selecting ? onToggle() : onOpen()),
    onLong: () => onToggle(),
  });
  return el;
};

/* Why? — decision + evidence detail block for inspector sheets (parity with
   the Windows inspector pane: decision, confidence, subject/project, evidence) */
A.whyBlock = (a) => {
  if (!a) return "";
  const c = a.classification;
  return `<div class="a-detail">
    <div><span>Decision</span><b>${FM.statusChip(a.decision.kind)}</b></div>
    <div><span>Confidence</span><b>${FM.confBar(c.confidence)}</b></div>
    ${c.subject ? `<div><span>Subject</span><b>${FM.esc(c.subject)}</b></div>` : ""}
    ${c.project ? `<div><span>Project</span><b>${FM.esc(c.project)}</b></div>` : ""}
    <div><span>Why</span><b>${FM.esc(a.decision.reasons.join("; ") || c.explanation)}</b></div>
  </div>
  <details class="a-ev"><summary>Evidence (${c.evidence.length})</summary>
    <pre>${c.evidence.map((e) => `${e.kind}  ${e.scoreContribution > 0 ? "+" : ""}${e.scoreContribution}  ${e.detail}`).join("\n") || "none"}</pre></details>`;
};

/* shared analysis cache per scope — Files/Organizer/Dashboard agree */
A.analyzed = (scope) => {
  const files = FM.store.files.filter((f) => f.sourceUri.startsWith(scope));
  const key = `an-${scope}-${files.length}-${FM.store.rules.filter((r) => r.enabled).length}`;
  if (A._an?.key !== key) A._an = { key, result: FM.engine.analyze(files) };
  return A._an.result;
};

/* ========================================================================= */
/* Home — Android dashboard                                                    */
/* ========================================================================= */
const home = (m) => {
  A.title("FileMind");
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  const files = FM.store.files;
  const dupGroups = {};
  files.forEach((f) => { const k = f.sizeBytes; (dupGroups[k] ??= []).push(f); });
  const dupes = Object.values(dupGroups).filter((g) => g.length > 1);
  const leftovers = files.filter((f) => FM.engine.excluded(f));
  const big = files.filter((f) => f.sizeBytes > 3e9);
  m.innerHTML = `
    <div class="a-page">
      <div class="a-hero">
        <div class="a-hero-t">Your files, sorted themselves.</div>
        <div class="a-hero-s">Offline-first organizer · on-device intelligence</div>
      </div>
      ${A.storageCard()}
      ${A.activeOpCard()}
      <section class="a-card tappable" data-go="organizer">
        <div class="a-card-head">${FM.icon("organize")}<h2>Organize</h2><span class="grow"></span>${FM.icon("chevR")}</div>
        <div class="a-card-sub">Scan a folder, preview the plan, approve — every step is reversible.</div>
      </section>
      <section class="a-card tappable" data-go="activity">
        <div class="a-card-head">${FM.icon("warn")}<h2>Smart Inbox</h2><span class="grow"></span>
          ${pending.length ? `<span class="a-badge">${pending.length}</span>` : FM.icon("chevR")}</div>
        <div class="a-card-sub">${pending.length ? `${pending.length} file(s) need your call.` : "Nothing waiting for review."}</div>
      </section>
      <section class="a-card tappable" data-go="devices">
        <div class="a-card-head">${FM.icon("devices")}<h2>Devices</h2><span class="grow"></span>${FM.icon("chevR")}</div>
        <div class="a-list tight">${FM.store.devices.map((d) => `
          <div class="a-row"><span class="a-row-ic">${FM.icon(d.icon)}</span>
            <span class="a-row-txt"><span class="a-row-title">${d.kind === "android" ? "This device" : FM.esc(d.name)}</span></span>
            <span class="a-row-right">${FM.statusChip(d.kind === "android" || d.connected ? "Connected" : "Missing")}</span></div>`).join("")}</div>
      </section>
      <section class="a-card tappable" data-go="tools">
        <div class="a-card-head">${FM.icon("tools")}<h2>Cleanup opportunities</h2><span class="grow"></span>${FM.icon("chevR")}</div>
        <div class="a-list tight">
          <div class="a-row"><span class="a-row-ic">${FM.icon("copy", "sm")}</span><span class="a-row-txt"><span class="a-row-title">${dupes.length} likely duplicate pair(s)</span></span><span class="a-opt-sub">dupes · F-P6</span></div>
          <div class="a-row"><span class="a-row-ic">${FM.icon("warn", "sm")}</span><span class="a-row-txt"><span class="a-row-title">${big.length} file(s) over 3 GB</span></span><span class="a-opt-sub">storage · F-P6</span></div>
          <div class="a-row"><span class="a-row-ic">${FM.icon("cancel", "sm")}</span><span class="a-row-txt"><span class="a-row-title">${leftovers.length} leftover partial download(s)</span></span><span class="a-opt-sub">auto-skipped</span></div>
        </div>
      </section>
      <div class="a-quick">
        <button class="a-chipbtn" data-go="search">${FM.icon("search", "sm")} Search</button>
        <button class="a-chipbtn" data-go="tools">${FM.icon("tools", "sm")} Tools</button>
        <button class="a-chipbtn" data-go="devices">${FM.icon("devices", "sm")} Devices</button>
        <button class="a-chipbtn" data-go="help">${FM.icon("info", "sm")} Help</button>
      </div>
      <div class="a-sec">Recent activity</div>
      <div class="a-list" id="recent"></div>
      ${A.demoNote()}
      <button class="a-fab" data-go="organizer" aria-label="Start organizing">${FM.icon("organize")}<span>Organize</span></button>
    </div>`;
  const rec = m.querySelector("#recent");
  FM.store.activity.slice(0, 3).forEach((a) => {
    rec.append(A.row({
      icon: FM.icon("activity"), title: FM.esc(a.text), sub: FM.dt(a.t),
      onTap: () => FM.go("activity"),
    }));
  });
  m.querySelectorAll("[data-go]").forEach((b) => (b.onclick = () => FM.go(b.dataset.go)));
};
FM.screen("dashboard", home);
FM.screen("home", home);

/* ========================================================================= */
/* Files — SAF-scoped browser; picker sheet + filter sheet, no h-scroll        */
/* ========================================================================= */
FM.screen("files", (m) => {
  A.title("Files");
  const st = { scope: "Downloads", q: "", kind: "All", sort: "name" };
  const BAD_CHARS = /[<>:"/\\|?*\[\]]/; /* Android-safe filename check (mock) */

  const render = () => {
    const scopes = A.scopes();
    const root = scopes.find(([n]) => n === st.scope)[1];
    const kinds = ["All", ...new Set(FM.store.files.filter((f) => f.sourceUri.startsWith(root)).map((f) => FM.engine.kindOf(f.extension)))];
    const res = A.analyzed(root);
    const byUri = Object.fromEntries(res.map((a) => [a.candidate.sourceUri, a]));
    let list = FM.store.files.filter((f) => f.sourceUri.startsWith(root));
    if (st.kind !== "All") list = list.filter((f) => FM.engine.kindOf(f.extension) === st.kind);
    if (st.q) list = list.filter((f) => f.displayName.toLowerCase().includes(st.q.toLowerCase()));
    const s = st.sort;
    list = [...list].sort((a, b) =>
      s === "size" ? b.sizeBytes - a.sizeBytes
      : s === "modified" ? Date.parse(b.modifiedAt) - Date.parse(a.modifiedAt)
      : s === "kind" ? FM.engine.kindOf(a.extension).localeCompare(FM.engine.kindOf(b.extension))
      : a.displayName.localeCompare(b.displayName));
    const activeFilters = (st.kind !== "All") + (st.sort !== "name");

    m.innerHTML = `
      <div class="a-page">
        <div class="a-searchbar">${FM.icon("search", "sm")}<input id="fq" type="search" placeholder="Search in ${st.scope}" value="${FM.esc(st.q)}" aria-label="Search in ${st.scope}"></div>
        <div class="a-ctl">
          <button class="a-pick grow" id="scope-pick" aria-label="Choose folder">${FM.icon("folder", "sm")}<span class="a-pick-txt">${st.scope}</span>${FM.icon("chevR", "sm")}</button>
          <button class="a-filterbtn" id="rescan" aria-label="Rescan folder">${FM.icon("retry", "sm")}</button>
          <span id="fbtn"></span>
        </div>
        <div class="a-ctl-sub">${list.length} item(s) · sorted by ${st.sort}${st.kind !== "All" ? ` · ${st.kind}` : ""}</div>
        <div class="a-list" id="flist"></div>
        ${A.demoNote()}
        <button class="a-fab" data-go="organizer" aria-label="Organize this folder">${FM.icon("organize")}<span>Organize</span></button>
      </div>`;
    m.querySelector("#scope-pick").onclick = () => A.picker({
      title: "Choose a folder",
      items: scopes.map(([n, r]) => ({
        value: n, label: n,
        sub: `${A.saf(r)} · ${FM.store.files.filter((f) => f.sourceUri.startsWith(r)).length} file(s)`,
        right: FM.statusChip(A.grants.includes(n) ? "Connected" : "Ask"),
      })),
      selected: st.scope,
      onPick: (v) => { st.scope = v; st.kind = "All"; A.sel.exit(); render(); },
    });
    m.querySelector("#rescan").onclick = () => {
      A._an = null; /* drop cached analysis — mirrors a rescan verb */
      FM.toast(`Rescanning ${st.scope}…`, "info");
      render();
    };
    m.querySelector("#fbtn").append(A.filterBtn(activeFilters, () => {
      const fstate = { kind: st.kind, sort: st.sort };
      A.filterSheet({
        groups: [
          { title: "Kind", key: "kind", options: kinds.map((k) => ({ value: k, label: k })) },
          { title: "Sort by", key: "sort", options: [["name", "Name"], ["kind", "Kind"], ["size", "Size"], ["modified", "Recently modified"]].map(([v, l]) => ({ value: v, label: l })) },
        ],
        state: fstate,
        onChange: () => { st.kind = fstate.kind; st.sort = fstate.sort; render(); },
        onClear: () => { fstate.kind = "All"; fstate.sort = "name"; st.kind = "All"; st.sort = "name"; render(); },
      });
    }));
    m.querySelector("[data-go]").onclick = () => FM.go("organizer");
    const inp = m.querySelector("#fq");
    inp.oninput = () => {
      st.q = inp.value;
      clearTimeout(inp._t);
      /* re-render rebuilds the DOM — restore focus + caret on the new input */
      inp._t = setTimeout(() => {
        render();
        const nq = m.querySelector("#fq");
        nq.focus(); nq.setSelectionRange(nq.value.length, nq.value.length);
      }, 160);
    };
    inp.onkeydown = (e) => e.stopPropagation();

    const fl = m.querySelector("#flist");
    if (!list.length) fl.innerHTML = FM.empty("files", "Nothing here", "No files match this scope and filter in the mock data.");
    list.forEach((f) => {
      const id = f.scanId;
      fl.append(A.fileRow(f, {
        selecting: A.sel.active,
        selected: A.sel.items.has(id),
        onToggle: () => {
          if (A.sel.active) { A.sel.toggle(id); }
          else { A.sel.enter(render); A.sel.items.add(id); A.sel.sync(); render(); }
        },
        onOpen: () => {
          const a = byUri[f.sourceUri];
          const excluded = FM.engine.excluded(f);
          A.sheet({
            title: f.displayName,
            body: `<div class="a-detail">
                <div><span>Location</span><b>${FM.esc(A.saf(f.sourceUri))}</b></div>
                <div><span>Size</span><b>${FM.bytes(f.sizeBytes)}</b></div>
                <div><span>Kind</span><b>${FM.esc(FM.engine.kindOf(f.extension))}${f.mimeType ? ` · ${FM.esc(f.mimeType)}` : ""}</b></div>
                <div><span>Modified</span><b>${FM.dt(f.modifiedAt)}</b></div>
                ${excluded ? `<div><span>Status</span><b>${FM.statusChip("Offline")} excluded</b></div>` : ""}
              </div>${excluded ? "" : A.whyBlock(a)}`,
            actions: [
              { label: "Organize this file", icon: "organize", kind: "primary", run: () => FM.go("organizer") },
              { label: "Rename", icon: "rename", run: () => renameSheet(f) },
              { label: "Select", icon: "check", run: () => { A.sel.enter(render); A.sel.items.add(id); A.sel.sync(); render(); } },
            ],
          });
        },
      }));
    });
  };

  const renameSheet = (f) => {
    const sh = A.sheet({
      title: "Rename",
      body: `<div class="field"><label>New name</label>
        <input class="input" id="rn" value="${FM.esc(f.displayName)}">
        <div class="err" id="rn-err" hidden></div>
        <div class="hint">Validation is specific, like the real app — illegal characters are called out by name.</div></div>`,
    });
    const inp = sh.el.querySelector("#rn"), err = sh.el.querySelector("#rn-err");
    const box = sh.el.querySelector(".a-sheet-actions");
    const ok = document.createElement("button");
    ok.className = "btn primary"; ok.textContent = "Rename";
    ok.onclick = () => {
      const v = inp.value.trim();
      const bad = v.match(BAD_CHARS);
      if (!v) { err.hidden = false; err.textContent = "The name can't be empty."; return; }
      if (bad) { err.hidden = false; err.textContent = `That name contains '${bad[0]}', which isn't allowed in a file name. Choose a different name.`; return; }
      sh.close(); FM.toast(`Demo — renamed to '${v}' (not persisted)`, "success");
    };
    box.append(ok);
    setTimeout(() => inp.focus(), 60);
  };
  A._renameSheet = renameSheet; /* selection sheet reuses it */
  render();
});

/* ---------- selection actions (contextual bar "more") ---------- */
document.getElementById("sel-sheet")?.addEventListener("click", () => {
  const n = A.sel.items.size;
  const actions = [
    { label: "Organize selected", icon: "organize", kind: "primary", run: () => { A.sel.exit(); FM.go("organizer"); } },
  ];
  if (n === 1) actions.push({
    label: "Rename", icon: "rename", run: () => {
      const id = [...A.sel.items][0];
      const f = FM.store.files.find((x) => x.scanId === id);
      A.sel.exit(); if (f) A._renameSheet?.(f);
    },
  });
  actions.push(
    { label: "Move / copy to…", icon: "move", run: () => { FM.toast("Demo — destination picker is not wired in the mockup", "info"); A.sel.exit(); } },
    { label: "Send to Smart Inbox", icon: "warn", run: () => { FM.toast(`${n} file(s) queued for review (mock)`, "success"); A.sel.exit(); } },
    { label: "Delete", icon: "trash", run: () => FM.toast("No delete operation exists in the core — by design", "info") },
    { label: "Clear selection", icon: "close", run: () => A.sel.exit() },
  );
  A.sheet({ title: `${n} file(s) selected`, actions });
});
document.getElementById("sel-close")?.addEventListener("click", () => A.sel.exit());

/* ========================================================================= */
/* Search — full-width field + filter sheet (kind / size / scope)              */
/* ========================================================================= */
FM.screen("search", (m) => {
  A.title("Search");
  const st = { q: "", kind: "All", size: "any", scope: "all" };
  const sizes = { any: [0, 1e20], small: [0, 1e6], medium: [1e6, 100e6], large: [100e6, 1e20] };

  const render = () => {
    let list = FM.store.files;
    if (st.scope !== "all") {
      const root = A.scopes().find(([n]) => n === st.scope)?.[1];
      if (root) list = list.filter((f) => f.sourceUri.startsWith(root));
    }
    if (st.kind !== "All") list = list.filter((f) => FM.engine.kindOf(f.extension) === st.kind);
    const [lo, hi] = sizes[st.size];
    list = list.filter((f) => f.sizeBytes >= lo && f.sizeBytes < hi);
    if (st.q) {
      const s = st.q.toLowerCase();
      list = list.filter((f) => f.displayName.toLowerCase().includes(s) || f.extension.includes(s));
    }
    const kinds = ["All", ...new Set(FM.store.files.map((f) => FM.engine.kindOf(f.extension)))];
    const activeFilters = (st.kind !== "All") + (st.size !== "any") + (st.scope !== "all");

    m.innerHTML = `
      <div class="a-page">
        <div class="a-searchbar lg">${FM.icon("search")}<input id="sq" type="search" placeholder="Search files" value="${FM.esc(st.q)}" aria-label="Search files"></div>
        <div class="a-ctl"><span id="sbtn"></span></div>
        <div class="a-sec">${st.q || activeFilters ? `${list.length} result(s)` : "All indexed files"}</div>
        <div class="a-list" id="slist"></div>
        <div class="a-note">${FM.icon("info", "sm")}<span>Mock search filters in memory. The real app queries the file index; full-text content search is Planned on Android.</span></div>
      </div>`;
    m.querySelector("#sbtn").append(A.filterBtn(activeFilters, () => {
      const fstate = { kind: st.kind, size: st.size, scope: st.scope };
      A.filterSheet({
        groups: [
          { title: "Folder", key: "scope", options: [{ value: "all", label: "All folders" }, ...A.scopes().map(([n]) => ({ value: n, label: n }))] },
          { title: "Kind", key: "kind", options: kinds.map((k) => ({ value: k, label: k })) },
          { title: "Size", key: "size", options: [["any", "Any size"], ["small", "Under 1 MB"], ["medium", "1–100 MB"], ["large", "Over 100 MB"]].map(([v, l]) => ({ value: v, label: l })) },
        ],
        state: fstate,
        onChange: () => { st.kind = fstate.kind; st.size = fstate.size; st.scope = fstate.scope; render(); },
        onClear: () => { Object.assign(fstate, { kind: "All", size: "any", scope: "all" }); Object.assign(st, fstate); render(); },
      });
    }));
    const inp = m.querySelector("#sq");
    inp.oninput = () => {
      st.q = inp.value;
      clearTimeout(inp._t);
      inp._t = setTimeout(() => {
        render();
        const nq = m.querySelector("#sq");
        nq.focus(); nq.setSelectionRange(nq.value.length, nq.value.length);
      }, 160);
    };
    const sl = m.querySelector("#slist");
    list.slice(0, 40).forEach((f) =>
      sl.append(A.fileRow(f, {
        onOpen: () => {
          const a = A.analyzed(f.sourceUri.slice(0, f.sourceUri.lastIndexOf("\\")))
            .find((x) => x.candidate.sourceUri === f.sourceUri);
          A.sheet({
            title: f.displayName,
            body: `<div class="a-detail">
                <div><span>Location</span><b>${FM.esc(A.saf(f.sourceUri))}</b></div>
                <div><span>Size</span><b>${FM.bytes(f.sizeBytes)}</b></div>
                <div><span>Modified</span><b>${FM.dt(f.modifiedAt)}</b></div>
              </div>${A.whyBlock(a)}`,
            actions: [{ label: "Open in Files", icon: "files", run: () => FM.go("files") }],
          });
        },
      })));
    if (!list.length) sl.innerHTML = FM.empty("search", "No matches", "Try a different name, extension, or filter.");
  };
  render();
});

/* ========================================================================= */
/* Organizer — permission explainer → scope → analyze → plan → safety → run    */
/* ========================================================================= */
FM.screen("organizer", (m) => {
  A.title("Organizer");
  const S = { step: "perm", scope: null, scopeName: "", analyzed: null, plan: null, safety: null, done: null, dry: true, tree: false, abort: null };
  const trees = A.scopes();
  const STEPS = ["Access", "Folder", "Analyze", "Plan", "Run"];
  const stepIdx = { perm: 0, scope: 1, plan: 3, run: 4, result: 4 };

  const steps = (cur) => {
    const idx = stepIdx[cur] ?? 2;
    const pct = Math.round(((idx + 1) / STEPS.length) * 100);
    return `<div class="a-steps">
      <div class="a-step-label">Step ${idx + 1} of ${STEPS.length} — ${STEPS[idx]}</div>
      <div class="a-progress"><i style="width:${pct}%"></i></div>
    </div>`;
  };

  const rebuild = () => {
    const root = S.scope;
    S.plan = FM.engine.plan(S.analyzed, S.dry, root + "\\organized", root, S.tree);
    S.safety = FM.engine.safety(S.analyzed, S.plan, root);
  };

  const render = () => {
    if (S.step === "perm") {
      /* permission explanation BEFORE any access request (ANDROID_UX spec) */
      m.innerHTML = `
        <div class="a-page">
          ${steps("perm")}
          <section class="a-card">
            <div class="a-card-head">${FM.icon("shield")}<h2>Folder access</h2></div>
            <div class="a-card-sub">
              FileMind organizes only the folders you pick. Android asks for access per folder
              (Storage Access Framework) — nothing leaves the device, and scanning never moves anything.
            </div>
            <div class="a-list tight">
              <div class="a-row"><span class="a-row-ic">${FM.icon("check")}</span><span class="a-row-txt"><span class="a-row-title">You choose each folder</span><span class="a-row-sub">Access is per-folder and revocable in Settings</span></span></div>
              <div class="a-row"><span class="a-row-ic">${FM.icon("lock")}</span><span class="a-row-txt"><span class="a-row-title">On-device only</span><span class="a-row-sub">Names and metadata never leave the phone</span></span></div>
              <div class="a-row"><span class="a-row-ic">${FM.icon("undo")}</span><span class="a-row-txt"><span class="a-row-title">Every run is undoable</span><span class="a-row-sub">Moves are recorded with an undo recipe</span></span></div>
            </div>
          </section>
          <div class="a-cta">
            <button class="btn primary lg" id="grant">Choose a folder to grant</button>
            <button class="btn ghost" id="skip">Not now</button>
          </div>
          ${A.demoNote()}
        </div>`;
      m.querySelector("#grant").onclick = () => { S.step = "scope"; render(); };
      m.querySelector("#skip").onclick = () => FM.go("dashboard");
    } else if (S.step === "scope") {
      m.innerHTML = `
        <div class="a-page">
          ${steps("scope")}
          <div class="a-sec">Pick a folder to organize</div>
          <div class="a-list" id="trees"></div>
          ${A.demoNote()}
        </div>`;
      const t = m.querySelector("#trees");
      trees.forEach(([name, root]) => {
        const granted = A.grants.includes(name);
        const count = FM.store.files.filter((f) => f.sourceUri.startsWith(root)).length;
        t.append(A.row({
          icon: FM.icon("folder"),
          title: name,
          sub: `${A.saf(root)} · ${count} file(s) · ${granted ? "Access granted" : "Will prompt for access"}`,
          right: FM.statusChip(granted ? "Connected" : "Ask"),
          onTap: () => {
            S.scope = root; S.scopeName = name;
            if (!granted) { A.grants.push(name); FM.toast(`Access granted to ${name} (mock SAF grant)`, "success"); }
            const cands = FM.store.files.filter((f) => f.sourceUri.startsWith(root));
            S.analyzed = FM.engine.analyze(cands);
            rebuild();
            S.step = "plan"; render();
          },
        }));
      });
    } else if (S.step === "plan") {
      const p = S.plan, sf = S.safety;
      const moves = p.items.filter((i) => i.operation === "Move");
      const asks = p.items.filter((i) => i.operation === "Ask");
      m.innerHTML = `
        <div class="a-page">
          ${steps("plan")}
          <section class="a-card">
            <div class="a-card-head">${FM.icon("organize")}<h2>Plan — ${FM.esc(S.scopeName)}</h2></div>
            <div class="a-card-sub">${p.items.length} planned action(s), ${moves.length} moving${asks.length ? `, ${asks.length} to Smart Inbox` : ""} · ${p.skipped.length} skipped${p.dryRun ? " · dry-run preview" : ""}</div>
          </section>
          <div class="a-card" id="opts">
            <div id="opt-dry"></div>
            <div id="opt-tree"></div>
          </div>
          ${sf.allowed
            ? sf.warnings.length
              ? `<div class="a-card warnline">${sf.warnings.map((w) => `<div>${FM.icon("warn", "sm")} ${FM.esc(w)}</div>`).join("")}</div>`
              : `<div class="a-card okline">${FM.icon("check", "sm")} Safety check passed — protected paths untouched, every move has an undo recipe.</div>`
            : `<div class="a-card errline">${sf.blockers.map((b) => `<div>${FM.icon("error", "sm")} ${FM.esc(b)}</div>`).join("")}</div>`}
          <div class="a-sec">Planned moves</div>
          <div class="a-list" id="plist"></div>
          <div class="a-cta">
            <button class="btn primary lg" id="exec" ${sf.allowed ? "" : "disabled"}>${S.dry ? "Run preview" : "Run this plan"}</button>
            <button class="btn ghost" id="back">Pick another folder</button>
          </div>
          ${A.demoNote()}
        </div>`;
      m.querySelector("#opt-dry").append(A.switchRow({
        icon: FM.icon("play"), title: "Preview only", sub: "Dry run — the full pipeline runs, nothing moves",
        checked: S.dry, onChange: (v) => { S.dry = v; rebuild(); render(); },
      }));
      m.querySelector("#opt-tree").append(A.switchRow({
        icon: FM.icon("folder"), title: "Keep folder structure", sub: "Preserve relative subfolders under the destination",
        checked: S.tree, onChange: (v) => { S.tree = v; rebuild(); render(); },
      }));
      const pl = m.querySelector("#plist");
      p.items.slice(0, 25).forEach((i) => {
        pl.append(A.row({
          icon: FM.icon(i.operation === "Ask" ? "warn" : "move"),
          title: FM.esc(i.source.split("\\").pop()),
          sub: i.operation === "Ask" ? FM.esc(i.reason) : `→ ${FM.esc(A.saf(i.destination))}`,
          right: i.operation === "Ask" ? FM.statusChip("Review") : FM.confBar(i._a?.classification.confidence ?? 0),
        }));
      });
      if (!p.items.length) pl.innerHTML = FM.empty("organize", "Nothing to move", "Every file classified as NoOp — the mock classifier leaves low-confidence files in place.");
      m.querySelector("#exec").onclick = async () => { S.step = "run"; await runExec(); };
      m.querySelector("#back").onclick = () => { S.step = "scope"; render(); };
    } else if (S.step === "result") {
      const done = S.done;
      m.innerHTML = `
        <div class="a-page">
          ${steps("result")}
          <section class="a-card">
            <div class="a-card-head">${FM.icon("check")}<h2>${FM.esc(done.status)}</h2>${FM.statusChip(done.status)}</div>
            <div class="a-card-sub">${done.items.filter((i) => i.status === "Committed").length} item(s) moved · recorded in Activity with an undo recipe</div>
          </section>
          <div class="a-cta">
            <button class="btn lg" id="undo">${FM.icon("undo", "sm")} Undo this run</button>
            <button class="btn primary lg" id="more">Done</button>
          </div>
          ${A.demoNote()}
        </div>`;
      m.querySelector("#undo").onclick = async () => {
        const okc = await FM.confirm({
          title: "Undo this operation?",
          body: `${done.items.length} moved file(s) will go back to their original locations. Undo is fingerprint-checked — a file that changed since is flagged NeedsReview instead of silently moved.`,
          ok: "Undo operation",
        });
        if (!okc) return;
        const undoRec = {
          operationId: FM.uid(32), planId: done.planId, status: "RolledBack",
          createdAt: new Date().toISOString(), committedAt: new Date().toISOString(),
          undoOfOperationId: done.operationId, error: null,
          items: done.items.filter((i) => i.status === "Committed").map((i) => ({
            itemId: FM.uid(8), operation: "Move", originalLocation: i.resultingLocation,
            resultingLocation: i.originalLocation, status: "Committed", verification: "verified", error: null,
          })),
        };
        FM.store.ops.unshift(undoRec);
        FM.store.activity.unshift({ t: undoRec.createdAt, text: `Undo ${done.operationId.slice(0, 8)} — ${undoRec.items.length} file(s) restored`, opId: undoRec.operationId });
        FM.bus.emit("ops");
        FM.toast("Operation undone — files restored", "success");
        FM.go("activity");
      };
      m.querySelector("#more").onclick = () => FM.go("activity");
    }
  };

  const runExec = async () => {
    if (S.dry) {
      /* dry run: pipeline ran end-to-end, nothing moved — mirrors flows.js */
      m.innerHTML = `
        <div class="a-page">
          ${steps("run")}
          <div class="a-card okline">${FM.icon("check", "sm")} <strong>Dry run complete.</strong> The full pipeline ran; nothing was moved. Output matches <span class="mono">execute --dry-run</span>.</div>
          <div class="a-list tight">${S.plan.items.slice(0, 12).map((i) =>
            `<div class="a-row"><span class="a-row-ic">${FM.icon(i.operation === "Ask" ? "warn" : "move", "sm")}</span>
             <span class="a-row-txt"><span class="a-row-title">${FM.esc(i.source.split("\\").pop())}</span>
             <span class="a-row-sub">${i.destination ? "→ " + FM.esc(A.saf(i.destination)) : FM.esc(i.reason)}</span></span></div>`).join("")}</div>
          <div class="a-cta">
            <button class="btn primary lg" id="real">${FM.icon("play", "sm")} Run for real</button>
            <button class="btn ghost" id="again">Start over</button>
          </div>
          ${A.demoNote()}
        </div>`;
      m.querySelector("#real").onclick = () => { S.dry = false; rebuild(); S.step = "plan"; render(); };
      m.querySelector("#again").onclick = () => { Object.assign(S, { step: "perm", scope: null, analyzed: null, plan: null, safety: null, done: null }); render(); };
      return;
    }
    S.abort = new AbortController();
    m.innerHTML = `
      <div class="a-page">
        ${steps("run")}
        <section class="a-card">
          <div class="a-card-head">${FM.icon("activity")}<h2>Running…</h2><span class="grow"></span><span id="o-stage">${FM.statusChip("Validating")}</span></div>
          <div class="a-progress" style="margin:var(--sp-2) 0"><i id="o-pct" style="width:0%"></i></div>
          <div class="a-card-sub" id="o-count"></div>
          <div class="a-list tight" id="oplog"></div>
        </section>
        <div class="a-cta"><button class="btn" id="o-cancel">${FM.icon("cancel", "sm")} Cancel</button></div>
      </div>`;
    const log = m.querySelector("#oplog");
    const moves = S.plan.items.filter((i) => i.operation === "Move");
    FM.store.activeOp = { label: `Organize ${S.scopeName}`, status: "Validating", pct: 0, done: 0, total: moves.length, current: "" };
    FM.bus.emit("ops");
    m.querySelector("#o-cancel").onclick = () => S.abort.abort();
    const addStep = (icon, text) => {
      const d = document.createElement("div");
      d.className = "a-opstep";
      d.innerHTML = `${icon} ${FM.esc(text)}`;
      log.append(d);
      log.scrollTop = log.scrollHeight;
    };
    try {
      const record = await FM.engine.execute(S.plan, (stage, rec, it) => {
        m.querySelector("#o-stage").innerHTML = FM.statusChip(rec.status === "Committed" ? "Verifying" : rec.status);
        const done = rec.items.filter((i) => i.status === "Committed").length;
        m.querySelector("#o-pct").style.width = Math.round((done / Math.max(1, rec.items.length)) * 100) + "%";
        m.querySelector("#o-count").textContent = `${done}/${rec.items.length} item(s)`;
        if (it) {
          addStep(it.status === "Committed" ? FM.icon("check", "sm") : it.status === "Failed" ? FM.icon("error", "sm") : FM.icon("clock", "sm"),
            `${it.status === "Executing" ? "Moving " : ""}${it.originalLocation.split("\\").pop()}${it.status === "Committed" ? " — moved" : it.status === "Failed" ? " — failed" : ""}`);
          FM.store.activeOp.done = done;
          FM.store.activeOp.pct = Math.round((done / Math.max(1, rec.items.length)) * 100);
          FM.store.activeOp.current = it.originalLocation.split("\\").pop();
          FM.bus.emit("ops");
        }
      }, S.abort.signal);
      FM.store.activeOp = null; FM.bus.emit("ops");
      FM.store.ops.unshift(record);
      FM.store.activity.unshift({ t: record.committedAt, text: `Organize ${S.scopeName} — ${record.items.length} file(s) moved`, opId: record.operationId });
      S.done = record;
      S.step = "result"; render();
    } catch (e) {
      FM.store.activeOp = null; FM.bus.emit("ops");
      if (e.name === "AbortError") {
        m.innerHTML = `<div class="a-page">
          ${steps("run")}
          <div class="a-card warnline">${FM.icon("cancel", "sm")} <strong>Cancelled.</strong> The operation was aborted — mirrors RollingBack handling for partial batches.</div>
          <div class="a-cta"><button class="btn primary lg" id="again">Start over</button></div>
        </div>`;
        m.querySelector("#again").onclick = () => { Object.assign(S, { step: "perm", scope: null, analyzed: null, plan: null, safety: null, done: null }); render(); };
      } else {
        S.step = "scope"; render();
        FM.toast(`Run failed: ${e.message}`, "error");
      }
    }
  };
  render();
});

/* ========================================================================= */
/* Activity — operations + Smart Inbox + Task Center                           */
/* ========================================================================= */
FM.screen("activity", (m) => {
  A.title("Activity");
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  const done = (o) => o.items.filter((i) => i.status === "Committed").length;
  const failed = (o) => o.items.filter((i) => i.status === "Failed").length;
  const canUndo = (o) => o.status === "Committed" && !o.undoOfOperationId && !FM.store.ops.some((x) => x.undoOfOperationId === o.operationId);
  m.innerHTML = `
    <div class="a-page">
      ${A.activeOpCard()}
      <div class="a-sec">Smart Inbox ${pending.length ? `· ${pending.length} pending` : ""}</div>
      <div class="a-list" id="inbox"></div>
      <div class="a-sec">Operations</div>
      <div class="a-list" id="ops"></div>
      <div class="a-sec">Operation states</div>
      <div class="a-legend">${["Planned", "Validating", "Executing", "Verifying", "Committed", "Failed", "RollingBack", "RolledBack", "NeedsAttention"].map(FM.statusChip).join(" ")}</div>
      <div class="a-note">${FM.icon("info", "sm")}<span>Every state pairs icon + label + color — never color alone.</span></div>
      ${A.demoNote()}
    </div>`;
  const ib = m.querySelector("#inbox");
  if (!pending.length) ib.innerHTML = FM.empty("check", "Inbox is clear", "Files the classifier can't place confidently land here for your call.");
  pending.forEach((i) => {
    ib.append(A.row({
      icon: FM.icon("warn"), title: FM.esc(i.fileName),
      sub: `${FM.esc(i.summary)} · suggested: ${FM.esc(i.suggestedDest)}`,
      right: FM.icon("chevR", "sm"),
      onTap: () => A.sheet({
        title: i.fileName,
        body: `<div class="a-card-sub">${FM.esc(i.reasons.join(" "))}</div><div class="a-detail"><div><span>Suggested</span><b>${FM.esc(i.suggestedDest)}</b></div><div><span>Source</span><b>${FM.esc(A.saf(i.sourceUri))}</b></div></div>`,
        actions: [
          { label: `Move to ${i.suggestedDest}`, icon: "check", kind: "primary", run: () => { i.status = "Accepted"; FM.bus.emit("ops"); FM.toast("Accepted — move queued (mock)", "success"); FM.route(); } },
          { label: "Leave in place", icon: "cancel", run: () => { i.status = "Dismissed"; FM.bus.emit("ops"); FM.toast("Dismissed", "info"); FM.route(); } },
        ],
      }),
    }));
  });
  const ops = m.querySelector("#ops");
  [...FM.store.ops].reverse().forEach((o) => {
    ops.append(A.row({
      icon: FM.icon("move"), title: `Organize · ${o.items.length} item(s)`,
      sub: `${FM.dt(o.createdAt)} · ${done(o)} moved${failed(o) ? ` · ${failed(o)} failed` : ""}`,
      right: FM.statusChip(o.status),
      onTap: () => {
        const actions = [];
        if (canUndo(o)) actions.push({
          label: "Undo operation", icon: "undo", kind: "primary", run: async () => {
            const okc = await FM.confirm({
              title: "Undo this operation?",
              body: `${done(o)} moved file(s) will go back to their original locations. Undo is fingerprint-checked; a file that changed since gets flagged NeedsReview instead of silently moved.`,
              ok: "Undo operation",
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
            FM.route();
          },
        });
        if (o.status === "NeedsAttention") actions.push({ label: "Retry failed", icon: "retry", run: () => FM.toast("Demo — retry would re-run only the failed item", "info") });
        A.sheet({
          title: "Operation detail",
          body: `<div class="a-list tight">${o.items.slice(0, 12).map((i) =>
            `<div class="a-row"><span class="a-row-ic">${FM.icon(i.status === "Failed" ? "error" : "check", "sm")}</span>
             <span class="a-row-txt"><span class="a-row-title">${FM.esc(i.originalLocation.split("\\").pop())}</span>
             <span class="a-row-sub">${i.status}${i.error ? " · " + FM.esc(i.error.message) : i.resultingLocation ? " → " + FM.esc(A.saf(i.resultingLocation)) : ""}</span></span></div>`).join("")}</div>`,
          actions,
        });
      },
    }));
  });
});

/* ========================================================================= */
/* Devices — truthful platform capability surface                              */
/* ========================================================================= */
FM.screen("devices", (m) => {
  A.title("Devices");
  m.innerHTML = `
    <div class="a-page">
      ${A.storageCard()}
      <div class="a-sec">Connected &amp; nearby</div>
      <div class="a-list" id="devs"></div>
      <div class="a-note">${FM.icon("info", "sm")}<span>Capability rows are truthful — features marked Planned are not pretending to work.</span></div>
    </div>`;
  const list = m.querySelector("#devs");
  FM.store.devices.forEach((d) => {
    const isSelf = d.kind === "android"; /* in the Android shell the phone is the host */
    const pct = Math.round((d.storage.used / d.storage.total) * 100);
    list.append(A.row({
      icon: FM.icon(d.icon), title: isSelf ? "This device" : FM.esc(d.name),
      sub: `${FM.bytes(d.storage.used)} / ${FM.bytes(d.storage.total)} · ${d.capabilities.slice(0, 2).join(", ")}`,
      right: FM.statusChip(isSelf || d.status === "connected" ? "Connected" : "Missing"),
      onTap: () => A.sheet({
        title: isSelf ? "This device" : d.name,
        body: `${FM.meter(pct, isSelf ? "This device" : d.name)}<div class="a-detail">
          <div><span>Capabilities</span><b>${d.capabilities.map(FM.esc).join(" · ")}</b></div>
          ${d.restrictions.length ? `<div><span>Limits</span><b>${d.restrictions.map(FM.esc).join(" · ")}</b></div>` : ""}
          ${isSelf ? `<div><span>Folder access</span><b>${A.grants.length} of ${A.scopes().length} trees granted (SAF)</b></div>` : ""}
          ${!isSelf && d.reconnect ? `<div><span>To connect</span><b>${FM.esc(d.reconnect)}</b></div>` : ""}</div>`,
        actions: isSelf || d.connected ? [] : [{ label: "Refresh", icon: "retry", kind: "primary", run: () => FM.toast("Scanning for devices…", "info") }],
      }),
    }));
  });
});

/* ========================================================================= */
/* Tools — truthful availability + organizer rules + storage demo              */
/* ========================================================================= */
FM.screen("tools", (m) => {
  A.title("Tools");
  const humanCond = (g) => [
    ...(g.conditions || []).map((c) => `${c.field} ${c.operator} '${c.value}'`),
    ...(g.groups || []).map(humanCond),
  ].join(` ${g.operator} `);
  const humanAct = (a) =>
    a.kind === "RouteToCollection" ? `route to collection '${a.collection}'`
    : a.kind === "Move" ? `move to ${a.destinationTemplate}`
    : a.kind === "Rename" ? `rename to ${a.namingTemplate}`
    : a.kind.toLowerCase();
  const tools = [
    ["organize", "Organize a folder", "Scan → classify → plan → safety-check → move files, with a dry-run preview first.", "Connected", () => FM.go("organizer")],
    ["activity", "Smart Inbox", `${FM.store.inbox.filter((i) => i.status === "Pending").length} file(s) waiting for a human decision.`, "Review", () => FM.go("activity")],
    ["db", "Analyze storage", "Where the space went, by file kind — demo below.", "Connected", () => m.querySelector("#storage-demo")?.scrollIntoView({ behavior: "smooth", block: "nearest" })],
    ["copy", "Duplicates", "Same-hash and similar-name groups with keep/delete suggestions.", "Connected", null],
    ["link", "Related files", "Version chains and same-stem siblings (e.g. notes.txt ↔ notes (2).txt).", "Connected", null],
    ["undo", "Undo history", "Every operation carries a reversible recipe.", "Connected", () => FM.go("activity")],
    ["activity", "Watch folders", "Background organize on change. WorkManager job — planned.", "Review", null],
    ["ai", "AI suggestions (Groq)", "Optional, off by default. Quota + privacy gate — sends file names only when enabled.", "Missing", null],
  ];
  m.innerHTML = `
    <div class="a-page">
      <div class="a-list" id="tl"></div>
      <div class="a-sec">Organizer rules</div>
      <div class="a-card-sub" style="padding:0 var(--sp-1)">Mirrors the rule engine — priority → specificity → stop-processing. Toggling a rule changes the next analysis.</div>
      <div class="a-list" id="rl"></div>
      <div class="a-sec">Storage analysis</div>
      <section class="a-card" id="storage-demo">
        <div class="a-card-head">${FM.icon("db")}<h2>Where the space went</h2></div>
        <div class="a-list tight" id="sKinds"></div>
        <div class="a-card-sub">Demo snapshot — the full pipeline runs in the host (<span class="mono">storage</span> verb, F-P6).</div>
      </section>
      ${A.demoNote()}
    </div>`;
  const tl = m.querySelector("#tl");
  tools.forEach(([ic, t, sub, st, fn]) =>
    tl.append(A.row({
      icon: FM.icon(ic), title: FM.esc(t), sub: FM.esc(sub), right: FM.statusChip(st),
      onTap: fn || (() => A.sheet({ title: t, body: `<div class="a-card-sub">${FM.esc(sub)}</div>` })),
    })));
  const rl = m.querySelector("#rl");
  FM.store.rules.forEach((r) => {
    const row = A.switchRow({
      icon: FM.icon("organize"),
      title: r.name,
      sub: `when ${humanCond(r.conditionRoot)} → ${r.actions.map(humanAct).join(", ")} · ${r.provenance} · priority ${r.priority}${r.stopProcessing ? " · stop" : ""}`,
      checked: r.enabled,
      onChange: (v) => { r.enabled = v; FM.toast(`Rule '${r.name}' ${v ? "enabled" : "disabled"}`, "info"); },
    });
    row.classList.add("a-rule");
    rl.append(row);
  });
  const sk = m.querySelector("#sKinds");
  FM_DATA.storage.byKind.forEach(([k, v]) => {
    const pct = Math.round((v / FM_DATA.storage.used) * 100);
    sk.insertAdjacentHTML("beforeend", `<div class="a-opstep"><span style="width:84px">${FM.esc(k)}</span><div class="a-progress" style="flex:1"><i style="width:${pct}%"></i></div><span class="a-opt-sub" style="width:64px;text-align:right">${FM.bytes(v)}</span></div>`);
  });
});

/* ========================================================================= */
/* Settings — real theme/mode controls + permission explainers + parity groups */
/* ========================================================================= */
FM.screen("settings", (m) => {
  A.title("Settings");
  let s = FM.store.settings; /* re-read after demo reset */

  const render = () => {
    m.innerHTML = `
      <div class="a-page">
        <div class="a-sec">General</div>
        <div class="a-list" id="gen"></div>
        <div class="a-sec">Appearance</div>
        <section class="a-card"><div class="a-seg" id="theme-seg">
          ${["system", "light", "dark"].map((t) => `<button class="a-seg-btn${FM.theme.get() === t ? " on" : ""}" data-t="${t}">${FM.icon(t === "dark" ? "moon" : t === "light" ? "sun" : "devices", "sm")}${t[0].toUpperCase() + t.slice(1)}</button>`).join("")}
        </div></section>
        <div class="a-sec">Organization</div>
        <div class="a-list" id="org"></div>
        <div class="a-sec">Notifications</div>
        <div class="a-list" id="notif"></div>
        <div class="a-sec">Privacy &amp; access</div>
        <div class="a-list" id="priv"></div>
        <div class="a-sec">Advanced</div>
        <div class="a-list" id="adv"></div>
        <div class="a-sec">About</div>
        <div class="a-list" id="about"></div>
        ${A.demoNote()}
      </div>`;
    m.querySelectorAll("#theme-seg [data-t]").forEach((b) => (b.onclick = () => { FM.theme.set(b.dataset.t); m.querySelectorAll("#theme-seg .a-seg-btn").forEach((x) => x.classList.toggle("on", x === b)); }));

    const gen = m.querySelector("#gen");
    gen.append(A.row({
      icon: FM.icon("folder"), title: "Default folder on open",
      sub: `Opens Files at ${s.defaultScope || "Downloads"}`,
      right: FM.icon("chevR", "sm"),
      onTap: () => A.picker({
        title: "Default folder",
        items: A.scopes().map(([n]) => ({ value: n, label: n })),
        selected: s.defaultScope || "Downloads",
        onPick: (v) => { s.defaultScope = v; FM.toast(`Default folder: ${v}`, "success"); render(); },
      }),
    }));

    const org = m.querySelector("#org");
    [["organize", "Organization mode", `Current: ${s.organizationMode} — Smart suggests, you approve`, () => A.picker({
        title: "Organization mode",
        items: [["Manual", "You approve every file"], ["Smart", "Suggestions, you approve the batch"], ["Automatic", "High-confidence moves run without review"]].map(([v, d]) => ({ value: v, label: v, sub: d })),
        selected: s.organizationMode,
        onPick: (mo) => { s.organizationMode = mo; FM.toast(`Mode: ${mo}`, "success"); render(); },
      })],
      ["copy", "Conflict policy", `When a name exists: ${s.conflictPolicy}`, () => A.picker({
        title: "Conflict policy",
        items: FM_DATA.enums.ConflictPolicy.map((c) => ({ value: c, label: c })),
        selected: s.conflictPolicy,
        onPick: (c) => { s.conflictPolicy = c; FM.toast(`Conflict policy: ${c}`, "success"); render(); },
      })],
      ["undo", "Operation history", "Undo recipes kept for every run", () => FM.go("activity")],
    ].forEach(([ic, t, sub, fn]) => org.append(A.row({ icon: FM.icon(ic), title: t, sub, right: FM.icon("chevR", "sm"), onTap: fn })));

    const notif = m.querySelector("#notif");
    [["operationDone", "Operation completed", "When an organize run finishes"],
     ["warnings", "Warnings and failures", "Items that need your attention"],
     ["smartInbox", "Smart Inbox arrivals", "Files waiting for your call"]].forEach(([k, t, sub]) =>
      notif.append(A.switchRow({ icon: FM.icon("warn"), title: t, sub, checked: s.notifications[k], onChange: (v) => { s.notifications[k] = v; FM.toast("Notification preference saved", "success"); } })));

    const priv = m.querySelector("#priv");
    priv.append(A.row({
      icon: FM.icon("shield"), title: "Folder access (SAF)",
      sub: `${A.grants.length} trees granted: ${A.grants.join(", ")}`,
      right: FM.icon("chevR", "sm"),
      onTap: () => {
        const sh = A.sheet({
          title: "Folder access",
          body: `<div class="a-card-sub">Android grants access per folder. FileMind sees only what you pick — revoke any time here or in system Settings.</div><div class="a-list tight" id="gl"></div>`,
        });
        const gl = sh.el.querySelector("#gl");
        const paintGrants = () => {
          gl.innerHTML = "";
          if (!A.grants.length) gl.innerHTML = `<div class="a-note">${FM.icon("info", "sm")}<span>No folders granted — the app can't see any files.</span></div>`;
          A.grants.forEach((g) => {
            gl.append(A.row({
              icon: FM.icon("folder"), title: g,
              sub: A.saf(FM_DATA.roots[g.toLowerCase()] || ""),
              right: `<button class="btn ghost tiny-btn" data-g="${g}">Revoke</button>`,
            }));
          });
          gl.querySelectorAll("[data-g]").forEach((b) => (b.onclick = (e) => {
            e.stopPropagation();
            A.grants = A.grants.filter((x) => x !== b.dataset.g);
            FM.toast(`Access to ${b.dataset.g} revoked (mock)`, "info");
            paintGrants();
          }));
        };
        paintGrants();
        const box = sh.el.querySelector(".a-sheet-actions");
        const more = document.createElement("button");
        more.className = "btn primary"; more.textContent = "Grant another folder";
        more.onclick = () => { sh.close(); FM.toast("Opens the system folder picker (mock)", "info"); };
        box.append(more);
      },
    }));
    priv.append(A.row({ icon: FM.icon("ai"), title: "AI features", sub: "Off by default. Enabling sends file names only — never contents.", right: FM.statusChip("Missing"), onTap: null }));
    priv.append(A.row({ icon: FM.icon("lock"), title: "Local catalog", sub: "Room database at app-private storage — nothing leaves the device", right: FM.icon("chevR", "sm"), onTap: null }));

    const adv = m.querySelector("#adv");
    adv.append(A.row({ icon: FM.icon("db"), title: "Database", sub: "filemind.db (Room) · schema v2", right: "", onTap: null }));
    adv.append(A.row({ icon: FM.icon("open"), title: "Export profile", sub: "Versioned organizer-profile.json — host verb exists; app UI planned", right: FM.statusChip("Missing"), onTap: null }));
    adv.append(A.row({
      icon: FM.icon("warn"), title: "Reset demo data", sub: "Mock operations and inbox return to seeded state",
      right: FM.icon("chevR", "sm"),
      onTap: async () => {
        const ok = await FM.confirm({ title: "Reset demo data?", body: "Mock operations and Smart Inbox items return to their seeded state on next reload. Nothing real is affected.", ok: "Reset demo", kind: "destructive" });
        if (ok) { FM.store = null; FM.initStore(); s = FM.store.settings; A.grants = ["Downloads", "Documents"]; FM.toast("Demo data reset", "success"); render(); }
      },
    }));

    const about = m.querySelector("#about");
    about.append(A.row({ icon: FM.icon("info"), title: "Help & About", sub: "Guides, shortcuts, and app info", right: FM.icon("chevR", "sm"), onTap: () => FM.go("help") }));
    about.append(A.row({ icon: FM.icon("phone"), title: "FileMind for Android", sub: "Mockup · native app ships with Compose (PLAN-021)", right: FM.statusChip("Connected") }));
  };
  render();
});

/* ========================================================================= */
/* Help & About — 9-topic subtree                                              */
/* ========================================================================= */
FM.screen("help", (m) => {
  A.title("Help & About");
  const topics = [
    ["organize", "Getting started", "Grant a folder, preview a plan, approve the moves."],
    ["shield", "Safety & undo", "Protected paths are denied; every run records an undo recipe."],
    ["warn", "Smart Inbox", "Where low-confidence files wait for your call."],
    ["ai", "AI features & privacy", "Optional Groq assist — names only, off by default, quota-capped."],
    ["folder", "Folder access (SAF)", "Per-folder grants, revocation, and scope limits."],
    ["copy", "Duplicates & related files", "Hash groups, version chains, keep/delete suggestions."],
    ["activity", "Activity & operations", "Read statuses, failures, retries, and undo."],
    ["devices", "Devices & sync scope", "What each connected device can do — truthfully."],
    ["phone", "About FileMind", "Version, license, and platform status."],
  ];
  m.innerHTML = `<div class="a-page"><div class="a-list" id="hl"></div>
    <section class="a-card"><div class="a-card-head">${FM.icon("phone")}<h2>FileMind</h2></div>
    <div class="a-card-sub">Android UI mockup — Material 3 Minimal Utility.<br>Native implementation: Jetpack Compose (PLAN-021). Backend: shared Kotlin core.</div></section>
    ${A.demoNote()}</div>`;
  const hl = m.querySelector("#hl");
  topics.forEach(([ic, t, sub]) =>
    hl.append(A.row({
      icon: FM.icon(ic), title: t, sub, right: FM.icon("chevR", "sm"),
      onTap: () => A.sheet({ title: t, body: `<div class="a-card-sub">${FM.esc(sub)}</div>` }),
    })));
});

/* ========================================================================= */
/* Shell boot — bottom nav, appbar, back                                       */
/* ========================================================================= */
const BNAV = [
  ["dashboard", "Home", "dashboard"],
  ["files", "Files", "files"],
  ["search", "Search", "search"],
  ["activity", "Activity", "activity"],
  ["settings", "Settings", "settings"],
];
document.addEventListener("DOMContentLoaded", () => {
  const nav = document.getElementById("bottomnav");
  nav.innerHTML = BNAV.map(([r, t, ic]) =>
    `<a class="bnav-item" data-r="${r}" href="#/${r}">${FM.icon(ic)}<span>${t}</span></a>`).join("");
  document.getElementById("search-btn").onclick = () => FM.go("search");
  /* system-back parity: Escape simulates Back (sheets → selection → screens) */
  addEventListener("keydown", (e) => e.key === "Escape" && A.backStack.length && A.popBack());
  /* live cards re-render when an op lands while a route is open */
  FM.bus.on("ops", () => {
    const name = (location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard");
    if (["dashboard", "activity"].includes(name)) FM.route();
  });
  /* sync nav highlight + reset per-screen shell state after each route */
  addEventListener("hashchange", () => setTimeout(() => {
    const name = (location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard");
    nav.querySelectorAll(".bnav-item").forEach((a) => a.classList.toggle("on", a.dataset.r === name));
    /* clear the old screen's rerender fn BEFORE exit — otherwise exit() would
       repaint the previous screen over the route that just rendered */
    A.clearBack(); A.sel.rerender = null; A.sel.exit();
  }, 0));
  /* initial state */
  setTimeout(() => {
    const name = (location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard");
    nav.querySelectorAll(".bnav-item").forEach((a) => a.classList.toggle("on", a.dataset.r === name));
  }, 0);
});
