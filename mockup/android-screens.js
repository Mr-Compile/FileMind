/* ============================================================================
   FileMind mockup — Android shell + screens
   PLAN-021 presentation layer: Material 3 Minimal Utility, phone-first.
   Reuses FM_DATA + FM.engine + FM helpers from app.js — the deterministic
   pipeline semantics (scan → analyze → plan → safety → execute → undo)
   are identical; only the shell and presentation are Android-specific.
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
A.backStack = [];
A.pushBack = (fn, label) => { A.backStack.push({ fn, label }); A.syncBack(); };
A.popBack = () => { const top = A.backStack.pop(); A.syncBack(); top?.fn(); };
A.clearBack = () => { A.backStack.length = 0; A.syncBack(); };
A.syncBack = () => { document.getElementById("back-btn").hidden = !A.backStack.length; };

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
  const close = () => { ov.remove(); const i = A.backStack.indexOf(rec); if (i >= 0) A.backStack.splice(i, 1); A.syncBack(); };
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
  A.backStack.push(rec); A.syncBack();
  return { el: ov, close };
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

A.storageCard = () => {
  const phone = FM.store.devices.find((d) => d.kind === "android") || FM.store.devices[0];
  const pct = Math.round((phone.storage.used / phone.storage.total) * 100);
  return `<section class="a-card">
    <div class="a-card-head">${FM.icon("phone")}<h2>This device</h2><span class="grow"></span>${FM.statusChip(pct > 85 ? "NeedsAttention" : "Connected")}</div>
    ${FM.meter(pct, "Phone storage")}
    <div class="a-card-sub">${FM.bytes(phone.storage.used)} used of ${FM.bytes(phone.storage.total)} · SAF trees: Downloads, Documents</div>
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

/* ========================================================================= */
/* Home — Android dashboard                                                    */
/* ========================================================================= */
const home = (m) => {
  A.title("FileMind");
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  m.innerHTML = `
    <div class="a-page">
      <div class="a-hero">
        <div class="a-hero-t">Your files, sorted themselves.</div>
        <div class="a-hero-s">Offline-first organizer · on-device intelligence</div>
      </div>
      ${A.storageCard()}
      <section class="a-card tappable" data-go="organizer">
        <div class="a-card-head">${FM.icon("organize")}<h2>Organize</h2><span class="grow"></span>${FM.icon("chevR")}</div>
        <div class="a-card-sub">Scan a folder, preview the plan, approve — every step is reversible.</div>
      </section>
      <section class="a-card tappable" data-go="activity">
        <div class="a-card-head">${FM.icon("warn")}<h2>Smart Inbox</h2><span class="grow"></span>
          ${pending.length ? `<span class="a-badge">${pending.length}</span>` : FM.icon("chevR")}</div>
        <div class="a-card-sub">${pending.length ? `${pending.length} file(s) need your call.` : "Nothing waiting for review."}</div>
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
/* Files — SAF-scoped browser with long-press selection                        */
/* ========================================================================= */
FM.screen("files", (m) => {
  A.title("Files");
  const scopes = [
    ["Downloads", FM_DATA.roots.downloads],
    ["Documents", FM_DATA.roots.documents],
    ["Desktop", FM_DATA.roots.desktop],
  ];
  let scope = "Downloads", q = "", kind = "All";

  const render = () => {
    const root = scopes.find(([n]) => n === scope)[1];
    const kinds = ["All", ...new Set(FM.store.files.filter((f) => f.sourceUri.startsWith(root)).map((f) => FM.engine.kindOf(f.extension)))];
    let list = FM.store.files.filter((f) => f.sourceUri.startsWith(root));
    if (kind !== "All") list = list.filter((f) => FM.engine.kindOf(f.extension) === kind);
    if (q) list = list.filter((f) => f.displayName.toLowerCase().includes(q.toLowerCase()));

    m.innerHTML = `
      <div class="a-page">
        <div class="a-scope">${scopes.map(([n]) => `<button class="a-scope-chip${n === scope ? " on" : ""}" data-scope="${n}">${FM.icon("folder", "sm")}${n}</button>`).join("")}</div>
        <div class="a-searchbar">${FM.icon("search", "sm")}<input id="fq" type="search" placeholder="Search in ${scope}" value="${FM.esc(q)}" aria-label="Search in ${scope}"></div>
        <div class="a-chips" id="kchips">${kinds.map((k) => `<button class="a-kchip${k === kind ? " on" : ""}" data-k="${k}">${k}</button>`).join("")}</div>
        <div class="a-list" id="flist"></div>
        ${A.demoNote()}
        <button class="a-fab" data-go="organizer" aria-label="Organize this folder">${FM.icon("organize")}<span>Organize</span></button>
      </div>`;
    m.querySelector("[data-go]").onclick = () => FM.go("organizer");
    m.querySelectorAll("[data-scope]").forEach((b) => (b.onclick = () => { scope = b.dataset.scope; kind = "All"; A.sel.exit(); render(); }));
    m.querySelectorAll("[data-k]").forEach((b) => (b.onclick = () => { kind = b.dataset.k; render(); }));
    const inp = m.querySelector("#fq");
    inp.oninput = () => { q = inp.value; clearTimeout(inp._t); inp._t = setTimeout(render, 160); };
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
        onOpen: () => A.sheet({
          title: f.displayName,
          body: `<div class="a-detail">
              <div><span>Location</span><b>${FM.esc(A.saf(f.sourceUri))}</b></div>
              <div><span>Size</span><b>${FM.bytes(f.sizeBytes)}</b></div>
              <div><span>Kind</span><b>${FM.esc(FM.engine.kindOf(f.extension))}</b></div>
              <div><span>Modified</span><b>${FM.dt(f.modifiedAt)}</b></div>
            </div>`,
          actions: [
            { label: "Organize this file", icon: "organize", kind: "primary", run: () => FM.go("organizer") },
            { label: "Select", icon: "check", run: () => { A.sel.enter(render); A.sel.items.add(id); A.sel.sync(); render(); } },
          ],
        }),
      }));
    });
  };
  render();
});

/* ---------- selection actions (contextual bar "more") ---------- */
document.getElementById("sel-sheet")?.addEventListener("click", () => {
  const n = A.sel.items.size;
  A.sheet({
    title: `${n} file(s) selected`,
    actions: [
      { label: "Organize selected", icon: "organize", kind: "primary", run: () => { A.sel.exit(); FM.go("organizer"); } },
      { label: "Send to Smart Inbox", icon: "warn", run: () => { FM.toast(`${n} file(s) queued for review (mock)`, "success"); A.sel.exit(); } },
      { label: "Clear selection", icon: "close", run: () => A.sel.exit() },
    ],
  });
});
document.getElementById("sel-close")?.addEventListener("click", () => A.sel.exit());

/* ========================================================================= */
/* Search                                                                      */
/* ========================================================================= */
FM.screen("search", (m) => {
  A.title("Search");
  let q = "", kind = "All";
  const render = () => {
    let list = FM.store.files;
    if (kind !== "All") list = list.filter((f) => FM.engine.kindOf(f.extension) === kind);
    if (q) {
      const s = q.toLowerCase();
      list = list.filter((f) => f.displayName.toLowerCase().includes(s) || f.extension.includes(s));
    }
    const kinds = ["All", ...new Set(FM.store.files.map((f) => FM.engine.kindOf(f.extension)))];
    m.innerHTML = `
      <div class="a-page">
        <div class="a-searchbar lg">${FM.icon("search")}<input id="sq" type="search" placeholder="Search files" value="${FM.esc(q)}" aria-label="Search files"></div>
        <div class="a-chips">${kinds.map((k) => `<button class="a-kchip${k === kind ? " on" : ""}" data-k="${k}">${k}</button>`).join("")}</div>
        <div class="a-sec">${q || kind !== "All" ? `${list.length} result(s)` : "All indexed files"}</div>
        <div class="a-list" id="slist"></div>
        <div class="a-note">${FM.icon("info", "sm")}<span>Mock search filters in memory. The real app queries the FTS5 index.</span></div>
      </div>`;
    m.querySelectorAll("[data-k]").forEach((b) => (b.onclick = () => { kind = b.dataset.k; render(); }));
    const inp = m.querySelector("#sq");
    inp.oninput = () => { q = inp.value; clearTimeout(inp._t); inp._t = setTimeout(render, 160); };
    const sl = m.querySelector("#slist");
    list.slice(0, 40).forEach((f) =>
      sl.append(A.fileRow(f, {
        onOpen: () => FM.go("files"),
      })));
    if (!list.length) sl.innerHTML = FM.empty("search", "No matches", "Try a different name, extension, or kind filter.");
  };
  render();
});

/* ========================================================================= */
/* Organizer — permission explainer → scope → analyze → plan → safety → run    */
/* ========================================================================= */
FM.screen("organizer", (m) => {
  A.title("Organizer");
  const S = { step: "perm", scope: null, analyzed: null, plan: null, safety: null, done: null };
  const trees = [
    ["Downloads", FM_DATA.roots.downloads, true],
    ["Documents", FM_DATA.roots.documents, false],
    ["Desktop", FM_DATA.roots.desktop, false],
  ];

  const steps = (cur) => {
    const names = ["Access", "Folder", "Analyze", "Plan", "Run"];
    const map = { perm: 0, scope: 1, plan: 3, run: 4, result: 4 };
    const idx = map[cur] ?? 2;
    return `<div class="a-steps">${names.map((n, i) =>
      `<span class="a-step${i < idx ? " done" : i === idx ? " cur" : ""}">${i < idx ? FM.icon("check", "sm") : ""}${n}</span>`).join('<span class="a-step-sep"></span>')}</div>`;
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
      trees.forEach(([name, root, granted]) => {
        const count = FM.store.files.filter((f) => f.sourceUri.startsWith(root)).length;
        t.append(A.row({
          icon: FM.icon("folder"),
          title: name,
          sub: `${A.saf(root)} · ${count} file(s) · ${granted ? "Access granted" : "Will prompt for access"}`,
          right: FM.statusChip(granted ? "Connected" : "Ask"),
          onTap: () => {
            S.scope = root; S.scopeName = name;
            const cands = FM.store.files.filter((f) => f.sourceUri.startsWith(root));
            S.analyzed = FM.engine.analyze(cands);
            S.plan = FM.engine.plan(S.analyzed, true, root + "\\organized", root);
            S.safety = FM.engine.safety(S.analyzed, S.plan, root);
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
            <div class="a-card-sub">${p.items.length} planned action(s), ${moves.length} moving${asks.length ? `, ${asks.length} to Smart Inbox` : ""} · ${p.skipped.length} skipped · dry-run preview</div>
          </section>
          ${sf.allowed
            ? sf.warnings.length
              ? `<div class="a-card warnline">${sf.warnings.map((w) => `<div>${FM.icon("warn", "sm")} ${FM.esc(w)}</div>`).join("")}</div>`
              : `<div class="a-card okline">${FM.icon("check", "sm")} Safety check passed — protected paths untouched, every move has an undo recipe.</div>`
            : `<div class="a-card errline">${sf.blockers.map((b) => `<div>${FM.icon("error", "sm")} ${FM.esc(b)}</div>`).join("")}</div>`}
          <div class="a-sec">Planned moves</div>
          <div class="a-list" id="plist"></div>
          <div class="a-cta">
            <button class="btn primary lg" id="exec" ${sf.allowed ? "" : "disabled"}>Run this plan</button>
            <button class="btn ghost" id="back">Pick another folder</button>
          </div>
          ${A.demoNote()}
        </div>`;
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
      m.querySelector("#undo").onclick = () => { FM.toast("Undo queued — the run reverses via recorded recipes (mock).", "info"); };
      m.querySelector("#more").onclick = () => FM.go("activity");
    }
  };

  const runExec = async () => {
    m.innerHTML = `
      <div class="a-page">
        ${steps("run")}
        <section class="a-card">
          <div class="a-card-head">${FM.icon("activity")}<h2>Running…</h2></div>
          <div class="a-list tight" id="oplog"></div>
        </section>
      </div>`;
    const log = m.querySelector("#oplog");
    FM.store.activeOp = { label: `Organize ${S.scopeName}`, status: "Validating", pct: 0, done: 0, total: S.plan.items.length, current: "" };
    FM.bus.emit("ops");
    const addStep = (icon, text) => {
      const d = document.createElement("div");
      d.className = "a-opstep";
      d.innerHTML = `${icon} ${FM.esc(text)}`;
      log.append(d);
      log.scrollTop = log.scrollHeight;
    };
    try {
      const record = await FM.engine.execute(S.plan, (stage, rec, it) => {
        if (it) {
          addStep(it.status === "Committed" ? FM.icon("check", "sm") : it.status === "Failed" ? FM.icon("error", "sm") : FM.icon("clock", "sm"),
            `${it.status === "Executing" ? "Moving " : ""}${it.originalLocation.split("\\").pop()}${it.status === "Committed" ? " — moved" : it.status === "Failed" ? " — failed" : ""}`);
          FM.store.activeOp.done = rec.items.filter((x) => x.status === "Committed").length;
          FM.store.activeOp.pct = Math.round((FM.store.activeOp.done / rec.items.length) * 100);
          FM.bus.emit("ops");
        } else {
          addStep(FM.icon("clock", "sm"), rec.status);
        }
      }, null);
      FM.store.activeOp = null; FM.bus.emit("ops");
      FM.store.ops.unshift(record);
      FM.store.activity.unshift({ t: record.committedAt, text: `Organize ${S.scopeName} — ${record.items.length} file(s) moved`, opId: record.operationId });
      S.done = record;
      S.step = "result"; render();
    } catch (e) {
      FM.store.activeOp = null; FM.bus.emit("ops");
      S.step = "scope"; render();
      FM.toast(`Run failed: ${e.message}`, "error");
    }
  };
  render();
});

/* ========================================================================= */
/* Activity — operations + Smart Inbox                                         */
/* ========================================================================= */
FM.screen("activity", (m) => {
  A.title("Activity");
  const pending = FM.store.inbox.filter((i) => i.status === "Pending");
  m.innerHTML = `
    <div class="a-page">
      <div class="a-sec">Smart Inbox ${pending.length ? `· ${pending.length} pending` : ""}</div>
      <div class="a-list" id="inbox"></div>
      <div class="a-sec">Operations</div>
      <div class="a-list" id="ops"></div>
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
    const done = o.items.filter((i) => i.status === "Committed").length;
    const failed = o.items.filter((i) => i.status === "Failed").length;
    ops.append(A.row({
      icon: FM.icon("move"), title: `Organize · ${o.items.length} item(s)`,
      sub: `${FM.dt(o.createdAt)} · ${done} moved${failed ? ` · ${failed} failed` : ""}`,
      right: FM.statusChip(o.status),
      onTap: () => A.sheet({
        title: "Operation detail",
        body: `<div class="a-list tight">${o.items.slice(0, 12).map((i) =>
          `<div class="a-row"><span class="a-row-ic">${FM.icon(i.status === "Failed" ? "error" : "check", "sm")}</span>
           <span class="a-row-txt"><span class="a-row-title">${FM.esc(i.originalLocation.split("\\").pop())}</span>
           <span class="a-row-sub">${i.status}${i.error ? " · " + FM.esc(i.error.message) : i.resultingLocation ? " → " + FM.esc(A.saf(i.resultingLocation)) : ""}</span></span></div>`).join("")}</div>`,
        actions: o.status === "Committed" ? [{ label: "Undo operation", icon: "undo", run: () => FM.toast("Undo queued (mock)", "info") }] : [],
      }),
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
    const pct = Math.round((d.storage.used / d.storage.total) * 100);
    list.append(A.row({
      icon: FM.icon(d.icon), title: FM.esc(d.name),
      sub: `${FM.bytes(d.storage.used)} / ${FM.bytes(d.storage.total)} · ${d.capabilities.slice(0, 2).join(", ")}`,
      right: FM.statusChip(d.status === "connected" ? "Connected" : "Missing"),
      onTap: () => A.sheet({
        title: d.name,
        body: `${FM.meter(pct, d.name)}<div class="a-detail">
          <div><span>Capabilities</span><b>${d.capabilities.map(FM.esc).join(" · ")}</b></div>
          ${d.restrictions.length ? `<div><span>Limits</span><b>${d.restrictions.map(FM.esc).join(" · ")}</b></div>` : ""}
          ${d.reconnect ? `<div><span>To connect</span><b>${FM.esc(d.reconnect)}</b></div>` : ""}</div>`,
        actions: d.connected ? [] : [{ label: "Refresh", icon: "retry", kind: "primary", run: () => FM.toast("Scanning for devices…", "info") }],
      }),
    }));
  });
});

/* ========================================================================= */
/* Tools — truthful availability                                               */
/* ========================================================================= */
FM.screen("tools", (m) => {
  A.title("Tools");
  const tools = [
    ["ai", "AI suggestions (Groq)", "Optional, off by default. Quota + privacy gate — sends file names only when enabled.", "Missing"],
    ["copy", "Duplicates", "Same-hash and similar-name groups with keep/delete suggestions.", "Connected"],
    ["link", "Related files", "Version chains and same-stem siblings (e.g. notes.txt ↔ notes (2).txt).", "Connected"],
    ["folder", "Empty folders", "Find and clean folders left behind after organizing.", "Connected"],
    ["db", "Index health", "Reindex, vacuum, and integrity checks on the local catalog.", "Connected"],
    ["undo", "Undo history", "Every operation carries a reversible recipe.", "Connected"],
    ["activity", "Watch folders", "Background organize on change. WorkManager job — planned.", "Review"],
  ];
  m.innerHTML = `<div class="a-page"><div class="a-list" id="tl"></div>${A.demoNote()}</div>`;
  const tl = m.querySelector("#tl");
  tools.forEach(([ic, t, sub, st]) =>
    tl.append(A.row({
      icon: FM.icon(ic), title: FM.esc(t), sub: FM.esc(sub), right: FM.statusChip(st),
      onTap: () => A.sheet({ title: t, body: `<div class="a-card-sub">${FM.esc(sub)}</div>` }),
    })));
});

/* ========================================================================= */
/* Settings — real theme/mode controls + permission explainers                 */
/* ========================================================================= */
FM.screen("settings", (m) => {
  A.title("Settings");
  const s = FM.store.settings;
  m.innerHTML = `
    <div class="a-page">
      <div class="a-sec">Appearance</div>
      <section class="a-card"><div class="a-seg" id="theme-seg">
        ${["system", "light", "dark"].map((t) => `<button class="a-seg-btn${FM.theme.get() === t ? " on" : ""}" data-t="${t}">${FM.icon(t === "dark" ? "moon" : t === "light" ? "sun" : "devices", "sm")}${t[0].toUpperCase() + t.slice(1)}</button>`).join("")}
      </div></section>
      <div class="a-sec">Organization</div>
      <div class="a-list" id="org"></div>
      <div class="a-sec">Privacy &amp; access</div>
      <div class="a-list" id="priv"></div>
      <div class="a-sec">About</div>
      <div class="a-list" id="about"></div>
      ${A.demoNote()}
    </div>`;
  m.querySelectorAll("#theme-seg [data-t]").forEach((b) => (b.onclick = () => { FM.theme.set(b.dataset.t); m.querySelectorAll("#theme-seg .a-seg-btn").forEach((x) => x.classList.toggle("on", x === b)); }));
  const org = m.querySelector("#org");
  [["organize", "Organization mode", `Current: ${s.organizationMode} — Smart suggests, you approve`, () => A.sheet({
      title: "Organization mode",
      actions: ["Manual", "Smart", "Automatic"].map((mo) => ({
        label: mo + (mo === s.organizationMode ? " ✓" : ""), kind: mo === s.organizationMode ? "primary" : "",
        run: () => { s.organizationMode = mo; FM.toast(`Mode: ${mo}`, "success"); FM.route(); },
      })),
    })],
    ["copy", "Conflict policy", `When a name exists: ${s.conflictPolicy}`, () => A.sheet({
      title: "Conflict policy",
      actions: ["KeepBoth", "Replace", "Skip", "Ask"].map((c) => ({ label: c, run: () => { s.conflictPolicy = c; FM.toast(`Conflict policy: ${c}`, "success"); FM.route(); } })),
    })],
    ["undo", "Operation history", "Undo recipes kept for every run", null],
  ].forEach(([ic, t, sub, fn]) => org.append(A.row({ icon: FM.icon(ic), title: t, sub, right: FM.icon("chevR", "sm"), onTap: fn })));
  const priv = m.querySelector("#priv");
  [["shield", "Folder access (SAF)", "2 trees granted: Downloads, Documents", () => A.sheet({
      title: "Folder access",
      body: `<div class="a-card-sub">Android grants access per folder. FileMind sees only what you pick — revoke any time in system Settings.</div>`,
      actions: [{ label: "Manage in system settings", icon: "open", run: () => FM.toast("Opens system SAF settings (mock)", "info") }],
    })],
    ["ai", "AI features", "Off by default. Enabling sends file names only — never contents.", null],
    ["lock", "Local catalog", "SQLite at app-private storage — nothing leaves the device", null],
  ].forEach(([ic, t, sub, fn]) => priv.append(A.row({ icon: FM.icon(ic), title: t, sub, right: FM.icon("chevR", "sm"), onTap: fn })));
  const about = m.querySelector("#about");
  about.append(A.row({ icon: FM.icon("info"), title: "Help & About", sub: "Guides, shortcuts, and app info", right: FM.icon("chevR", "sm"), onTap: () => FM.go("help") }));
  about.append(A.row({ icon: FM.icon("phone"), title: "FileMind for Android", sub: "Mockup · Compose UI in PLAN-021", right: FM.statusChip("Review") }));
});

/* ========================================================================= */
/* Help & About — PLAN-021 9-topic subtree                                     */
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
  document.getElementById("back-btn").onclick = () => A.popBack();
  document.getElementById("search-btn").onclick = () => FM.go("search");
  /* system-back parity: Escape simulates Back (sheets → selection → screens) */
  addEventListener("keydown", (e) => e.key === "Escape" && A.backStack.length && A.popBack());
  /* sync nav highlight + reset per-screen shell state after each route */
  addEventListener("hashchange", () => setTimeout(() => {
    const name = (location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard");
    nav.querySelectorAll(".bnav-item").forEach((a) => a.classList.toggle("on", a.dataset.r === name));
    A.clearBack(); A.sel.exit();
  }, 0));
  /* initial state */
  setTimeout(() => {
    const name = (location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard");
    nav.querySelectorAll(".bnav-item").forEach((a) => a.classList.toggle("on", a.dataset.r === name));
  }, 0);
});
