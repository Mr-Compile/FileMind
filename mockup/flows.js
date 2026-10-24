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
      FM.store.activeOp = null; FM.bus.emit("ops");
      FM.store.ops.unshift(st.record);
      FM.store.activity.unshift({ t: st.record.committedAt, text: `Organize ${st.scope} — ${st.record.items.length} file(s) moved`, opId: st.record.operationId });
      st.step = "result"; render();
    } catch (e) {
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
  const planLine = (p) => `plan ${p.planId}  items=${p.items.length}  review=${p.requiresReview}  dryRun=${p.dryRun}`;
  const itemLines = (p) => p.items.map((i) => `  ${i.operation.padEnd(12)} ${i.source}${i.destination ? " -> " + i.destination : ""}`).join("\n");

  const HELP = `FileMind - offline-first file organizer (Windows backend)

Usage:
  filemind scan <dir> [--shallow]
  filemind analyze <dir> [--shallow]
  filemind plan <dir> [--dest <root>] [--shallow]
  filemind execute <dir> [--dest <root>] [--dry-run] [--apply] [--shallow]
  filemind undo <operationId>
  filemind history [count]
  filemind smart-inbox [resolve <id>]
  filemind mode [<manual|smart|automatic|ai-assisted|local-only>]
  filemind rules [list]
  filemind rules show <id>
  filemind rules test <id|rule.json> <dir>
  filemind rules import <file>
  filemind rules export <id> [--out <file>]
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
        const p = FM.engine.plan(res, true, dest);
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
        const p = FM.engine.plan(executable, dry, dest);
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
    ["Local signals", "extension → FileKind + MIME (FileKindMap/MimeMap)", "ClassifierEngine.cs · Scanning/MimeMap.cs", "EXISTING"],
    ["Rule engine", "JSON rules: priority → specificity → stop → conflicts", "Domain/Rules/RuleEngine.cs", "EXISTING"],
    ["Classifier", "subject code>alias>keyword + project match", "SubjectMatcher.cs · ProjectMatcher.cs", "EXISTING"],
    ["Confidence", "weighted evidence, per-kind caps, bands 95/70", "ConfidenceEngine.cs", "EXISTING"],
    ["AI (optional)", "Groq suggestions — advisory only, never mutates", "Intelligence.AI (empty)", "MISSING"],
    ["Decision", "Organize / Review(Smart Inbox) / Ignore / NoOp", "Application/Decisions/DecisionMaker.cs", "EXISTING"],
    ["Safety", "protected paths deny-by-default, pre-checks, conflicts", "Domain/Safety/SafetyEngine.cs", "EXISTING"],
    ["ActionPlan", "immutable items + undo recipes + fingerprints", "Domain/Operations/ActionPlanner.cs", "EXISTING"],
    ["Platform adapter", "real System.IO moves, KeepBoth suffixing, verify", "Platform.Windows/WindowsFileSystemAdapter.cs", "EXISTING"],
    ["Verify + History", "Planned→…→Committed persisted per item in SQLite", "TransactionEngine.cs · SqliteStore.cs", "EXISTING"],
    ["Undo", "fingerprint-checked reverse plan → RolledBack", "UndoService.cs · RollbackService.cs", "EXISTING"],
  ];
  const modules = [
    ["FileMind.Domain", "Contracts, engines, rules, safety, planning, transactions, undo. Zero I/O.", "77 unit tests", "EXISTING"],
    ["FileMind.Application", "Scanner, DecisionMaker, OrganizerPipeline, indexing, startup recovery, Smart Inbox, ModePolicy.", "wiring + E2E", "EXISTING"],
    ["FileMind.Infrastructure.Sqlite", "WAL SQLite, schema v2 + migrations, all repositories incl. IOperationStore + file_index hash.", "E2E-verified", "EXISTING"],
    ["FileMind.Platform.Windows", "Real FS adapter, path normalizer, protected paths, SHA256.", "E2E-verified", "EXISTING"],
    ["FileMind.Windows.Host", "Console verbs: scan/analyze/plan/execute/undo/history/smart-inbox/mode/rules/seed-demo.", "the app today", "EXISTING"],
    ["FileMind.Branding", "Canonical palette tokens as C# consts + parity tests.", "8 tests", "EXISTING"],
    ["FileMind.Intelligence.Local", "Local content extraction (PDF/DOCX/EXIF…).", "F-P5", "MISSING"],
    ["FileMind.Intelligence.AI", "Groq provider + privacy gate. Advisory only.", "F-P7", "MISSING"],
    ["Watchers/automation", "FileSystemWatcher, debounce, background jobs.", "F-P4", "MISSING"],
    ["Search (FTS5)", "No FTS index in schema yet — PLAN-015 adds it.", "F-P9", "MISSING"],
    ["Android", "Kotlin/Room/SAF backend.", "F-P8", "MISSING"],
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
