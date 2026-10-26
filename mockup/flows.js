/* FileMind UI mockup — interactive flows: Organizer (the spec operation flow over
   the real pipeline semantics), Console host simulator, Architecture view. */
"use strict";

const escF = FM.esc, iconF = FM.icon;

/* ============================= Organizer =============================
   Select source → Scan → Analyze → Plan → Safety → Execute → Result.
   Mirrors OrganizerPipeline + the host verbs scan/analyze/plan/execute/undo. */
FM.screen("organizer", (m, params) => {
  const st = (FM.state.org ??= { step: "source", scope: params.src || "downloads", dry: params.dry === "1" ? true : true, analyzed: null, plan: null, safety: null, record: null, abort: null, paused: false });
  const scopes = [
    ["downloads", "Downloads", FM_DATA.roots.downloads],
    ["documents", "Documents", FM_DATA.roots.documents],
    ["desktop", "Desktop", FM_DATA.roots.desktop],
    ["protected", "System folder", FM_DATA.roots.protected],
  ];
  const STEPS = ["source", "scan", "analyze", "plan", "safety", "execute", "result"];
  const stepIdx = () => STEPS.indexOf(st.step);
  const stepsBar = () => `<div class="steps">${STEPS.map((s, i) =>
    `<div class="step ${i === stepIdx() ? "on" : i < stepIdx() ? "done" : ""}"><span class="n">${i < stepIdx() ? "✓" : i + 1}</span>${s[0].toUpperCase() + s.slice(1)}</div>${i < STEPS.length - 1 ? '<span class="arr">›</span>' : ""}`).join("")}</div>`;
  const scopeFiles = () => st.scope === "protected" ? [] : FM.store.files.filter((f) => f.sourceUri.startsWith(FM_DATA.roots[st.scope]));

  const render = () => {
    const head = `<div class="page-head"><div><h1>Organizer</h1><div class="sub">The real pipeline, step by step: <span class="mono">scan → analyze → plan → safety → execute → verify → history</span>. Demo — no real files move.</div></div>
      <div class="actions"><label class="check"><input type="checkbox" id="o-dry" ${st.dry ? "checked" : ""}> Dry run</label></div></div>${stepsBar()}`;
    m.innerHTML = head + `<div id="org-body"></div>`;
    m.querySelector("#o-dry").onchange = (e) => { st.dry = e.target.checked; };
    const b = m.querySelector("#org-body");
    ({ source: vSource, scan: vScan, analyze: vAnalyze, plan: vPlan, safety: vSafety, execute: vExecute, result: vResult })[st.step](b);
  };

  /* ---------- step 1: source ---------- */
  const vSource = (b) => {
    b.innerHTML = `
    <div class="tool-grid">
      ${scopes.map(([id, t, p]) => `
        <div class="card clickable ${st.scope === id ? "" : ""}" data-src="${id}" style="${st.scope === id ? "border-color:var(--fm-primary)" : ""}">
          <h3>${iconF(id === "protected" ? "lock" : "folder")} ${t} ${st.scope === id ? '<span class="badge badge-info">selected</span>' : ""}</h3>
          <div class="tiny muted mono">${escF(p)}</div>
          <div class="tiny muted" style="margin-top:6px">${id === "protected" ? "Protected path — demonstrates a safety blocker." : `${FM.store.files.filter((f) => f.sourceUri.startsWith(p)).length} item(s)`}</div>
        </div>`).join("")}
    </div>
    <div style="margin-top:var(--sp-5)"><button class="btn primary" id="o-scan">${iconF("play", "sm")} Scan this folder</button></div>`;
    b.querySelectorAll("[data-src]").forEach((c) => (c.onclick = () => { st.scope = c.dataset.src; render(); }));
    b.querySelector("#o-scan").onclick = () => { st.step = "scan"; render(); };
  };

  /* ---------- step 2: scan (streams like `filemind scan`) ---------- */
  const vScan = async (b) => {
    if (st.scope === "protected") {
      b.innerHTML = FM.banner("error", "lock", `<strong>Protected location.</strong> <span class="mono">${escF(FM_DATA.roots.protected)}</span> is a system path — the Safety Engine denies it by default before any work begins.`);
      b.innerHTML += `<button class="btn" id="o-back">${iconF("back", "sm")} Choose a different folder</button>`;
      b.querySelector("#o-back").onclick = () => { st.step = "source"; render(); };
      return;
    }
    const files = scopeFiles();
    const excl = files.filter((f) => FM.engine.excluded(f));
    const kept = files.filter((f) => !FM.engine.excluded(f));
    b.innerHTML = `
      <div class="term" style="height:300px"><div class="term-out" id="o-scanout"></div></div>
      <div style="margin-top:var(--sp-4)"><button class="btn primary" id="o-next" disabled>${iconF("play", "sm")} Analyze ${kept.length} file(s)</button></div>`;
    const out = b.querySelector("#o-scanout");
    for (const [i, f] of kept.entries()) {
      out.innerHTML += `${escF(f.displayName.padEnd(40))} ${String(FM.n0(f.sizeBytes)).padStart(12)}  ${f.sourceScope}\n`;
      out.scrollTop = out.scrollHeight;
      await FM.sleep(90);
    }
    if (excl.length) out.innerHTML += `<span class="dim">-- ${excl.length} item(s) skipped by exclusion pattern (${excl.map((f) => f.displayName).join(", ")})</span>\n`;
    out.innerHTML += `<span class="dim">-- ${kept.length} file(s)</span>\n`;
    const next = b.querySelector("#o-next");
    next.disabled = false;
    next.onclick = () => { st.analyzed = FM.engine.analyze(kept); st.step = "analyze"; render(); };
  };

  /* ---------- step 3: analyze ---------- */
  const vAnalyze = (b) => {
    const rows = st.analyzed.map((a, i) => {
      const c = a.classification;
      return `<tr data-a="${i}" style="cursor:pointer">
        <td><div class="fname">${FM.fileIcon(c.fileKind)}<span class="n">${escF(a.candidate.displayName)}</span></div></td>
        <td class="muted">${c.fileKind}</td><td>${FM.confBar(c.confidence)}</td>
        <td class="muted">${c.subject || "-"}</td><td class="muted">${c.project || "-"}</td>
        <td>${(() => { const t = { Organize: "success", Review: "warning", Ignore: "neutral", NoOp: "offline" }[a.decision.kind]; return `<span class="chip ${t}"><span class="dot"></span>${a.decision.kind}</span>`; })()}</td>
      </tr>
      <tr class="exp" data-x="${i}" hidden><td colspan="6">
        <div class="tiny"><strong>Decision:</strong> ${escF(a.decision.reasons.join("; ") || "—")} · <strong>Rules:</strong> ${a.rules.matched.map((r) => escF(r.name)).join(", ") || "none"}</div>
        <pre style="margin-top:6px">${c.evidence.map((e) => `${e.kind.padEnd(20)} ${e.scoreContribution > 0 ? "+" : ""}${e.scoreContribution}  ${e.detail}`).join("\n")}${c.warnings.length ? "\n" + c.warnings.map((w) => "warning: " + w).join("\n") : ""}</pre>
      </td></tr>`;
    }).join("");
    const counts = st.analyzed.reduce((acc, a) => ((acc[a.decision.kind] = (acc[a.decision.kind] || 0) + 1), acc), {});
    b.innerHTML = `
      <div class="filters" style="margin-bottom:var(--sp-3)">${["Organize", "Review", "Ignore", "NoOp"].filter((k) => counts[k]).map((k) => `<span class="chip ${{ Organize: "success", Review: "warning", Ignore: "neutral", NoOp: "offline" }[k]}"><span class="dot"></span>${k} × ${counts[k]}</span>`).join("")}</div>
      <div class="tbl-wrap"><table class="tbl"><thead><tr><th>Name</th><th>Kind</th><th>Confidence</th><th>Subject</th><th>Project</th><th>Decision</th></tr></thead><tbody>${rows}</tbody></table></div>
      <div style="margin-top:var(--sp-4)"><button class="btn primary" id="o-plan">${iconF("play", "sm")} Build action plan</button></div>`;
    b.querySelectorAll("tr[data-a]").forEach((tr) => (tr.onclick = () => {
      const x = b.querySelector(`tr[data-x="${tr.dataset.a}"]`); x.hidden = !x.hidden;
    }));
    b.querySelector("#o-plan").onclick = () => {
      st.plan = FM.engine.plan(st.analyzed, st.dry, `${FM_DATA.roots[st.scope]}\\organized`);
      st.safety = FM.engine.safety(st.analyzed, st.plan, FM_DATA.roots[st.scope]);
      st.step = "plan"; render();
    };
  };

  /* ---------- step 4: plan (mirrors `filemind plan` output) ---------- */
  const vPlan = (b) => {
    const p = st.plan;
    b.innerHTML = `
      <div class="term" style="height:auto;margin-bottom:var(--sp-4)"><div class="term-out">plan ${p.planId}  items=${p.items.length}  review=${p.requiresReview}  dryRun=${p.dryRun}</div></div>
      <div class="tbl-wrap"><table class="tbl"><thead><tr><th>Operation</th><th>Source</th><th>Destination</th><th>Why</th></tr></thead><tbody>
        ${p.items.map((i) => `<tr>
          <td>${FM.statusChip(i.operation)}</td>
          <td class="tiny mono">${escF(i.source.replace(FM_DATA.roots[st.scope] + "\\", ""))}</td>
          <td class="tiny mono">${i.destination ? escF(i.destination.replace(FM_DATA.roots[st.scope] + "\\", "")) : '<span class="muted">Smart Inbox</span>'}</td>
          <td class="tiny muted">${escF(i.reason)}</td>
        </tr>`).join("")}
      </tbody></table></div>
      ${p.skipped.length ? `<div class="tiny muted" style="margin-top:var(--sp-2)">${p.skipped.length} file(s) untouched (${[...new Set(p.skipped.map((a) => a.decision.kind))].join("/")} decisions).</div>` : ""}
      <div style="margin-top:var(--sp-4)"><button class="btn primary" id="o-safety">${iconF("shield", "sm")} Run safety check</button></div>`;
    b.querySelector("#o-safety").onclick = () => { st.step = "safety"; render(); };
  };

  /* ---------- step 5: safety ---------- */
  const vSafety = (b) => {
    const s = st.safety;
    b.innerHTML = `
      ${s.blockers.map((x) => FM.banner("error", "error", `<strong>Blocked:</strong> ${escF(x)}`)).join("")}
      ${s.warnings.map((x) => FM.banner("warning", "warn", `<strong>Warning:</strong> ${escF(x)}`)).join("")}
      ${s.allowed && !s.warnings.length ? FM.banner("info", "check", "Pre-checks passed: source exists, destination is valid, no protected paths, no unresolved conflicts.") : ""}
      ${s.allowed
        ? `<div style="margin-top:var(--sp-4)">
             <button class="btn primary" id="o-exec">${iconF("play", "sm")} ${st.plan.dryRun ? "Execute dry run" : "Execute for real"}</button>
             <button class="btn" id="o-back">Back to plan</button></div>`
        : `<div style="margin-top:var(--sp-4)"><button class="btn" id="o-back">${iconF("back", "sm")} Back</button></div>`}`;
    b.querySelector("#o-exec")?.addEventListener("click", async () => {
      if (s.requiredConfirmation && !st.dry) {
        const ok = await FM.confirm({
          title: "Execute with warnings?",
          body: s.warnings.map(escF).join("<br>") + "<br><br>Every move is verified and recorded — you can undo the whole operation afterwards.",
          ok: `Execute ${st.plan.items.filter((i) => i.operation === "Move").length} move(s)`,
        });
        if (!ok) return;
      }
      st.step = "execute"; render();
    });
    b.querySelector("#o-back").onclick = () => { st.step = "plan"; render(); };
  };

  /* ---------- step 6: execute ---------- */
  const vExecute = async (b) => {
    const p = st.plan;
    /* Re-entry while running (PLAN-028): static snapshot — the engine keeps
       going; restarting execute would double-run the plan. */
    if (st.running) {
      const op = FM.store.activeOp;
      b.innerHTML = `<div class="card">
        <div style="display:flex;align-items:center;gap:var(--sp-3)">
          ${FM.statusChip("Executing")}
          <div class="progress" style="flex:1"><i style="width:${op ? Math.round(op.done / Math.max(1, op.total) * 100) : 0}%"></i></div>
          <span class="tiny muted">${op ? `${op.done}/${op.total}` : "…"} item(s)</span>
        </div>
        <div class="tiny muted" style="margin-top:var(--sp-2)">Run continues in the background — the result lands in Activity.</div>
      </div>`;
      return;
    }
    if (p.dryRun) {
      b.innerHTML = `${FM.banner("info", "info", `<strong>Dry run.</strong> The full pipeline ran end-to-end; nothing was moved. Output matches <span class="mono">filemind execute --dry-run</span>.`)}
        <div class="term" style="height:auto"><div class="term-out">-- dry-run: no changes made.</div></div>
        <div style="margin-top:var(--sp-4);display:flex;gap:var(--sp-2)">
          <button class="btn primary" id="o-real">${iconF("play", "sm")} Run for real</button>
          <button class="btn" id="o-again">Start over</button></div>`;
      b.querySelector("#o-real").onclick = () => { st.dry = false; st.plan = FM.engine.plan(st.analyzed, false, `${FM_DATA.roots[st.scope]}\\organized`); st.safety = FM.engine.safety(st.analyzed, st.plan, FM_DATA.roots[st.scope]); st.step = "safety"; render(); };
      b.querySelector("#o-again").onclick = () => { Object.assign(st, { step: "source", analyzed: null, plan: null, safety: null, record: null }); render(); };
      return;
    }
    st.abort = new AbortController();
    st.running = true;
    b.innerHTML = `
      <div class="card">
        <div style="display:flex;align-items:center;gap:var(--sp-3);margin-bottom:var(--sp-3)">
          <span id="o-stage">${FM.statusChip("Validating")}</span>
          <div class="progress" style="flex:1"><i id="o-pct" style="width:0%"></i></div>
          <span class="tiny muted" id="o-count"></span>
        </div>
        <div class="tiny muted" id="o-cur" style="margin-bottom:var(--sp-3)"></div>
        <div class="tbl-wrap"><table class="tbl"><tbody id="o-items">
          ${p.items.filter((i) => i.operation === "Move").map((i) => `<tr data-it="${i.itemId}"><td class="tiny mono" style="max-width:280px">${escF(i.source.split("\\").pop())}</td><td>${FM.statusChip("Planned")}</td></tr>`).join("")}
        </tbody></table></div>
        <div style="margin-top:var(--sp-3);display:flex;gap:var(--sp-2)">
          <button class="btn" id="o-cancel">${iconF("cancel", "sm")} Cancel</button>
        </div>
      </div>`;
    const moves = p.items.filter((i) => i.operation === "Move");
    FM.store.activeOp = { label: `Organize ${st.scope}`, status: "Validating", pct: 0, done: 0, total: moves.length, current: "" };
    FM.bus.emit("ops");
    b.querySelector("#o-cancel").onclick = () => st.abort.abort();
    const setStage = (s) => { b.querySelector("#o-stage").innerHTML = FM.statusChip(s); FM.store.activeOp.status = s; FM.bus.emit("ops"); };
    try {
      st.record = await FM.engine.execute(p, (stage, rec, it) => {
        setStage(rec.status === "Committed" ? "Verifying" : rec.status);
        const done = rec.items.filter((i) => i.status === "Committed").length;
        b.querySelector("#o-pct").style.width = Math.round((done / rec.items.length) * 100) + "%";
        b.querySelector("#o-count").textContent = `${done}/${rec.items.length}`;
        if (it) {
          const tr = b.querySelector(`tr[data-it="${it.itemId}"]`);
          if (tr) tr.cells[1].innerHTML = FM.statusChip(it.status);
          b.querySelector("#o-cur").textContent = it.status === "Executing" ? `Moving ${it.originalLocation.split("\\").pop()}…` : "";
          FM.store.activeOp.done = done; FM.store.activeOp.pct = Math.round((done / rec.items.length) * 100); FM.store.activeOp.current = it.originalLocation.split("\\").pop();
          FM.bus.emit("ops");
        }
      }, st.abort.signal);
      st.running = false;
      FM.store.activeOp = null; FM.bus.emit("ops");
      FM.store.ops.unshift(st.record);
      FM.store.activity.unshift({ t: st.record.committedAt, text: `Organize ${st.scope} — ${st.record.items.length} file(s) moved`, opId: st.record.operationId });
      st.step = "result"; render();
    } catch (e) {
      st.running = false;
      if (e.name === "AbortError") {
        FM.store.activeOp = null; FM.bus.emit("ops");
        b.innerHTML = FM.banner("warning", "cancel", "<strong>Cancelled.</strong> The operation was aborted — mirrors <span class=\"mono\">OperationStatus.Failed → RollingBack</span> handling for partial batches.") +
          `<button class="btn" id="o-again">Start over</button>`;
        b.querySelector("#o-again").onclick = () => { Object.assign(st, { step: "source", analyzed: null, plan: null, safety: null, record: null }); render(); };
      } else throw e;
    }
  };

  /* ---------- step 7: result ---------- */
  const vResult = (b) => {
    const r = st.record;
    b.innerHTML = `
      <div class="card">
        <h3>${iconF("check")} Operation committed ${FM.statusChip(r.status)}</h3>
        <dl class="kv">
          <dt>Operation</dt><dd class="mono">${r.operationId}</dd>
          <dt>Plan</dt><dd class="mono">${r.planId}</dd>
          <dt>Moved</dt><dd>${r.items.length} file(s), all verified</dd>
          <dt>Committed</dt><dd>${FM.dt(r.committedAt)}</dd>
        </dl>
        <div style="margin-top:var(--sp-4);display:flex;gap:var(--sp-2);flex-wrap:wrap">
          <button class="btn" id="o-undo">${iconF("undo", "sm")} Undo operation</button>
          <button class="btn" id="o-act">${iconF("activity", "sm")} View in Activity</button>
          <button class="btn primary" id="o-again">Organize another folder</button>
        </div>
      </div>`;
    b.querySelector("#o-undo").onclick = async () => {
      const undoRec = {
        operationId: FM.uid(32), planId: r.planId, status: "RolledBack",
        createdAt: new Date().toISOString(), committedAt: new Date().toISOString(),
        undoOfOperationId: r.operationId, error: null,
        items: r.items.map((i) => ({ itemId: FM.uid(8), operation: "Move", originalLocation: i.resultingLocation, resultingLocation: i.originalLocation, status: "Committed", verification: "verified", error: null })),
      };
      FM.store.ops.unshift(undoRec);
      FM.store.activity.unshift({ t: undoRec.createdAt, text: `Undo ${r.operationId.slice(0, 8)} — ${undoRec.items.length} file(s) restored`, opId: undoRec.operationId });
      FM.toast("Operation undone — files restored", "success");
      FM.go("activity");
    };
    b.querySelector("#o-act").onclick = () => FM.go("activity");
    b.querySelector("#o-again").onclick = () => { Object.assign(st, { step: "source", scope: "downloads", analyzed: null, plan: null, safety: null, record: null }); render(); };
  };

  render();
});

/* ============================= Console =============================
   Simulates src/FileMind.Windows.Host/Program.cs verbs on the same mock data. */
FM.screen("console", (m) => {
  const st = (FM.state.con ??= { hist: [], hi: -1 });
  m.innerHTML = `
  <div class="page-head"><div><h1>Console host</h1><div class="sub">Simulates <span class="mono">FileMind.Windows.Host</span> — the app that actually ships today. Same verbs, same output, same mock dataset.</div></div></div>
  <div class="term"><div class="term-out" id="c-out">FileMind — offline-first file organizer (Windows backend) [demo terminal]
Type <span class="dim">--help</span> for usage. Folders: downloads, documents, desktop
</div>
  <div class="term-in"><span class="ps">&gt; filemind</span><input id="c-in" autocomplete="off" spellcheck="false" aria-label="filemind command"></div></div>
  <div class="tiny muted" style="margin-top:var(--sp-2)">Nothing executes — this renders what the real host prints for the same inputs.</div>`;
  const out = m.querySelector("#c-out"), inp = m.querySelector("#c-in");
  inp.focus();
  const print = (s, cls = "") => { out.innerHTML += cls ? `<span class="${cls}">${escF(s)}\n</span>` : `${escF(s)}\n`; out.scrollTop = out.scrollHeight; };
  const printRaw = (s) => { out.innerHTML += s + "\n"; out.scrollTop = out.scrollHeight; };

  const filesIn = (dir) => FM.store.files.filter((f) => f.sourceUri.startsWith(dir));
  const findRoot = (arg) => {
    if (!arg) return null;
    const a = arg.toLowerCase().replace(/[\\\/]+$/, "");
    return Object.values(FM_DATA.roots).find((r) => r.toLowerCase() === a || r.toLowerCase().endsWith("\\" + a) || r.toLowerCase().endsWith(a));
  };
  const pad = (s, n) => String(s).padEnd(n);
  const fmtB = (b) => b >= 1 << 30 ? `${(b / 2 ** 30).toFixed(1)} GiB`
    : b >= 1 << 20 ? `${(b / 2 ** 20).toFixed(1)} MiB`
    : b >= 1 << 10 ? `${(b / 2 ** 10).toFixed(1)} KiB` : `${b} B`;
  const planLine = (p) => `plan ${p.planId}  items=${p.items.length}  review=${p.requiresReview}  dryRun=${p.dryRun}`;
  const itemLines = (p) => p.items.map((i) => `  ${i.operation.padEnd(12)} ${i.source}${i.destination ? " -> " + i.destination : ""}`).join("\n");

  const HELP = `FileMind - offline-first file organizer (Windows backend)

Usage:
  filemind scan <dir> [--shallow]
  filemind analyze <dir> [--shallow]
  filemind plan <dir> [--dest <root>] [--shallow] [--preserve-tree]
  filemind execute <dir> [--dest <root>] [--dry-run] [--apply] [--shallow] [--preserve-tree]
  filemind undo <operationId>
  filemind history [count]
  filemind smart-inbox [resolve <id>]
  filemind mode [<manual|smart|automatic|ai-assisted|local-only>]
  filemind rules [list]
  filemind rules show <id>
  filemind rules test <id|rule.json> <dir>
  filemind rules import <file>
  filemind rules export <id> [--out <file>]
  filemind watch add <dir> [--mode <mode>] [--tree]
  filemind watch remove <dir>
  filemind watch list
  filemind watch tree <dir> [on|off]
  filemind watch [run] [--seconds N] [--apply]
  filemind watch-status
  filemind enrich <dir> [--shallow]
  filemind dupes [<prefix>] [--plan] [--apply] [--dest <root>]
  filemind storage [<prefix>]
  filemind search [text] [--name <s>] [--ext <e>] [--kind <k>] [--older-than <d>] [--newer-than <d>] [--limit <n>]
  filemind ai-status
  filemind ai-usage [count]
  filemind ai-key <set <key>|clear|status>
  filemind ai-suggest <file>
  filemind ai-rule <natural language>
  filemind ai-search <natural language>
  filemind learn [list]
  filemind learn record <path> <chosen-destination>
  filemind learn suggest [--dest <root>]
  filemind learn calibration
  filemind related [<path>]
  filemind profile export [--out <file>]
  filemind profile import <file> [--preview|--apply] [--policy merge|skip]
  filemind profile backup [--dir <dir>]
  filemind profile restore <file>
  filemind seed-demo

Environment:
  FILEMIND_DB   SQLite database path (default: %LOCALAPPDATA%\\FileMind\\filemind.db)
  FILEMIND_DEST Default destination root for plan/execute`;

  const run = async (line) => {
    printRaw(`<span class="dim">&gt; filemind ${escF(line)}</span>`);
    const args = line.trim().split(/\s+/).filter(Boolean);
    if (!args.length) return;
    const cmd = args[0].toLowerCase();
    const pos = args.slice(1).filter((a) => !a.startsWith("--"));
    const flags = new Set(args.slice(1).filter((a) => a.startsWith("--")));
    const flagVal = (n) => { const i = args.indexOf(n); return i >= 0 ? args[i + 1] : null; };
    const err = (s) => print(`error: ${s}`, "err");

    if (cmd === "clear") { out.innerHTML = ""; return; }
    if (cmd === "-h" || cmd === "--help" || cmd === "help") return print(HELP);
    if (cmd === "seed-demo")
      return print("Seeded demo catalog: subject IT421, project alpha, collections school/work.");

    const root = pos[0] ? findRoot(pos[0]) : null;
    const needDir = () => {
      if (!pos[0]) { err("missing <dir> argument"); return true; }
      if (!root) { err(`directory '${pos[0]}' does not exist`); return true; }
      return false;
    };

    switch (cmd) {
      case "scan": {
        if (needDir()) return;
        const kept = filesIn(root).filter((f) => !FM.engine.excluded(f));
        for (const c of kept) print(`${pad(c.displayName, 40)} ${String(FM.n0(c.sizeBytes)).padStart(10)}  ${c.sourceScope}`);
        print(`-- ${kept.length} file(s); indexed ${kept.length}, hashed ${kept.length}`);
        return;
      }
      case "analyze": {
        if (needDir()) return;
        const res = FM.engine.analyze(filesIn(root).filter((f) => !FM.engine.excluded(f)));
        for (const a of res) {
          const c = a.classification;
          print(`${pad(a.candidate.displayName, 40)} kind=${pad(c.fileKind, 12)} conf=${String(c.confidence).padStart(5)} subject=${c.subject ?? "-"} project=${c.project ?? "-"} -> ${a.decision.kind}`);
          if (a.decision.kind === "Review") print(`    review: ${a.decision.reasons.join("; ")}`, "dim");
        }
        return;
      }
      case "plan": {
        if (needDir()) return;
        const res = FM.engine.analyze(filesIn(root).filter((f) => !FM.engine.excluded(f)));
        const dest = flagVal("--dest") || `${root}\\organized`;
        const p = FM.engine.plan(res, true, dest, root, flags.has("--preserve-tree"));
        print(planLine(p));
        print(itemLines(p));
        return;
      }
      case "execute": {
        if (needDir()) return;
        const res = FM.engine.analyze(filesIn(root).filter((f) => !FM.engine.excluded(f)));
        /* mirrors ModePolicy: Manual needs --apply; Review/needsReview -> Smart Inbox; NoOp/Ignore -> skip */
        const mode = (FM.store.settings.organizationMode || "Manual").toLowerCase();
        const apply = flags.has("--apply");
        const previewOnly = mode === "manual" && !apply;
        const route = (a) => {
          if (a.decision.kind === "NoOp" || a.decision.kind === "Ignore") return "Skip";
          if (mode === "manual" && !(apply || previewOnly)) return "PreviewOnly";
          if (mode === "automatic") return "Execute";
          return (a.decision.kind === "Review" || a.classification.needsReview) ? "SmartInbox" : "Execute";
        };
        const executable = res.filter((a) => route(a) === "Execute");
        print(`-- mode=${mode}; ${executable.length} item(s) routed to execute`);
        const dest = flagVal("--dest") || `${root}\\organized`;
        const dry = flags.has("--dry-run") || previewOnly;
        const p = FM.engine.plan(executable, dry, dest, root, flags.has("--preserve-tree"));
        print(planLine(p));
        print(itemLines(p));
        const sf = FM.engine.safety(executable, p, root);
        sf.warnings.forEach((w) => print(`  warn: ${w}`, "dim"));
        if (!sf.allowed) {
          sf.blockers.forEach((x) => print(`  blocked: ${x}`, "err"));
          return err("Safety validation failed; nothing executed.");
        }
        if (previewOnly) return print("-- mode=manual: preview only. Re-run with --apply to execute.", "dim");
        if (p.dryRun) return print("-- dry-run: no changes made.", "dim");
        print(`  <running staged pipeline: Validating → Executing → Verifying>`, "dim");
        const rec = await FM.engine.execute(p, null, null);
        FM.store.ops.unshift(rec);
        FM.store.activity.unshift({ t: rec.committedAt, text: `Console: organize ${pos[0]} — ${rec.items.length} file(s) moved`, opId: rec.operationId });
        FM.bus.emit("ops");
        print(`operation ${rec.operationId}: ${rec.status}`, "ok");
        rec.items.forEach((i) => print(`  ${i.operation.padEnd(10)} ${i.originalLocation} -> ${i.resultingLocation} [${i.status}]`));
        return;
      }
      case "undo": {
        if (!pos[0]) return err("usage: filemind undo <operationId>");
        const o = FM.store.ops.find((x) => x.operationId.startsWith(pos[0]));
        if (!o) return err(`operation '${pos[0]}' not found`);
        if (o.status !== "Committed") return err(`operation ${o.operationId} is ${o.status}; only Committed operations can be undone`);
        const undoRec = {
          operationId: FM.uid(32), planId: o.planId, status: "RolledBack",
          createdAt: new Date().toISOString(), committedAt: new Date().toISOString(), undoOfOperationId: o.operationId, error: null,
          items: o.items.filter((i) => i.status === "Committed").map((i) => ({ itemId: FM.uid(8), operation: "Move", originalLocation: i.resultingLocation, resultingLocation: i.originalLocation, status: "Committed", verification: "verified", error: null })),
        };
        FM.store.ops.unshift(undoRec);
        FM.bus.emit("ops");
        print(`undo operation ${undoRec.operationId}: ${undoRec.status}`);
        undoRec.items.forEach((i) => print(`  ${i.operation.padEnd(10)} ${i.originalLocation} -> ${i.resultingLocation} [${i.status}]`));
        return;
      }
      case "history": {
        const n = pos[0] && /^\d+$/.test(pos[0]) ? +pos[0] : 10;
        FM.store.ops.slice(0, n).forEach((r) =>
          print(`${r.operationId}  ${pad(r.status, 15)} ${FM.utc(r.createdAt)}  plan=${r.planId}  items=${r.items.length}`));
        return;
      }
      case "smart-inbox": {
        if (pos[0] === "resolve") {
          if (!pos[1]) return err("usage: filemind smart-inbox resolve <id>");
          const it = FM.store.inbox.find((i) => i.id === pos[1] && i.status === "Pending");
          if (!it) return err(`could not resolve '${pos[1]}'`);
          it.status = "Resolved";
          FM.bus.emit("ops");
          return;
        }
        const open = FM.store.inbox.filter((i) => i.status === "Pending");
        for (const i of open) {
          const conf = (i.reasons.join(" ").match(/Confidence (\d+)/) || [null, "-"])[1];
          print(`${i.id}  conf=${String(conf).padStart(4)}  ${i.sourceUri}\n    reason: ${i.reasons.join("; ")}`);
        }
        print(`-- ${open.length} open item(s)`);
        return;
      }
      case "mode": {
        const okModes = ["manual", "smart", "automatic", "ai-assisted", "local-only"];
        const display = (m) => m.toLowerCase() === "aiassisted" ? "ai-assisted" : m.toLowerCase() === "localonly" ? "local-only" : m.toLowerCase();
        if (!pos[0]) {
          print(`mode=${display(FM.store.settings.organizationMode || "Manual")}`);
          print("modes: manual (preview until --apply), smart (organize confident, rest to Smart Inbox),");
          print("       automatic (execute all safety-passed), ai-assisted (smart + AI, needs PLAN-009), local-only (smart, AI off)");
          return;
        }
        const req = pos[0] === "set" && pos[1] ? pos[1] : pos[0];
        if (!okModes.includes(req)) return err(`unknown mode '${req}'.`);
        const enumName = { "ai-assisted": "AiAssisted", "local-only": "LocalOnly" };
        FM.store.settings.organizationMode = enumName[req] ?? (req[0].toUpperCase() + req.slice(1));
        print(`mode=${req}`);
        return;
      }
      case "rules": {
        const sub = pos[0] ?? "list";
        const countConds = (g) => (g?.conditions?.length ?? 0) + (g?.groups ?? []).reduce((s, x) => s + countConds(x), 0);
        if (sub === "list") {
          [...FM.store.rules].sort((a, b) => b.priority - a.priority).forEach((r) =>
            print(`${pad(r.id, 16)} ${pad(r.name, 30)} priority=${String(r.priority).padStart(4)} enabled=${r.enabled} provenance=${r.provenance} conditions=${countConds(r.conditionRoot)}`));
          return print(`-- ${FM.store.rules.length} rule(s)`);
        }
        if (sub === "show" || sub === "export") {
          if (!pos[1]) return err(`usage: filemind rules ${sub} <id>${sub === "export" ? " [--out <file>]" : ""}`);
          const r = FM.store.rules.find((x) => x.id === pos[1]);
          if (!r) return err(`rule '${pos[1]}' not found`);
          if (sub === "export" && flagVal("--out")) return print(`exported ${r.id} -> ${flagVal("--out")} (demo: nothing written)`);
          return print(JSON.stringify(r, null, 2));
        }
        if (sub === "test") {
          if (!pos[1] || !pos[2]) return err("usage: filemind rules test <id|rule.json> <dir>");
          const r = FM.store.rules.find((x) => x.id === pos[1]);
          if (!r) return err(`rule '${pos[1]}' not found (demo loads stored rules only)`);
          const tRoot = findRoot(pos[2]);
          if (!tRoot) return err(`directory '${pos[2]}' does not exist`);
          const res = FM.engine.analyze(filesIn(tRoot).filter((f) => !FM.engine.excluded(f)));
          let matched = 0;
          for (const a of res) {
            const hit = FM.engine.evalGroup(r.conditionRoot, a.candidate, a.classification);
            if (hit) matched++;
            print(`${(hit ? "MATCH" : "SKIP ").padEnd(6)} ${pad(a.candidate.displayName, 40)} ${hit ? r.actions.map((x) => x.kind).join(", ") : "-"}`);
          }
          return print(`-- rule ${r.id}: ${matched}/${res.length} candidate(s) matched (test only, nothing changed)`);
        }
        if (sub === "import") return err("demo: import reads a real .json file — host validates then upserts with provenance=Imported");
        return err("usage: filemind rules [list|show|test|import|export]");
      }
      case "enrich": {
        if (needDir()) return;
        const res = FM.engine.analyze(filesIn(root).filter((f) => !FM.engine.excluded(f)));
        for (const a of res) {
          const c = a.classification;
          const meta = a.candidate.metadata ? Object.entries(a.candidate.metadata).map(([k, v]) => `${k}=${v}`).join(", ") : "-";
          const evs = c.evidence.filter((e) => e.kind.startsWith("content-") || /extract|title|manifest|id3|exif|ooxml|pdf|code|video|audio|image/.test(e.kind));
          print(`${pad(a.candidate.displayName, 40)} kind=${pad(c.fileKind, 12)} extracted=${String(a.candidate.extracted?.length ?? 0).padStart(5)}ch meta=[${meta}]`);
          evs.forEach((e) => print(`    ev ${pad(e.kind, 28)} +${String(e.scoreContribution).padStart(5)}  ${e.detail}`));
          (c.warnings || []).forEach((w) => print(`    warn: ${w}`, "dim"));
        }
        return;
      }
      case "dupes": {
        const pool = pos[0] ? filesIn(pos[0]) : FM.store.files;
        const bySize = new Map();
        for (const f of pool.filter((x) => x.sizeBytes > 0)) {
          (bySize.get(f.sizeBytes) ?? bySize.set(f.sizeBytes, []).get(f.sizeBytes)).push(f);
        }
        const groups = [...bySize.values()].filter((g) => g.length >= 2);
        const inGroup = new Set(groups.flat());
        const similar = [];
        const rest = pool.filter((f) => !inGroup.has(f));
        for (let i = 0; i < rest.length; i++)
          for (let j = i + 1; j < rest.length; j++) {
            const a = rest[i], b = rest[j];
            if (a.extension !== b.extension) continue;
            const ta = new Set(a.displayName.replace(/\.[^.]+$/, "").toLowerCase().split(/\W+/).filter(Boolean));
            const tb = new Set(b.displayName.replace(/\.[^.]+$/, "").toLowerCase().split(/\W+/).filter(Boolean));
            const jac = [...ta].filter((t) => tb.has(t)).length / new Set([...ta, ...tb]).size;
            if (jac >= 0.5 && Math.min(a.sizeBytes, b.sizeBytes) / Math.max(a.sizeBytes, b.sizeBytes) >= 0.9)
              similar.push([a, b, jac]);
          }
        const reclaim = groups.reduce((s, g) => s + g[0].sizeBytes * (g.length - 1), 0);
        print(`-- ${groups.length} duplicate group(s), ${similar.length} similar pair(s)`);
        groups.forEach((g) => {
          print(`group exact  ${fmtB(g[0].sizeBytes)} x${g.length}  reclaimable=${fmtB(g[0].sizeBytes * (g.length - 1))}`);
          g.forEach((f) => print(`  ${f.sourceUri}`));
        });
        similar.forEach(([a, b, j]) =>
          print(`similar ${(20 + j * 60 + 20).toFixed(1).padStart(5)}  ${a.sourceUri} <-> ${b.sourceUri}\n    same type ${a.extension}; name overlap ${Math.round(j * 100)}%; size within 100%`));
        print(`total reclaimable: ${fmtB(reclaim)} (advisory — no files were moved or deleted)`);
        if (!flags.has("--plan")) return;
        const review = `${flagVal("--dest") ?? pos[0] ?? "organized"}\\duplicates-review`;
        const items = groups.flatMap((g) => g.slice(1));
        if (!items.length) return print("-- no confirmed duplicates to quarantine; nothing planned");
        print(`plan dupes-demo  items=${items.length}  review=True  dryRun=${!flags.has("--apply")}`);
        items.forEach((f) => print(`  Move         ${f.sourceUri} -> ${review}\\${f.displayName}`));
        if (!flags.has("--apply")) return print("-- preview only. Re-run with --plan --apply to quarantine copies.");
        return print("-- executed " + items.length + "/" + items.length + " item(s); op=demo-dupes (demo: files not actually moved)");
      }
      case "storage": {
        const pool = pos[0] ? filesIn(pos[0]) : FM.store.files;
        const now = Date.now();
        const days = (f) => Math.floor((now - new Date(f.modifiedAt).getTime()) / 864e5);
        const kindOf = (f) => FM.engine.analyze([f])[0]?.classification.fileKind ?? "Other";
        const byKind = new Map();
        pool.forEach((f) => {
          const k = kindOf(f);
          const b = byKind.get(k) ?? { bytes: 0, n: 0 };
          b.bytes += f.sizeBytes; b.n++; byKind.set(k, b);
        });
        const bands = [["0-30d", (d) => d <= 30], ["31-90d", (d) => d > 30 && d <= 90], ["91-365d", (d) => d > 90 && d <= 365], [">365d", (d) => d > 365]];
        const reclaim = [...pool.reduce((m, f) => m.set(f.sizeBytes, (m.get(f.sizeBytes) ?? 0) + 1), new Map())]
          .filter(([s, n]) => s > 0 && n > 1).reduce((t, [s, n]) => t + s * (n - 1), 0);
        print(`-- ${pool.length} file(s), ${fmtB(pool.reduce((s, f) => s + f.sizeBytes, 0))} total; reclaimable dupes=${fmtB(reclaim)}`);
        print("by kind:");
        [...byKind.entries()].sort((a, b) => b[1].bytes - a[1].bytes)
          .forEach(([k, b]) => print(`  ${pad(k, 12)} ${fmtB(b.bytes).padStart(10)}  ${b.n} file(s)`));
        print("by age:");
        bands.forEach(([label, match]) => {
          const sel = pool.filter((f) => match(days(f)));
          print(`  ${pad(label, 12)} ${fmtB(sel.reduce((s, f) => s + f.sizeBytes, 0)).padStart(10)}  ${sel.length} file(s)`);
        });
        print("largest:");
        [...pool].sort((a, b) => b.sizeBytes - a.sizeBytes).slice(0, 5)
          .forEach((f) => print(`  ${fmtB(f.sizeBytes).padStart(10)}  ${f.sourceUri}`));
        print("oldest:");
        [...pool].sort((a, b) => days(b) - days(a)).slice(0, 5)
          .forEach((f) => print(`  ${String(days(f)).padStart(5)}d  ${f.sourceUri}`));
        return;
      }
      case "search": {
        const flagVals = ["--name", "--ext", "--kind", "--older-than", "--newer-than", "--limit"]
          .map(flagVal).filter(Boolean);
        const text = pos.find((a) => !flagVals.includes(a));
        const kindOf = (f) => FM.engine.analyze([f])[0]?.classification.fileKind ?? "Other";
        const days = (f) => Math.floor((Date.now() - new Date(f.modifiedAt).getTime()) / 864e5);
        const terms = (text ?? "").toLowerCase().match(/[\p{L}\p{N}_]+/gu) ?? [];
        let pool = FM.store.files.filter((f) =>
          terms.every((t) => f.displayName.toLowerCase().includes(t) || (f.extractedTextRef ?? "").toLowerCase().includes(t)));
        if (flagVal("--name")) pool = pool.filter((f) => f.displayName.toLowerCase().includes(flagVal("--name").toLowerCase()));
        if (flagVal("--ext")) pool = pool.filter((f) => f.extension.toLowerCase() === "." + flagVal("--ext").replace(/^\./, "").toLowerCase());
        if (flagVal("--kind")) pool = pool.filter((f) => kindOf(f).toLowerCase() === flagVal("--kind").toLowerCase());
        if (flagVal("--older-than")) pool = pool.filter((f) => days(f) > +flagVal("--older-than"));
        if (flagVal("--newer-than")) pool = pool.filter((f) => days(f) <= +flagVal("--newer-than"));
        pool = pool.slice(0, +(flagVal("--limit") ?? 50));
        pool.forEach((f) => print(`${pad(f.displayName, 40)} ${pad(kindOf(f), 10)} ${fmtB(f.sizeBytes).padStart(10)}  rank=-0.000  ${f.sourceUri}`));
        print(`-- ${pool.length} result(s)` + (pool.length ? "" : " — nothing indexed matches; run `scan`/`enrich` to populate the index"));
        return;
      }
      case "profile": {
        const sub = (pos[0] ?? "").toLowerCase();
        const profileDoc = () => ({
          schemaVersion: 1,
          rules: FM.store.rules,
          collections: FM_DATA.collections,
          subjects: FM_DATA.subjects,
          projects: FM_DATA.projects ?? [],
          namingTemplates: {},
          exclusions: FM_DATA.exclusions,
          preferences: FM.store.settings.organizationMode ? { mode: FM.store.settings.organizationMode } : undefined,
        });
        if (sub === "export") {
          const out = flagVal("--out");
          if (!out) { print(JSON.stringify(profileDoc(), null, 2)); return; }
          return print(`exported ${out}`);
        }
        if (sub === "import") {
          if (!pos[1]) return err("usage: filemind profile import <file> [--preview|--apply] [--policy merge|skip]");
          const incoming = {
            subjects: [{ id: "it421", name: "IT421 Networks (imported)" }, { id: "bio-new", name: "Biology" }],
            exclusions: ["*.bak"],
          };
          const known = new Set(FM_DATA.subjects.map((s) => s.id));
          const knownEx = new Set(FM_DATA.exclusions);
          const diff = [
            ...incoming.subjects.map((s) => ({ kind: known.has(s.id) ? "changed" : "added", sec: "subjects", id: s.id })),
            ...incoming.exclusions.map((x) => ({ kind: knownEx.has(x) ? "same" : "added", sec: "exclusions", id: x })),
          ];
          const policy = flagVal("--policy") ?? "merge";
          if (!["merge", "skip"].includes(policy)) return err(`unknown policy '${policy}' (merge|skip)`);
          if (flags.has("--apply")) {
            print(`backup profile-backup-${new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14)}.json`);
            const skipped = policy === "skip" ? diff.filter((d) => d.kind !== "added").length : 0;
            print(`applied=${diff.length - skipped} skipped=${skipped} policy=${policy}`);
            return;
          }
          diff.forEach((d) => print(`${pad(d.kind, 8)} ${pad(d.sec, 12)} ${d.id}`));
          return print(`-- preview only (schema v1); re-run with --apply to write (${diff.filter((d) => d.kind !== "same").length} change(s))`);
        }
        if (sub === "backup")
          return print(`backup ${flagVal("--dir") ?? "C:\\\\Users\\\\demo\\\\FileMind"}\\\\profile-backup-${new Date().toISOString().replace(/[-:T]/g, "").slice(0, 14)}.json`);
        if (sub === "restore") {
          if (!pos[1]) return err("usage: filemind profile restore <file>");
          return print(`restored ${pos[1]}: applied=4 skipped=0`);
        }
        return err("usage: filemind profile [export|import|backup|restore]");
      }
      case "learn": {
        const sub = pos[0] ?? "list";
        const corr = (FM.store.corrections ??= []);
        if (sub === "record") {
          if (!pos[1] || !pos[2]) return err("usage: filemind learn record <path> <chosen-destination>");
          corr.push({ path: pos[1], chosen: pos[2], source: "manual" });
          return;
        }
        if (sub === "suggest") {
          const byKey = {};
          corr.forEach((c) => {
            const ext = (c.path.split(".").pop() ?? "").toLowerCase();
            const dir = c.chosen.replace(/[\\/][^\\/]+$/, "");
            (byKey[`${ext}|${dir}`] ??= []).push(c);
          });
          const drafts = Object.entries(byKey).filter(([, v]) => v.length >= 2);
          if (!drafts.length) return print("no learned suggestions yet (need 2+ agreeing corrections)");
          drafts.forEach(([k, v]) => {
            const [ext, dir] = k.split("|");
            print(`[draft:learned] learned-${ext}-draft\n    Learned: .${ext} → ${dir}\n    disabled — review before enabling`);
          });
          return print(`-- ${drafts.length} draft rule(s); none active`);
        }
        if (sub === "calibration") {
          const cal = FM.store.calibration ?? {};
          const keys = Object.keys(cal);
          if (!keys.length) return print("no calibration signals recorded");
          keys.forEach((k) => print(`${pad(k, 20)} ${cal[k] >= 0 ? "+" : ""}${cal[k]}`));
          return;
        }
        if (!corr.length) { print("-- 0 correction(s)"); return; }
        corr.forEach((c) => print(`${c.path}  ->  ${c.chosen}   (${c.source})`));
        return print(`-- ${corr.length} correction(s)`);
      }
      case "related": {
        // version chains over the mock index: v(N), " (N)", trailing year
        const files = FM.store.files ?? [];
        const stem = (n) => n.replace(/\.[^.]+$/, "");
        const vRx = /(?:^|[_\-\s])v(\d{1,4})$/i, pRx = /\s*\((\d{1,3})\)$/, yRx = /(?:^|[_\-\s])((?:19|20)\d{2})$/;
        const groups = {};
        files.forEach((f) => {
          const s = stem(f.displayName);
          const dir = f.sourceUri.replace(/[\\/][^\\/]+$/, "").toLowerCase();
          const ext = (f.extension ?? f.displayName.split(".").pop()).toLowerCase();
          let base = null, order = 0;
          let m;
          if ((m = s.match(vRx))) { base = s.replace(vRx, "").replace(/[_\-\s]+$/, ""); order = +m[1]; }
          else if ((m = s.match(pRx))) { base = s.replace(pRx, ""); order = +m[1]; }
          else if ((m = s.match(yRx))) { base = s.replace(yRx, "").replace(/[_\-\s]+$/, ""); order = +m[1]; }
          else { base = s; }
          if (!base) return;
          (groups[`${dir}|${ext}|${base.toLowerCase()}`] ??= { base, items: [] }).items.push({ f, order, ver: order > 0 });
        });
        let chains = Object.values(groups)
          .filter((g) => g.items.some((i) => i.ver) && new Set(g.items.map((i) => i.order)).size >= 2)
          .map((g) => { g.items.sort((a, b) => a.order - b.order); return g; });
        if (pos[0]) {
          const target = pos[0].toLowerCase();
          chains = chains.filter((g) => g.items.some((i) => i.f.sourceUri.toLowerCase() === target));
        }
        if (!chains.length) return print("no version chains found");
        chains.forEach((g) => {
          print(`${g.base}`);
          const latest = g.items[g.items.length - 1];
          g.items.forEach((i) => print(`    ${i === latest ? "* " : "  "}${i.f.sourceUri}`));
        });
        return print("-- suggestion only; nothing moved");
      }
      case "watch": {
        const watched = (FM.store.settings.watchedFolders ??= []);
        const sub = pos[0] ?? "run";
        if (sub === "add") {
          if (!pos[1]) return err("usage: filemind watch add <dir> [--mode <mode>] [--tree]");
          const wr = findRoot(pos[1]);
          if (!wr) return err(`directory '${pos[1]}' does not exist`);
          if (watched.some((w) => w.path === wr)) return err(`'${wr}' is already watched`);
          const m = flagVal("--mode");
          watched.push({ path: wr, mode: m ? m[0].toUpperCase() + m.slice(1) : null, tree: flags.has("--tree") });
          return print(`watching ${wr}`);
        }
        if (sub === "tree") {
          if (!pos[1]) return err("usage: filemind watch tree <dir> [on|off]");
          const w = watched.find((x) => x.path === pos[1]);
          if (!w) return err(`not watched: '${pos[1]}'`);
          if (!pos[2]) return print(`tree=${w.tree ? "on" : "off"}  ${w.path}`);
          if (!["on", "off"].includes(pos[2])) return err("usage: filemind watch tree <dir> [on|off]");
          w.tree = pos[2] === "on";
          return print(`tree=${pos[2]}  ${w.path}`);
        }
        if (sub === "remove") {
          if (!pos[1]) return err("usage: filemind watch remove <dir>");
          const i = watched.findIndex((w) => w.path === pos[1]);
          if (i < 0) return err(`not watched: '${pos[1]}'`);
          watched.splice(i, 1);
          return;
        }
        if (sub === "list") {
          const def = FM.store.settings.organizationMode || "Manual";
          watched.forEach((w) => print(`${pad(w.path, 50)} mode=${w.mode ?? def}  tree=${w.tree ? "on" : "off"}`));
          return print(`-- ${watched.length} watched folder(s)`);
        }
        // run — simulate the real loop: source → bounded queue → debounce → stability → orchestrator
        if (watched.length === 0) return err("no watched folders — add one: filemind watch add <dir>");
        const secs = flagVal("--seconds") ?? "∞";
        print(`watching ${watched.length} folder(s) for ${secs}s`, "dim");
        // demo: synthesize a Created event for the first .crdownload (skipped) and first normal file (organized)
        const wr = watched[0].path;
        const cand = filesIn(wr).filter((f) => !FM.engine.excluded(f))[0];
        const partial = (FM.store.files.find((f) => f.displayName.endsWith(".crdownload")));
        let events = 0, processed = 0, actions = 0;
        if (partial) {
          events++;
          print(`  warn: ${partial.displayName}: incomplete download — skipped until stable`, "dim");
        }
        if (cand) {
          events += 2; processed++; // Created + Changed collapse into one due event after debounce
          const res = FM.engine.analyze([cand]);
          const a = res[0];
          const wMode = (watched[0].mode ?? FM.store.settings.organizationMode ?? "Smart").toLowerCase();
          if (a.decision.kind === "NoOp" || a.decision.kind === "Ignore") {
            actions++;
            print(`  ${a.candidate.displayName}: ${a.decision.kind} — indexed only`, "dim");
          } else if (a.decision.kind === "Review" && wMode !== "automatic") {
            actions++;
            FM.store.inbox.unshift({ id: FM.uid(8), sourceUri: a.candidate.sourceUri, status: "Pending", reasons: a.decision.reasons });
            print(`[notify] Smart Inbox item: ${a.candidate.displayName}: needs review`, "dim");
          } else {
            actions++;
            const dest = `${watched[0].path}\\organized`;
            const p = FM.engine.plan(res, false, dest, wr, watched[0].tree === true);
            const rec = await FM.engine.execute(p, null, null);
            FM.store.ops.unshift(rec);
            FM.bus.emit("ops");
            print(`[notify] File organized: ${a.candidate.displayName} -> ${rec.status} (op ${rec.operationId})`, "ok");
          }
        }
        return print(`-- ${events} event(s), ${processed} processed, ${actions} action(s)`);
      }
      case "ai-status": {
        const st = FM.store.settings;
        print(`enabled=${st.aiEnabled === true}  model=openai/gpt-oss-120b`);
        print("endpoint=https://api.groq.com/openai/v1/chat/completions");
        print(`api-key=${st.aiKey ? "present (secret-store)" : "MISSING (set GROQ_API_KEY in .env or `filemind ai-key set`)"}`);
        print("consent: metadata=True extracted-text=False");
        print(`quota: ${(st.aiUsage ?? []).length}/800 req/day, 0/160000 tok/day, 0/24 req/min`);
        print(`provider: available=${st.aiEnabled === true && !!st.aiKey}`);
        print("-- advisory only; AI output never mutates files and never bypasses SafetyEngine", "dim");
        return;
      }
      case "ai-usage": {
        const n = +pos[0] || 10;
        const rows = (FM.store.settings.aiUsage ?? []).slice(0, n);
        rows.forEach((u) => print(`${u.at}  ${pad(u.purpose, 16)} ${pad("gpt-oss-120b", 22)} in=${String(u.pin).padStart(5)} out=${String(u.pout).padStart(5)}  ${u.outcome}`));
        print(`-- ${rows.length} record(s) — metadata only, payload contents are never stored`, "dim");
        return;
      }
      case "ai-key": {
        const st = FM.store.settings;
        const sub = pos[0];
        if (sub === "set") {
          if (!pos[1]) return err("usage: filemind ai-key set <key>");
          st.aiKey = pos[1];
          return print("Groq API key stored in the DPAPI user store. It never enters the repo or logs.");
        }
        if (sub === "clear") { st.aiKey = null; return print("Groq API key removed from the DPAPI user store."); }
        if (sub === "status") return print(st.aiKey ? "key configured (source: secret-store)" : "no key configured");
        return err("usage: filemind ai-key <set <key>|clear|status>");
      }
      case "ai-suggest": {
        if (!pos[0]) return err("usage: filemind ai-suggest <file>");
        if (FM.store.settings.aiEnabled !== true) return err("AI disabled (ai.enabled not set).");
        return err("No Groq API key — set GROQ_API_KEY (env or .env) or `filemind ai-key set <key>`.");
      }
      case "ai-rule": {
        if (FM.store.settings.aiEnabled !== true) return err("AI disabled (ai.enabled not set).");
        return err("No Groq API key — set GROQ_API_KEY (env or .env) or `filemind ai-key set <key>`.");
      }
      case "ai-search": {
        const nl = pos.join(" ");
        if (!nl) return err("usage: filemind ai-search <natural language>");
        if (FM.store.settings.aiEnabled !== true)
          print("-- interpretation unavailable (AiOffline); plain text search", "dim");
        const terms = nl.toLowerCase().match(/[\p{L}\p{N}_]+/gu) ?? [];
        const pool = FM.store.files.filter((f) =>
          terms.every((t) => f.displayName.toLowerCase().includes(t) || (f.extractedTextRef ?? "").toLowerCase().includes(t)));
        pool.forEach((f) => print(`${pad(f.displayName, 40)} ${fmtB(f.sizeBytes).padStart(10)}  ${f.sourceUri}`));
        print(`-- ${pool.length} result(s)`);
        return;
      }
      case "watch-status": {
        const watched = FM.store.settings.watchedFolders ?? [];
        const def = FM.store.settings.organizationMode || "Manual";
        print(`mode=${def}  watched=${watched.length}`);
        watched.forEach((w) => print(`  ${w.path}  mode=${w.mode ?? def}`));
        const open = FM.store.inbox.filter((i) => i.status === "Pending").length;
        print(`pending jobs=0  smart-inbox open=${open}`);
        return;
      }
      default:
        return err(`Unknown command '${cmd}'. Run 'filemind --help'.`);
    }
  };

  inp.addEventListener("keydown", async (e) => {
    if (e.key === "Enter" && inp.value.trim()) {
      const v = inp.value; inp.value = "";
      st.hist.push(v); st.hi = st.hist.length;
      inp.disabled = true;
      await run(v);
      inp.disabled = false; inp.focus();
    } else if (e.key === "ArrowUp") { e.preventDefault(); if (st.hi > 0) inp.value = st.hist[--st.hi]; }
    else if (e.key === "ArrowDown") { e.preventDefault(); if (st.hi < st.hist.length - 1) inp.value = st.hist[++st.hi]; else { st.hi = st.hist.length; inp.value = ""; } }
  });
});

/* ============================= Architecture =============================
   Verified module/pipeline truth — mirrors docs/architecture/ARCHITECTURE.md. */
FM.screen("architecture", (m) => {
  const stages = [
    ["FileCandidate", "file under consideration — path, size, mime, scope", "Contracts/FileCandidate.cs", "EXISTING"],
    ["Normalize", "NFKC + case-fold + separator unify; 'IT 421'='IT-421'='it421'", "Domain/Classification/Normalizer.cs", "EXISTING"],
    ["Local signals", "extension → FileKind + MIME; extractors add bounded content/metadata evidence", "ClassifierEngine.cs · Intelligence.Local analyzers", "EXISTING"],
    ["Rule engine", "JSON rules: priority → specificity → stop → conflicts", "Domain/Rules/RuleEngine.cs", "EXISTING"],
    ["Classifier", "subject code>alias>keyword + project match", "SubjectMatcher.cs · ProjectMatcher.cs", "EXISTING"],
    ["Confidence", "weighted evidence, per-kind caps, bands 95/70", "ConfidenceEngine.cs", "EXISTING"],
    ["AI (optional)", "Groq suggestions — advisory only, never mutates", "Intelligence.AI · AiAdvisor merge", "EXISTING"],
    ["Decision", "Organize / Review(Smart Inbox) / Ignore / NoOp", "Application/Decisions/DecisionMaker.cs", "EXISTING"],
    ["Safety", "protected paths deny-by-default, pre-checks, conflicts", "Domain/Safety/SafetyEngine.cs", "EXISTING"],
    ["ActionPlan", "immutable items + undo recipes + fingerprints", "Domain/Operations/ActionPlanner.cs", "EXISTING"],
    ["Platform adapter", "real System.IO moves, KeepBoth suffixing, verify", "Platform.Windows/WindowsFileSystemAdapter.cs", "EXISTING"],
    ["Verify + History", "Planned→…→Committed persisted per item in SQLite", "TransactionEngine.cs · SqliteStore.cs", "EXISTING"],
    ["Undo", "fingerprint-checked reverse plan → RolledBack", "UndoService.cs · RollbackService.cs", "EXISTING"],
  ];
  const modules = [
    ["FileMind.Domain", "Contracts, engines, rules, safety, planning, transactions, undo. Zero I/O.", "77 unit tests", "EXISTING"],
    ["FileMind.Application", "Scanner, DecisionMaker, OrganizerPipeline, indexing, startup recovery, Smart Inbox, ModePolicy, watch pipeline (queue/debounce/stability/orchestrator), duplicates + storage analysis.", "wiring + E2E", "EXISTING"],
    ["FileMind.Infrastructure.Sqlite", "WAL SQLite, schema v2 + migrations, all repositories incl. IOperationStore + file_index hash + persisted job queue.", "E2E-verified", "EXISTING"],
    ["FileMind.Platform.Windows", "Real FS adapter, path normalizer, protected paths, SHA256, FileSystemWatcher, drive attach/detach, console notifications.", "E2E-verified", "EXISTING"],
    ["FileMind.Windows.Host", "Console verbs: scan/analyze/plan/execute/undo/history/smart-inbox/mode/rules/watch/watch-status/enrich/dupes/storage/search/ai-*/seed-demo.", "the app today", "EXISTING"],
    ["FileMind.Branding", "Canonical palette tokens as C# consts + parity tests.", "8 tests", "EXISTING"],
    ["FileMind.Intelligence.Local", "Analyzer registry + extractors: OOXML/PDF/text/EXIF/ID3/video/archive-manifest/code-signals; bounded + fail-safe; feeds classification + {Title}/{Author}/{Year} rename tokens.", "F-P5", "EXISTING"],
    ["Duplicates & storage", "size→partial→full hash gating over file_index; advisory similarity candidates; kind/age breakdown; review-first quarantine plan (KeepBoth, undoable).", "F-P6", "EXISTING"],
    ["FileMind.Intelligence.AI", "Groq provider + privacy gate + quota gate + usage ledger + NL draft/interpret. Advisory only.", "F-P7", "EXISTING"],
    ["Watchers/automation", "FileSystemWatcher, bounded queue + debounce, stability checks, persisted jobs, drive pause/resume.", "F-P4", "EXISTING"],
    ["Search (FTS5)", "file_content_index FTS5 (schema v3) populated by indexing; search verb: text + name/ext/kind/age filters; local-only, escaped MATCH.", "F-P9", "EXISTING"],
    ["Android", "Kotlin/Room/SAF backend — adapters, grants, Room, WorkManager, share intake; on-device run unverified.", "F-P8", "IMPLEMENTED"],
    ["mockup/", "This preview — canonical visual reference of the approved UI.", "F-UI1", "EXISTING"],
  ];
  const chip = (s) => `<span class="chip ${s === "EXISTING" ? "success" : "offline"}"><span class="dot"></span>${s}</span>`;

  m.innerHTML = `
  <div class="page-head"><div><h1>Architecture</h1><div class="sub">How this codebase actually works — every node maps to a real file. Click one.</div></div></div>
  <h2>The one pipeline that may mutate files</h2>
  <div class="pipe" id="pipe">${stages.map(([t, d, f, s], i) =>
    `<div class="pnode ${s === "MISSING" ? "off" : ""}" data-n="${i}"><div class="t">${iconF(s === "MISSING" ? "lock" : "check", "sm")} ${t}</div><div class="d">${d}</div></div>${i < stages.length - 1 ? '<span class="parr">→</span>' : ""}`).join("")}</div>
  <div class="card" id="pdetail" style="margin-top:var(--sp-4)"><div class="tiny muted">Select a pipeline stage to see the real code behind it.</div></div>

  <h2 style="margin-top:var(--sp-6)">Modules</h2>
  <div class="tbl-wrap"><table class="tbl"><thead><tr><th>Module</th><th>Responsibility</th><th>Proof</th><th>Status</th></tr></thead><tbody>
    ${modules.map(([n, d, p, s]) => `<tr><td class="mono tiny">${n}</td><td class="tiny">${d}</td><td class="tiny muted">${p}</td><td>${chip(s)}</td></tr>`).join("")}
  </tbody></table></div>

  <h2 style="margin-top:var(--sp-6)">Boundaries that can't be crossed</h2>
  <div class="tool-grid">
    <div class="card"><h3>${iconF("shield")} UI never mutates</h3><div class="tiny muted">Only <span class="mono">Decision → Safety → ActionPlan → Platform Adapter → Verify → History</span> may change files. This mockup moves nothing real.</div></div>
    <div class="card"><h3>${iconF("ai")} AI is advisory</h3><div class="tiny muted">Groq (optional, F-P7) may suggest classifications — it can never execute file actions. Offline failure never breaks local organization.</div></div>
    <div class="card"><h3>${iconF("undo")} Everything is reversible</h3><div class="tiny muted">Every plan item carries an undo recipe + fingerprint. No silent overwrites — conflicts default to Keep Both. No delete op exists in the core.</div></div>
  </div>`;

  m.querySelectorAll(".pnode").forEach((n) => (n.onclick = () => {
    m.querySelectorAll(".pnode").forEach((x) => x.classList.remove("sel"));
    n.classList.add("sel");
    const [t, d, f, s] = stages[+n.dataset.n];
    m.querySelector("#pdetail").innerHTML = `<h3>${t} ${chip(s)}</h3>
      <div class="tiny" style="margin-bottom:6px">${d}</div>
      <div class="tiny muted">Code: <span class="mono">${f}</span></div>`;
  }));
});
