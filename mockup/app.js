/* FileMind UI mockup — core: icons, theme, router, store, mock engine, UI helpers.
   Mirrors behavior of src/FileMind.* — see mock-data.js header + docs/rules/MOCKUP_RULES.md */
"use strict";

const FM = (window.FM = {
  screens: {},
  state: {},
  store: null,
  bus: Object.assign(document.createElement("i"), {
    emit(ev) { this.dispatchEvent(new Event(ev)); },
    on(ev, fn) { this.addEventListener(ev, fn); },
  }),
});

/* ---------------- icons (semantic names per foundations/05) ---------------- */
const P = (d, extra = "") =>
  `<svg class="ic" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${d}${extra}</svg>`;
FM.icons = {
  dashboard: P('<rect x="3" y="3" width="6" height="6" rx="1.5"/><rect x="11" y="3" width="6" height="4" rx="1.5"/><rect x="11" y="9" width="6" height="8" rx="1.5"/><rect x="3" y="11" width="6" height="6" rx="1.5"/>'),
  files: P('<path d="M3 5.5A1.5 1.5 0 0 1 4.5 4h3l1.5 2h6.5A1.5 1.5 0 0 1 17 7.5v7A1.5 1.5 0 0 1 15.5 16h-11A1.5 1.5 0 0 1 3 14.5z"/>'),
  organize: P('<path d="M10 3l1.2 3.2L14.5 7.5l-3.3 1.3L10 12l-1.2-3.2L5.5 7.5l3.3-1.3z"/><path d="M15 12l.8 2 2 .8-2 .8-.8 2-.8-2-2-.8 2-.8z"/>'),
  tools: P('<path d="M12.5 4.5a3.5 3.5 0 0 0-4.7 4.3L4 12.6V16h3.4l3.8-3.8a3.5 3.5 0 0 0 4.3-4.7l-2.4 2.4-2-2z"/>'),
  search: P('<circle cx="9" cy="9" r="5.5"/><path d="M13.5 13.5 17 17"/>'),
  devices: P('<rect x="3" y="4" width="14" height="9" rx="1.5"/><path d="M8 17h4M10 13v4"/>'),
  phone: P('<rect x="6" y="3" width="8" height="14" rx="2"/><path d="M9 15h2"/>'),
  usb: P('<rect x="6" y="8" width="8" height="9" rx="1.5"/><path d="M8 8V5h4v3"/><path d="M9 5.5h.01M11 5.5h.01"/>'),
  activity: P('<path d="M3 10h3l2-5 3 10 2-5h4"/>'),
  settings: P('<circle cx="10" cy="10" r="2.5"/><path d="M10 3v2M10 15v2M3 10h2M15 10h2M5 5l1.4 1.4M13.6 13.6 15 15M15 5l-1.4 1.4M6.4 13.6 5 15"/>'),
  console: P('<rect x="3" y="4" width="14" height="12" rx="1.5"/><path d="M6.5 8l2.5 2-2.5 2M11 12.5h3"/>'),
  arch: P('<rect x="3" y="3" width="5" height="5" rx="1"/><rect x="12" y="3" width="5" height="5" rx="1"/><rect x="7.5" y="12" width="5" height="5" rx="1"/><path d="M8 5.5h4M10 8v4"/>'),
  folder: P('<path d="M3 5.5A1.5 1.5 0 0 1 4.5 4h3l1.5 2h6.5A1.5 1.5 0 0 1 17 7.5v7A1.5 1.5 0 0 1 15.5 16h-11A1.5 1.5 0 0 1 3 14.5z"/>'),
  file: P('<path d="M6 3h5l3 3v10.5A1.5 1.5 0 0 1 12.5 18h-6A1.5 1.5 0 0 1 5 16.5v-12A1.5 1.5 0 0 1 6 3z"/><path d="M11 3v3h3"/>'),
  move: P('<path d="M3 10h11M10 6l4 4-4 4"/><path d="M17 4v12"/>'),
  copy: P('<rect x="7" y="7" width="10" height="10" rx="1.5"/><path d="M13 7V5a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h2"/>'),
  rename: P('<path d="M4 16h12M6 16l4-11 3 8h2"/><path d="M7.5 11h4"/>'),
  trash: P('<path d="M4 6h12M8 6V4h4v2M6 6l.8 10h6.4L14 6"/><path d="M8.5 9v5M11.5 9v5"/>'),
  undo: P('<path d="M4 7h8a4 4 0 0 1 0 8H7"/><path d="M7 4 4 7l3 3"/>'),
  retry: P('<path d="M16 10a6 6 0 1 1-2-4.2"/><path d="M16 3v4h-4"/>'),
  play: P('<path d="M7 4.5v11l9-5.5z"/>'),
  pause: P('<path d="M7 4h2.5v12H7zM11.5 4H14v12h-2.5z"/>'),
  cancel: P('<circle cx="10" cy="10" r="6.5"/><path d="M7.5 7.5l5 5M12.5 7.5l-5 5"/>'),
  check: P('<path d="M4 10.5 8.5 15 16 5.5"/>'),
  info: P('<circle cx="10" cy="10" r="6.5"/><path d="M10 9v4M10 6.5h.01"/>'),
  warn: P('<path d="M10 3.5 17 16H3z"/><path d="M10 8v4M10 14h.01"/>'),
  error: P('<circle cx="10" cy="10" r="6.5"/><path d="M10 6v5M10 13.5h.01"/>'),
  offline: P('<path d="M4 4l12 12"/><path d="M7.5 7.6A6.5 6.5 0 0 1 15 12M10 15.5h.01"/>'),
  lock: P('<rect x="5" y="9" width="10" height="7" rx="1.5"/><path d="M7 9V7a3 3 0 0 1 6 0v2"/>'),
  shield: P('<path d="M10 3l6 2.5v4c0 4-2.7 6.5-6 7.5-3.3-1-6-3.5-6-7.5v-4z"/><path d="M7.5 10l2 2 3.5-3.5"/>'),
  clock: P('<circle cx="10" cy="10" r="6.5"/><path d="M10 6v4l2.5 2"/>'),
  db: P('<ellipse cx="10" cy="5" rx="6" ry="2.5"/><path d="M4 5v10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5V5"/><path d="M4 10c0 1.4 2.7 2.5 6 2.5s6-1.1 6-2.5"/>'),
  back: P('<path d="M11 5 6 10l5 5M6 10h11"/>'),
  up: P('<path d="M5 11l5-5 5 5M10 6v11"/>'),
  fwd: P('<path d="M9 5l5 5-5 5M3 10h11"/>'),
  dots: P('<circle cx="5" cy="10" r="1.3" fill="currentColor" stroke="none"/><circle cx="10" cy="10" r="1.3" fill="currentColor" stroke="none"/><circle cx="15" cy="10" r="1.3" fill="currentColor" stroke="none"/>'),
  open: P('<path d="M8 5H5a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2v-3"/><path d="M11 3h6v6M17 3 9.5 10.5"/>'),
  sun: P('<circle cx="10" cy="10" r="3.5"/><path d="M10 3v2M10 15v2M3 10h2M15 10h2M5 5l1.5 1.5M13.5 13.5 15 15M15 5l-1.5 1.5M6.5 13.5 5 15"/>'),
  moon: P('<path d="M15.5 12A5.5 5.5 0 0 1 8 4.5 5.5 5.5 0 1 0 15.5 12z"/>'),
  theme: P('<circle cx="10" cy="10" r="6.5"/><path d="M10 3.5a6.5 6.5 0 0 1 0 13z" fill="currentColor" stroke="none"/>'),
  density: P('<path d="M4 5h12M4 8h12M4 11h12M4 14h12"/>'),
  filter: P('<path d="M3 5h14l-5.5 6v4.5L8.5 17v-6z"/>'),
  sort: P('<path d="M7 4v12M7 16l-3-3M7 16l3-3M13 16V4M13 4l-3 3M13 4l3 3"/>'),
  close: P('<path d="M6 6l8 8M14 6l-8 8"/>'),
  plus: P('<path d="M10 4v12M4 10h12"/>'),
  link: P('<path d="M8.5 11.5a3 3 0 0 0 4.5.4l2-2a3 3 0 0 0-4.2-4.3l-1.2 1.1"/><path d="M11.5 8.5a3 3 0 0 0-4.5-.4l-2 2a3 3 0 0 0 4.2 4.3l1.2-1.1"/>'),
  chevR: P('<path d="M7 4l6 6-6 6"/>'),
  ai: P('<path d="M10 3l1.5 4.5L16 9l-4.5 1.5L10 15l-1.5-4.5L4 9l4.5-1.5z"/><path d="M15.5 13l.7 2 2 .7-2 .7-.7 2-.7-2 2-.7 2-.7z"/>'),
};
FM.icon = (n, cls = "") => {
  const s = FM.icons[n] || FM.icons.file;
  return cls ? s.replace('class="ic"', `class="ic ${cls}"`) : s;
};

/* ---------------- utils ---------------- */
FM.esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
FM.bytes = (n) => {
  if (n < 1024) return `${n} B`;
  const u = ["KB", "MB", "GB", "TB"];
  let v = n;
  for (const x of u) { v /= 1024; if (v < 1024) return `${v.toFixed(v >= 100 ? 0 : 1)} ${x}`; }
  return `${v.toFixed(1)} PB`;
};
FM.n0 = (n) => Math.round(n).toLocaleString("en-US");
FM.dt = (iso) => new Date(iso).toLocaleString("en-US", { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });
FM.utc = (d) => new Date(d).toISOString().replace("T", " ").slice(0, 19) + "Z";
FM.uid = (n = 12) => [...crypto.getRandomValues(new Uint8Array(n))].map((b) => b.toString(16).padStart(2, "0")).join("").slice(0, n);
FM.sleep = (ms, sig) => new Promise((res, rej) => {
  const t = setTimeout(res, ms);
  sig?.addEventListener("abort", () => { clearTimeout(t); rej(new DOMException("aborted", "AbortError")); }, { once: true });
});
FM.debounce = (fn, ms) => { let t; return (...a) => { clearTimeout(t); t = setTimeout(() => fn(...a), ms); }; };

/* ---------------- theme + density ---------------- */
FM.theme = {
  get: () => localStorage.getItem("fm-theme") || "system",
  resolved() {
    const m = this.get();
    return m === "system" ? (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light") : m;
  },
  apply() {
    const r = this.resolved();
    document.documentElement.dataset.theme = r === "dark" ? "dark" : "";
  },
  set(m) { localStorage.setItem("fm-theme", m); this.apply(); FM.bus.emit("theme"); },
  cycle() { this.set({ system: "light", light: "dark", dark: "system" }[this.get()]); },
};
matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => FM.theme.get() === "system" && FM.theme.apply());
FM.density = {
  get: () => localStorage.getItem("fm-density") === "compact",
  set(v) {
    v ? localStorage.setItem("fm-density", "compact") : localStorage.removeItem("fm-density");
    document.body.classList.toggle("compact", v);
  },
};

/* ---------------- store (runtime mock state) ---------------- */
/* Mirrors persistence via SqliteStore repositories — operations, smart inbox, settings. */
FM.initStore = () => {
  if (FM.store) return;
  const d = FM_DATA;
  FM.store = {
    ops: d.operations.map((o) => structuredClone(o)),
    inbox: d.smartInbox.map((i) => structuredClone(i)),
    settings: structuredClone(d.settings),
    files: d.fileCandidates.map((f) => structuredClone(f)),
    rules: d.rules.map((r) => structuredClone(r)),
    devices: d.devices.map((x) => structuredClone(x)),
    activity: d.recentActivity.map((a) => ({ ...a })),
    activeOp: null,
  };
};

/* ---------------- mock engine ----------------
   Mirrors the real pipeline semantics, not just the shapes:
   FileScanner/ExclusionMatcher -> ClassifierEngine (Normalizer + FileKindMap +
   SubjectMatcher 65/45/15 + ProjectMatcher 55/55/12 + ConfidenceEngine caps/bands)
   -> RuleEngine (priority, stop-processing, conflicts) -> DecisionMaker
   -> ActionPlanner (templates, Ask items, undo recipes, fingerprints)
   -> SafetyEngine -> TransactionEngine (staged per-item states). */
FM.engine = {
  /* mirrors src/FileMind.Domain/Classification/Normalizer.cs */
  tokens(s) {
    const seps = "-_. ,()[]{}";
    const t = (s || "").normalize("NFKC").split("")
      .map((c) => (seps.includes(c) || /\s/.test(c) ? " " : c.toLowerCase()))
      .join("").split(/\s+/).filter(Boolean);
    return { original: s || "", list: t, joined: t.join(" "), compact: t.join("") };
  },
  kindOf(ext) { /* mirrors FileKindMap.FromExtension */
    const e = ext.replace(/^\./, "").toLowerCase();
    for (const [k, exts] of Object.entries(FM_DATA.kindMap)) if (exts.includes(e)) return k;
    return "Other";
  },
  fingerprint(c) { /* mirrors FileIdentity.ComputeFingerprint (size:ticks, hex) */
    const ticks = (Date.parse(c.modifiedAt) + 62135596800000) * 10000;
    return `${c.sizeBytes.toString(16)}:${Math.round(ticks).toString(16)}`;
  },
  excluded(c) { /* mirrors ExclusionMatcher (defaults + IExclusionRepository patterns) */
    const pats = ["*.tmp", "*.part", "*.crdownload", "desktop.ini", "thumbs.db", ...FM_DATA.exclusions];
    const n = c.displayName.toLowerCase(), uri = c.sourceUri.toLowerCase();
    return pats.some((p) => (p.startsWith("*.") ? n.endsWith(p.slice(1)) : n === p || uri.includes(p.toLowerCase())));
  },

  evalCond(cond, c, cls) { /* mirrors src/FileMind.Domain/Rules/ConditionEvaluator.cs */
    const E = FM.engine;
    const field = {
      Filename: c.displayName, Extension: c.extension.replace(/^\./, ""), MimeType: c.mimeType || "",
      SourceFolder: c.sourceUri, SourceScope: c.sourceScope, Size: c.sizeBytes,
      CreatedAt: c.createdAt, ModifiedAt: c.modifiedAt,
      Subject: cls?.subject || "", Project: cls?.project || "", Keyword: c.displayName,
      Collection: cls?.collection || "", Category: cls?.category || "",
      ExtractedContent: "", Metadata: "",
    }[cond.field] ?? "";
    const v = cond.value;
    const f = typeof field === "number" ? field : E.tokens(String(field)).joined;
    const nv = E.tokens(v).joined;
    switch (cond.operator) {
      case "Equals": return f === nv;
      case "NotEquals": return f !== nv;
      case "Contains": return typeof f === "string" && (f.includes(nv) || E.tokens(String(field)).compact.includes(E.tokens(v).compact));
      case "NotContains": return !(typeof f === "string" && f.includes(nv));
      case "StartsWith": return typeof f === "string" && f.startsWith(nv);
      case "EndsWith": return typeof f === "string" && f.endsWith(nv);
      case "Regex": try { return new RegExp(v, "i").test(String(field)); } catch { return false; }
      case "GreaterThan": return +f > +v;
      case "LessThan": return +f < +v;
      case "In": return v.split("|").map((x) => E.tokens(x).joined).includes(String(f));
      default: return false;
    }
  },
  evalGroup(g, c, cls) { /* mirrors ConditionGroup AND/OR nesting (depth <= 3) */
    const rs = [
      ...(g.conditions || []).map((x) => FM.engine.evalCond(x, c, cls)),
      ...(g.groups || []).map((x) => FM.engine.evalGroup(x, c, cls)),
    ];
    if (!rs.length) return false;
    return g.operator === "Or" ? rs.some(Boolean) : rs.every(Boolean);
  },
  evalRules(c, cls) { /* mirrors src/FileMind.Domain/Rules/RuleEngine.cs */
    const matched = [];
    const rules = [...FM.store.rules].filter((r) => r.enabled)
      .sort((a, b) => b.priority - a.priority || a.id.localeCompare(b.id));
    for (const r of rules) {
      if (FM.engine.evalGroup(r.conditionRoot, c, cls)) {
        matched.push(r);
        if (r.stopProcessing) break;
      }
    }
    /* conflict detection: two winning move-class actions targeting different destinations */
    const dests = matched.flatMap((r) => r.actions)
      .filter((a) => ["Move", "Rename", "RouteToCollection"].includes(a.kind))
      .map((a) => a.destinationTemplate || a.collection || "");
    const uniq = [...new Set(dests)];
    return {
      matched,
      conflicts: uniq.length > 1 ? [`Rule destination conflicts: ${uniq.join(" vs ")}`] : [],
      proposedActions: matched.flatMap((r) => r.actions),
      winningRuleId: matched[0]?.id ?? null,
    };
  },

  classify(c) { /* mirrors src/FileMind.Domain/Classification/ClassifierEngine.cs */
    const E = FM.engine, T = E.tokens(c.displayName), ev = [], warnings = [];
    const fileKind = E.kindOf(c.extension);
    if (fileKind !== "Other")
      ev.push({ kind: "extension-signal", detail: `Extension '${c.extension}' => ${fileKind}`, scoreContribution: 10, source: "Local" });
    if (c.mimeType)
      ev.push({ kind: "mime-signal", detail: `MIME '${c.mimeType}'`, scoreContribution: 8, source: "Local" });

    /* mirrors SubjectMatcher: code 65 > name/alias 45 > keyword 15; ambiguity -10 + warning */
    const sMatches = [];
    for (const s of FM_DATA.subjects) {
      const code = E.tokens(s.code || "").joined;
      if (code && (T.joined.includes(code) || T.compact.includes(code.replace(/ /g, "")))) { sMatches.push([s, 65, "subject-code"]); continue; }
      if ([s.name, ...s.aliases].some((a) => { const na = E.tokens(a).joined; return na && T.joined.includes(na); })) { sMatches.push([s, 45, "subject-alias"]); continue; }
      if (s.keywords.some((k) => { const nk = E.tokens(k).joined; return nk && T.joined.includes(nk); })) sMatches.push([s, 15, "subject-keyword"]);
    }
    let subject = null;
    if (sMatches.length) {
      const best = sMatches.sort((a, b) => b[1] - a[1] || a[0].id.localeCompare(b[0].id))[0];
      subject = best[0].id;
      ev.push({ kind: best[2], detail: `Matched '${best[0].name}'`, scoreContribution: best[1], source: "Local" });
      const amb = sMatches.filter((m) => m[0].id !== best[0].id && m[1] >= best[1]);
      if (amb.length) {
        ev.push({ kind: "ambiguity-penalty", detail: "Ambiguous subject match", scoreContribution: -10, source: "Local" });
        warnings.push(`Ambiguous subject match: ${amb.map((a) => a[0].name).join(", ")} also matched.`);
      }
    }
    /* inferred cluster fallback (F-P12): files sharing a course code group
       under School/{Subject}/... without a catalog entry */
    if (!subject && this._inferred?.size) {
      const code = this.codeIn(c.displayName, this._inferred);
      if (code) {
        subject = code;
        ev.push({ kind: "inferred-subject", detail: `Auto-grouped: ${this._inferred.get(code)} files share course code '${code}'.`, scoreContribution: 60, source: "Local" });
      }
    }
    /* mirrors ProjectMatcher: alias 55 / name 55 > keyword 12; ambiguity -10 + warning */
    const pMatches = [];
    for (const p of FM_DATA.projects) {
      if (p.aliases.some((a) => { const na = E.tokens(a).joined; return na && T.joined.includes(na); })) { pMatches.push([p, 55, "project-alias"]); continue; }
      const np = E.tokens(p.name).joined;
      if (np && T.joined.includes(np)) { pMatches.push([p, 55, "project-name"]); continue; }
      if (p.keywords.some((k) => { const nk = E.tokens(k).joined; return nk && T.joined.includes(nk); })) pMatches.push([p, 12, "project-keyword"]);
    }
    let project = null;
    if (pMatches.length) {
      const best = pMatches.sort((a, b) => b[1] - a[1] || a[0].id.localeCompare(b[0].id))[0];
      project = best[0].id;
      ev.push({ kind: best[2], detail: `Matched '${best[0].name}'`, scoreContribution: best[1], source: "Local" });
      const amb = pMatches.filter((m) => m[0].id !== best[0].id && m[1] >= best[1]);
      if (amb.length) {
        ev.push({ kind: "ambiguity-penalty", detail: "Ambiguous project match", scoreContribution: -10, source: "Local" });
        warnings.push(`Ambiguous project match: ${amb.map((a) => a[0].name).join(", ")} also matched.`);
      }
    }
    if (subject && fileKind !== "Other")
      ev.push({ kind: "context-over-type", detail: "Subject context present; context beats generic type routing.", scoreContribution: 20, source: "Local" });

    /* mirrors ConfidenceEngine: per-kind caps then clamp 0..100; bands >=95 High / 70-94 Medium / <70 Low */
    const caps = { "subject-keyword": 15, "project-keyword": 12, "extension-signal": 10, "mime-signal": 8, "content-signal": 25, "ai-signal": 30 };
    const byKind = {};
    for (const e of ev) byKind[e.kind] = (byKind[e.kind] || 0) + e.scoreContribution;
    const confidence = Math.max(0, Math.min(100,
      Object.entries(byKind).reduce((s, [k, v]) => s + (caps[k] != null ? Math.min(v, caps[k]) : v), 0)));
    const band = confidence >= 95 ? "High" : confidence >= 70 ? "Medium" : "Low";
    const parts = [`kind=${fileKind}`];
    if (subject) parts.push(`subject=${subject}`);
    if (project) parts.push(`project=${project}`);
    parts.push(`band=${band}`, `evidence=${ev.length}`);
    return {
      fileKind,
      collection: subject ? "school" : project ? "work" : null,
      category: null, subcategory: null, subject, project,
      evidence: ev, confidence, sources: ["Local"],
      explanation: parts.join("; "),
      warnings, needsReview: band === "Medium" || warnings.length > 0,
    };
  },

  /* mirrors SubjectClusterer.cs: LETTERS{2,5}[-_ ]?DIGITS{3,4}L? word-bounded;
     deny-list; clusters of >= 2 files get an ephemeral inferred subject id */
  COURSE_CODE: /(?<![A-Za-z0-9])([A-Za-z]{2,5})[-_ ]?([0-9]{3,4}[A-Za-z]?)(?![A-Za-z0-9])/gi,
  CODE_DENY: new Set(["img", "pxl", "vid", "dsc", "dscn", "screenshot", "screen", "photo",
    "wa", "wp", "mmexport", "received", "download", "doc", "file",
    "copy", "untitled", "final", "scan", "snap", "new", "temp", "mov"]),

  detectClusters(names) {
    const groups = new Map();
    for (const name of names) {
      for (const m of name.matchAll(this.COURSE_CODE)) {
        if (this.CODE_DENY.has(m[1].toLowerCase())) continue;
        const k = `${m[1]}-${m[2]}`.toUpperCase();
        if (!groups.has(k)) groups.set(k, new Set());
        groups.get(k).add(name);
      }
    }
    return new Map([...groups].filter(([, v]) => v.size >= 2).map(([k, v]) => [k, v.size]));
  },

  codeIn(name, inferred) {
    for (const m of name.matchAll(this.COURSE_CODE)) {
      if (this.CODE_DENY.has(m[1].toLowerCase())) continue;
      const k = `${m[1]}-${m[2]}`.toUpperCase();
      if (inferred.has(k)) return k;
    }
    return null;
  },

  analyze(candidates) { /* mirrors src/FileMind.Application/Decisions/DecisionMaker.cs */
    this._inferred = this.detectClusters(candidates.map((c) => c.displayName));
    return candidates.map((c) => {
      const excluded = FM.engine.excluded(c);
      const classification = FM.engine.classify(c);
      const rules = excluded
        ? { matched: [], conflicts: [], proposedActions: [], winningRuleId: null }
        : FM.engine.evalRules(c, classification);
      const acts = rules.proposedActions;
      const org = acts.find((a) => ["Move", "RouteToCollection", "Rename", "Copy"].includes(a.kind));
      let decision;
      if (excluded) {
        decision = { kind: "Ignore", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: ["Excluded by scanner pattern."] };
      } else if (rules.conflicts.length) {
        decision = { kind: "Review", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: rules.conflicts };
      } else if (acts.some((a) => a.kind === "Ignore")) {
        decision = { kind: "Ignore", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: ["Matched an ignore rule."] };
      } else if (acts.some((a) => a.kind === "Ask")) {
        decision = { kind: "Review", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: ["Rule requires user confirmation."] };
      } else if (org) {
        const coll = FM_DATA.collections.find((x) => x.id === org.collection || x.name === org.collection);
        decision = {
          kind: "Organize",
          destinationTemplate: org.destinationTemplate || coll?.destinationTemplate
            || (classification.subject ? "School/{Subject}/{FileKind}" : classification.project ? "Work/{Project}/{FileKind}" : "Files/{FileKind}"),
          namingTemplate: org.namingTemplate || null,
          conflictPolicy: org.conflictPolicy || "KeepBoth",
          reasons: [`Rule '${rules.winningRuleId}' matched.`],
        };
      } else {
        const band = classification.confidence >= 95 ? "High" : classification.confidence >= 70 ? "Medium" : "Low";
        if (band === "Low") {
          decision = { kind: "NoOp", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: [`Confidence ${classification.confidence} below Medium threshold; left untouched.`] };
        } else if (band === "Medium" || classification.needsReview) {
          decision = { kind: "Review", destinationTemplate: null, namingTemplate: null, conflictPolicy: "KeepBoth", reasons: [`Confidence ${classification.confidence} requires Smart Inbox review.`] };
        } else {
          decision = {
            kind: "Organize",
            destinationTemplate: classification.subject ? "School/{Subject}/{FileKind}" : classification.project ? "Work/{Project}/{FileKind}" : "Files/{FileKind}",
            namingTemplate: null, conflictPolicy: "KeepBoth",
            reasons: [`High-confidence classification (${classification.confidence}).`],
          };
        }
      }
      return { candidate: c, classification, rules, decision };
    });
  },

  resolveDest(a, root, relDir = "", preserveTree = false) { /* mirrors ActionPlanner.ResolveTemplate + Combine + DestinationBase */
    const cls = a.classification, c = a.candidate, now = new Date();
    const stem = c.displayName.replace(/\.[^.]+$/, "");
    const rel = (a.decision.destinationTemplate || "{Collection}/{FileKind}")
      .replace("{Collection}", cls.collection ?? "Unsorted")
      .replace("{Category}", cls.category ?? "General")
      .replace("{Subcategory}", cls.subcategory ?? "General")
      .replace("{Subject}", cls.subject ?? "Unsorted")
      .replace("{Project}", cls.project ?? "General")
      .replace("{FileKind}", cls.fileKind)
      .replace("{Name}", stem).replace("{Ext}", c.extension.replace(/^\./, ""))
      .replace("{YYYY}", String(now.getFullYear()))
      .replace("{MM}", String(now.getMonth() + 1).padStart(2, "0"))
      .replace("{DD}", String(now.getDate()).padStart(2, "0"))
      .replace("{RelativeDir}", relDir);
    const name = a.decision.namingTemplate
      ? a.decision.namingTemplate.replace("{Name}", stem).replace("{Ext}", c.extension.replace(/^\./, ""))
      : c.displayName;
    /* category-then-tree: relDir appended inside the resolved template only when preserve-tree is on */
    return `${root}\\${rel}${preserveTree && relDir ? "\\" + relDir : ""}\\${name}`;
  },

  /* IPathNormalizer.RelativeTo: "" for root-equal, null when not under */
  relativeTo(path, root) {
    const p = path.replaceAll("/", "\\").replace(/\\+$/, "");
    const r = root.replaceAll("/", "\\").replace(/\\+$/, "");
    if (p.toLowerCase() === r.toLowerCase()) return "";
    return p.toLowerCase().startsWith(r.toLowerCase() + "\\") ? p.slice(r.length + 1) : null;
  },

  plan(analyzed, dryRun, destRoot, srcRoot = null, preserveTree = false) { /* mirrors src/FileMind.Domain/Operations/ActionPlanner.cs */
    const items = [], seen = new Set();
    let requiresReview = false;
    for (const a of analyzed) {
      if (a.decision.kind === "Review") {
        requiresReview = true;
        items.push({ /* Review decisions produce an Ask item — they never move files */
          itemId: FM.uid(8), operation: "Ask", source: a.candidate.sourceUri, destination: null,
          conflictPolicy: "Ask", reason: a.decision.reasons.join("; ") || "Low confidence or conflicts",
          preconditions: [], undoRecipe: null, _a: a,
        });
        continue;
      }
      if (a.decision.kind !== "Organize") continue;
      requiresReview = requiresReview || a.classification.needsReview;
      const dir = a.candidate.sourceUri.slice(0, a.candidate.sourceUri.lastIndexOf("\\"));
      const relDir = (srcRoot && FM.engine.relativeTo(dir, srcRoot)) || "";
      let dest = FM.engine.resolveDest(a, destRoot, relDir, preserveTree);
      let n = 2; /* mirrors ConflictPolicy.KeepBoth deterministic suffixing */
      const bi = dest.lastIndexOf(".");
      while (seen.has(dest.toLowerCase()) || FM.engine.destExists(dest))
        dest = `${dest.slice(0, bi)} (${n++})${dest.slice(bi)}`;
      seen.add(dest.toLowerCase());
      const fp = FM.engine.fingerprint(a.candidate);
      items.push({
        itemId: FM.uid(8), operation: "Move", source: a.candidate.sourceUri, destination: dest,
        conflictPolicy: a.decision.conflictPolicy || "KeepBoth",
        reason: a.decision.reasons.join("; ") || a.classification.explanation,
        preconditions: [`source-exists:${a.candidate.sourceUri}`, `fingerprint:${fp}`],
        undoRecipe: { reverseOperation: "Move", reverseSource: dest, reverseDestination: a.candidate.sourceUri, fingerprint: fp, preconditions: [`exists:${dest}`] },
        _a: a,
      });
    }
    return {
      planId: FM.uid(32), createdAt: new Date().toISOString(), sourceDecision: "OrganizerPipeline",
      items, requiresReview, dryRun, reversibleEstimate: true,
      skipped: analyzed.filter((a) => ["NoOp", "Ignore"].includes(a.decision.kind)),
    };
  },
  destExists: (p) => FM_DATA.existingPaths.includes(p.toLowerCase().replaceAll("/", "\\")),

  safety(analyzed, plan, sourceRoot) { /* mirrors src/FileMind.Domain/Safety/SafetyEngine.cs */
    const blockers = [], warnings = [];
    if (FM_DATA.protectedPrefixes.some((p) => sourceRoot.toLowerCase().startsWith(p)))
      blockers.push(`'${sourceRoot}' is a protected location. Protected/system/app paths are denied by default.`);
    if (!plan.items.length) blockers.push("Nothing to do — no file reached an Organize decision.");
    const big = plan.items.filter((i) => (i._a?.candidate.sizeBytes || 0) > 3e9);
    if (big.length) warnings.push(`${big.length} large file(s) (>3 GB) will take a while to move across drives.`);
    const locked = plan.items.filter((i) => i._a?.candidate.platformHints?.locked === "true");
    locked.forEach((i) => warnings.push(`'${i._a.candidate.displayName}' may be in use — it will be retried, then reported.`));
    const dupes = plan.items.filter((i) => / \(\d+\)\.[^.]+$/.test(i.destination || ""));
    if (dupes.length) warnings.push(`${dupes.length} destination name(s) already exist — Keep Both renames with (n).`);
    const asks = plan.items.filter((i) => i.operation === "Ask").length;
    if (asks) warnings.push(`${asks} file(s) need Smart Inbox review — they will not be moved.`);
    return { allowed: !blockers.length, blockers, warnings, requiredConfirmation: warnings.length ? "Review warnings before executing." : null };
  },

  async execute(plan, onStep, sig) { /* mirrors src/FileMind.Domain/Operations/TransactionEngine.cs */
    const rec = {
      operationId: FM.uid(32), planId: plan.planId, status: "Validating",
      createdAt: new Date().toISOString(), committedAt: null, undoOfOperationId: null, error: null,
      items: plan.items.filter((i) => i.operation === "Move").map((i) => ({
        itemId: i.itemId, operation: i.operation, originalLocation: i.source,
        resultingLocation: null, status: "Planned", verification: null, error: null,
      })),
    };
    onStep?.("Validating", rec);
    await FM.sleep(500, sig);
    rec.status = "Executing"; onStep?.("Executing", rec);
    for (const it of rec.items) {
      it.status = "Executing"; onStep?.("item", rec, it);
      await FM.sleep(380 + Math.random() * 300, sig);
      it.status = "Committed";
      it.resultingLocation = plan.items.find((p) => p.itemId === it.itemId).destination;
      it.verification = "verified"; onStep?.("item", rec, it);
    }
    rec.status = "Verifying"; onStep?.("Verifying", rec);
    await FM.sleep(400, sig);
    rec.status = "Committed"; rec.committedAt = new Date().toISOString();
    onStep?.("Committed", rec);
    return rec;
  },
};

/* ---------------- UI helpers ---------------- */
const STATUS_META = { /* canonical operation states — components/03 + Enums.cs OperationStatus */
  Ready: ["neutral", "info"], Queued: ["neutral", "clock"], Checking: ["info", "search"], Running: ["info", "play"],
  Paused: ["warning", "pause"], Completed: ["success", "check"], "Completed with warnings": ["warning", "warn"],
  Failed: ["error", "error"], Cancelled: ["offline", "cancel"], Offline: ["offline", "offline"],
  Unsupported: ["neutral", "lock"], "Permission required": ["warning", "lock"],
  Planned: ["info", "clock"], Validating: ["info", "search"], Executing: ["info", "play"], Verifying: ["info", "shield"],
  Committed: ["success", "check"], RollingBack: ["warning", "undo"], RolledBack: ["offline", "undo"],
  NeedsAttention: ["warning", "warn"], Missing: ["offline", "lock"], Organize: ["info", "organize"],
  Review: ["warning", "warn"], Ignore: ["neutral", "cancel"], NoOp: ["neutral", "info"], Ask: ["warning", "info"],
  Move: ["info", "move"], Copy: ["info", "copy"], Rename: ["info", "rename"], CreateFolder: ["info", "folder"],
  High: ["success", "check"], Medium: ["warning", "warn"], Low: ["error", "info"],
};
FM.statusChip = (s, extra = "") => {
  const [tone, ic] = STATUS_META[s] || ["neutral", "info"];
  return `<span class="chip ${tone}"><span class="dot"></span>${FM.icon(ic, "sm")}${FM.esc(s)}${extra}</span>`;
};
FM.confBand = (v) => (v >= 95 ? "high" : v >= 70 ? "med" : "low");
FM.confBar = (v) =>
  `<span class="conf ${FM.confBand(v)}" title="Confidence ${v} — bands: High ≥95, Medium 70–94, Low <70">
    <span class="bar"><i style="width:${v}%"></i></span>${v}</span>`;
FM.fileIcon = (kind) => `<span class="fico ${FM.esc(kind)}" aria-hidden="true">${FM.esc(kind.slice(0, 2))}</span>`;
FM.meter = (pct, label) =>
  `<div class="meter" role="meter" aria-valuenow="${pct}" aria-valuemin="0" aria-valuemax="100" aria-label="${FM.esc(label || "storage")}"><i style="width:${pct}%"></i></div>`;
FM.empty = (icon, title, sub, action = "") =>
  `<div class="empty">${FM.icon(icon, "ic-lg")}<div class="t">${FM.esc(title)}</div><div>${sub}</div>${action ? `<div style="margin-top:var(--sp-4)">${action}</div>` : ""}</div>`;
FM.banner = (kind, icon, html) =>
  `<div class="banner ${kind}">${FM.icon(icon)}<div class="grow">${html}</div></div>`;
FM.crumbs = (parts) =>
  `<nav class="crumbs" aria-label="Location">${parts.map((p, i) =>
    (i ? FM.icon("chevR", "sm") : "") + (p.href ? `<a href="${p.href}">${FM.esc(p.t)}</a>` : `<span>${FM.esc(p.t)}</span>`)
  ).join("")}</nav>`;

FM.toast = (msg, kind = "info", ms = 3600) => {
  const t = document.createElement("div");
  t.className = `toast ${kind}`;
  t.innerHTML = `${FM.icon(kind === "success" ? "check" : kind === "error" ? "error" : "info")}<span>${msg}</span>`;
  document.getElementById("toast-root").append(t);
  setTimeout(() => t.remove(), ms);
};
FM.dialog = (html, { onClose } = {}) => {
  const ov = document.createElement("div");
  ov.className = "overlay";
  ov.innerHTML = `<div class="dialog" role="dialog" aria-modal="true">${html}</div>`;
  const close = () => { ov.remove(); document.removeEventListener("keydown", esc); onClose?.(); };
  const esc = (e) => e.key === "Escape" && close();
  ov.addEventListener("click", (e) => e.target === ov && close());
  document.addEventListener("keydown", esc);
  document.getElementById("dialog-root").append(ov);
  ov.querySelector(".dialog")?.querySelector("button,input,select,[tabindex]")?.focus();
  return { el: ov, close };
};
FM.confirm = ({ title, body, ok = "Continue", kind = "primary", cancel = "Cancel" }) =>
  new Promise((res) => {
    const d = FM.dialog(`
      <h2>${FM.icon(kind === "destructive" ? "warn" : "info")} ${FM.esc(title)}</h2>
      <div class="tiny" style="color:var(--fm-text-secondary)">${body}</div>
      <div class="foot">
        <button class="btn" data-x="0">${FM.esc(cancel)}</button>
        <button class="btn ${kind}" data-x="1">${FM.esc(ok)}</button>
      </div>`);
    d.el.querySelectorAll("[data-x]").forEach((b) => (b.onclick = () => { d.close(); res(b.dataset.x === "1"); }));
  });
/* Three-choice confirm (PLAN-028 leave-guard): resolves "ok" | "alt" | "cancel". */
FM.confirm3 = ({ title, body, ok = "Continue", alt, cancel = "Stay", kind = "primary" }) =>
  new Promise((res) => {
    const d = FM.dialog(`
      <h2>${FM.icon(kind === "destructive" ? "warn" : "info")} ${FM.esc(title)}</h2>
      <div class="tiny" style="color:var(--fm-text-secondary)">${body}</div>
      <div class="foot">
        <button class="btn" data-x="cancel">${FM.esc(cancel)}</button>
        ${alt ? `<button class="btn" data-x="alt">${FM.esc(alt)}</button>` : ""}
        <button class="btn ${kind}" data-x="ok">${FM.esc(ok)}</button>
      </div>`);
    d.el.querySelectorAll("[data-x]").forEach((b) => (b.onclick = () => { d.close(); res(b.dataset.x); }));
  });
FM.menu = (items, x, y) => {
  const m = document.createElement("div");
  m.className = "menu"; m.style.left = x + "px"; m.style.top = y + "px";
  m.innerHTML = items.map((i, n) =>
    i.sep ? '<div class="sep"></div>'
      : `<button data-i="${n}" ${i.disabled ? "disabled" : ""}>${FM.icon(i.icon || "file", "sm")}${FM.esc(i.label)}${i.note ? `<span class="muted tiny" style="margin-left:auto">${FM.esc(i.note)}</span>` : ""}</button>`
  ).join("");
  const kill = () => { m.remove(); document.removeEventListener("click", kill, true); };
  document.addEventListener("click", kill, true);
  m.querySelectorAll("[data-i]").forEach((b) => (b.onclick = (e) => { e.stopPropagation(); kill(); items[+b.dataset.i].run?.(); }));
  document.body.append(m);
  const r = m.getBoundingClientRect();
  if (r.right > innerWidth) m.style.left = innerWidth - r.width - 8 + "px";
  if (r.bottom > innerHeight) m.style.top = innerHeight - r.height - 8 + "px";
};

/* ---------------- router ---------------- */
FM.routes = [
  ["dashboard", "Dashboard", "dashboard"],
  ["files", "Files", "files"],
  ["organizer", "Organizer", "organize"],
  ["tools", "Tools", "tools"],
  ["search", "Search", "search"],
  ["devices", "Devices", "devices"],
  ["activity", "Activity", "activity"],
  ["settings", "Settings", "settings"],
  ["help", "Help & About", "info"],
];
FM.go = (r) => (location.hash = "#/" + r);
FM.route = () => {
  const [path, qs] = location.hash.replace(/^#\/?/, "").split("?");
  const params = Object.fromEntries(new URLSearchParams(qs || ""));
  /* deep-linkable theme/density: #/dashboard?theme=dark&density=compact */
  if (["system", "light", "dark"].includes(params.theme)) FM.theme.set(params.theme);
  if (params.density != null) FM.density.set(params.density === "compact");
  const name = path || "dashboard";
  const render = FM.screens[name] || FM.screens._missing;
  document.querySelectorAll(".nav-item").forEach((a) => a.classList.toggle("active", a.dataset.r === name));
  const main = document.getElementById("main");
  main.innerHTML = "";
  render(main, params);
  main.focus({ preventScroll: true });
  document.title = `FileMind — ${name[0].toUpperCase() + name.slice(1)}`;
};
FM.screens._missing = (m) => {
  m.innerHTML = `<div class="page-head"><div><h1>Page not found</h1><div class="sub">This screen doesn't exist in the mockup.</div></div></div>
  ${FM.empty("search", "No screen at this route", `The route you opened isn't part of the approved information architecture.`, `<a class="btn primary" href="#/dashboard">Go to Dashboard</a>`)}`;
};
FM.screen = (name, fn) => (FM.screens[name] = fn);

/* ---------------- shell boot ---------------- */
function buildNav() {
  const sb = document.getElementById("sidebar");
  sb.innerHTML =
    FM.routes.map(([r, t, ic]) => `<a class="nav-item" data-r="${r}" href="#/${r}">${FM.icon(ic)}<span class="lbl">${t}</span></a>`).join("") +
    `<div class="nav-sep">Backend</div>` +
    [["console", "Console", "console"], ["architecture", "Architecture", "arch"]]
      .map(([r, t, ic]) => `<a class="nav-item" data-r="${r}" href="#/${r}">${FM.icon(ic)}<span class="lbl">${t}</span></a>`).join("") +
    `<div style="margin-top:auto;padding:var(--sp-3);font-size:11px;color:var(--fm-text-secondary)">Mock data — no real files are touched.</div>`;
}
function updateOpBadge() {
  const b = document.getElementById("opcount");
  const n = (FM.store?.activeOp ? 1 : 0) + (FM.store?.inbox.filter((i) => i.status === "Pending").length || 0);
  b.hidden = !n; b.textContent = n;
}
document.addEventListener("DOMContentLoaded", () => {
  FM.initStore?.();
  FM.theme.apply();
  FM.density.set(FM.density.get());
  buildNav();
  document.querySelectorAll("[data-icon]").forEach((s) => (s.outerHTML = FM.icon(s.dataset.icon)));
  document.getElementById("theme-btn").onclick = () => {
    const cur = FM.theme.get();
    const menu = ["system", "light", "dark"].map((m) => ({
      label: m[0].toUpperCase() + m.slice(1) + (m === cur ? " ✓" : ""),
      icon: m === "dark" ? "moon" : m === "light" ? "sun" : "devices",
      run: () => { FM.theme.set(m); FM.toast(`Theme: ${m}`, "success"); },
    }));
    const r = document.getElementById("theme-btn").getBoundingClientRect();
    FM.menu(menu, r.left, r.bottom + 6);
  };
  document.getElementById("density-btn").onclick = () => {
    const v = !FM.density.get(); FM.density.set(v);
    FM.toast(v ? "Compact density on" : "Comfortable density on", "info");
  };
  document.getElementById("opcenter-btn").onclick = () => FM.go("activity");
  document.getElementById("command-box").onclick = () => FM.go("search");
  addEventListener("keydown", (e) => {
    if (e.ctrlKey && e.key.toLowerCase() === "k") { e.preventDefault(); FM.go("search"); }
  });
  FM.bus.on("ops", updateOpBadge);
  updateOpBadge();
  addEventListener("hashchange", FM.route);
  /* Leave-guard parity (PLAN-028): exiting the organizer while a run executes
     asks Keep running / Cancel run / Stay. FM.store.activeOp exists only
     mid-run on both platforms' screens; the session object survives because
     organizer state lives outside the screen closure. */
  addEventListener("hashchange", (e) => {
    const op = FM.store?.activeOp;
    const org = FM.state?.org ?? FM.store?.orgSession;
    if (!op || !org) return;
    const from = (new URL(e.oldURL).hash || "").replace(/^#\/?/, "").split("?")[0];
    const to = location.hash.replace(/^#\/?/, "").split("?")[0] || "dashboard";
    if (from !== "organizer" || to === "organizer") return;
    FM.confirm3({
      title: "Run in progress",
      body: `${op.done} of ${op.total} item(s) done. The run can keep going in the background — the result lands in Activity.`,
      ok: "Keep running", alt: "Cancel run", cancel: "Stay",
    }).then((r) => {
      if (r === "alt") { org.abort?.abort(); return; }
      if (r === "cancel") location.hash = "#/organizer";
      // "ok": stay on the new route — the run completes in the background.
    });
  });
  FM.route();
  /* splash: brief brand display on boot, then fade (BRAND.md: simple/fast, no fake progress) */
  const splash = document.getElementById("splash");
  if (splash) setTimeout(() => { document.body.classList.add("splash-done"); setTimeout(() => splash.remove(), 300); }, 500);
});
