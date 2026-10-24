/* FileMind UI mockup — mock data.
   Every entity mirrors a real contract verbatim (camelCase = C# PascalCase).
   Sources: src/FileMind.Domain/Contracts/*.cs, src/FileMind.Application/Ports/Repositories.cs,
   src/FileMind.Windows.Host/Program.cs (seed-demo values), tests/fixtures/manifest.json.
   Keep in sync with code per docs/rules/MOCKUP_RULES.md. */
"use strict";

const FM_DATA = {};

/* mirrors src/FileMind.Domain/Contracts/Enums.cs — names kept verbatim */
FM_DATA.enums = {
  OperationType: ["Move", "Copy", "Rename", "CreateFolder", "Tag", "Ignore", "Notify", "Ask"],
  ConflictPolicy: ["KeepBoth", "Skip", "Ask", "ReplaceWithConfirmation"],
  OperationStatus: ["Planned", "Validating", "Executing", "Verifying", "Committed", "Failed", "RollingBack", "RolledBack", "NeedsAttention"],
  ConfidenceBand: ["High", "Medium", "Low"],
  ClassifierSource: ["Local", "Rule", "Ai"],
  SourceScope: ["Downloads", "Documents", "Desktop", "Media", "WatchedFolder", "ExternalDrive", "Other"],
  HashState: ["NotComputed", "Partial", "Full"],
  FileKind: ["Document", "Image", "Video", "Audio", "Archive", "Code", "Spreadsheet", "Presentation", "Executable", "Font", "Other"],
  DecisionKind: ["Organize", "Review", "Ignore", "NoOp"],
  /* mirrors src/FileMind.Domain/Contracts/Rule.cs */
  RuleActionKind: ["Move", "Copy", "Rename", "Tag", "Ignore", "Notify", "Ask", "CreateFolder", "RouteToCollection"],
  RuleProvenance: ["Builtin", "User", "Imported", "AiSuggested"],
};

/* mirrors FileKindMap in src/FileMind.Domain/Classification/ClassifierEngine.cs */
FM_DATA.kindMap = {
  Document: ["pdf", "doc", "docx", "txt", "rtf", "md", "odt"],
  Spreadsheet: ["xls", "xlsx", "csv", "ods"],
  Presentation: ["ppt", "pptx", "odp", "key"],
  Image: ["jpg", "jpeg", "png", "gif", "bmp", "webp", "tiff", "heic", "svg"],
  Video: ["mp4", "mkv", "avi", "mov", "wmv", "webm", "m4v"],
  Audio: ["mp3", "wav", "flac", "aac", "ogg", "m4a", "wma"],
  Archive: ["zip", "rar", "7z", "tar", "gz", "bz2"],
  Code: ["cs", "sln", "csproj", "js", "ts", "py", "java", "cpp", "c", "h", "rs", "go", "php", "html", "css", "json", "xml", "yml", "yaml", "sql", "sh", "ps1"],
  Executable: ["exe", "msi", "dll", "bat", "cmd", "com"],
  Font: ["ttf", "otf", "woff", "woff2"],
};
/* mirrors src/FileMind.Application/Scanning/MimeMap.cs */
FM_DATA.mimeMap = {
  pdf: "application/pdf", txt: "text/plain", md: "text/markdown", rtf: "application/rtf",
  doc: "application/msword", docx: "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  xls: "application/vnd.ms-excel", xlsx: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  csv: "text/csv", ppt: "application/ms-powerpoint",
  pptx: "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  jpg: "image/jpeg", jpeg: "image/jpeg", png: "image/png", gif: "image/gif", bmp: "image/bmp",
  webp: "image/webp", svg: "image/svg+xml", mp4: "video/mp4", mkv: "video/x-matroska",
  avi: "video/x-msvideo", mov: "video/quicktime", mp3: "audio/mpeg", wav: "audio/wav",
  flac: "audio/flac", zip: "application/zip", rar: "application/vnd.rar",
  "7z": "application/x-7z-compressed", json: "application/json", xml: "application/xml",
  html: "text/html", css: "text/css", cs: "text/plain", exe: "application/vnd.microsoft.portable-executable",
};

/* mock folder roots (fictional user) */
FM_DATA.roots = {
  downloads: "C:\\Users\\Demo\\Downloads",
  documents: "C:\\Users\\Demo\\Documents",
  desktop: "C:\\Users\\Demo\\Desktop",
  protected: "C:\\Windows\\System32\\drivers",
};
/* mirrors WindowsProtectedPaths (src/FileMind.Platform.Windows/WindowsProtectedPaths.cs) */
FM_DATA.protectedPrefixes = ["c:\\windows", "c:\\program files", "c:\\program files (x86)", "c:\\programdata", "c:\\$recycle.bin"];
/* dest paths that already exist — drives the KeepBoth conflict demo (ConflictPolicy.KeepBoth) */
FM_DATA.existingPaths = [
  "c:\\users\\demo\\downloads\\organized\\school\\it421\\it421_lecture_5.mp4",
];
/* user exclusion patterns from IExclusionRepository (defaults *.tmp/*.part/*.crdownload/desktop.ini/thumbs.db live in the scanner) */
FM_DATA.exclusions = ["node_modules", "\\bin\\"];

const DL = FM_DATA.roots.downloads, DOC = FM_DATA.roots.documents, DT = FM_DATA.roots.desktop;
let i = 0;
const fc = (dir, name, ext, size, scope, modifiedDaysAgo, hints = {}) => ({
  /* mirrors FileCandidate record */
  scanId: `scan-${(++i).toString(16).padStart(6, "0")}`,
  sourceUri: `${dir}\\${name}`,
  displayName: name,
  extension: ext,
  mimeType: FM_DATA.mimeMap[ext.replace(/^\./, "")] ?? null,
  sizeBytes: size,
  createdAt: new Date(Date.now() - (modifiedDaysAgo + 30) * 864e5).toISOString(),
  modifiedAt: new Date(Date.now() - modifiedDaysAgo * 864e5).toISOString(),
  sourceScope: scope,
  metadata: {},
  extractedTextRef: null,
  hashState: "NotComputed",
  capabilities: { canRead: true, canWrite: true, canMove: true, canCopy: true, canRename: true, canCreateFolder: true },
  platformHints: hints,
});
FM_DATA.fileCandidates = [
  fc(DL, "IT421_Lecture_5.mp4", ".mp4", 512_483_328, "Downloads", 1),
  fc(DL, "IT-421 lab slides.pptx", ".pptx", 18_932_112, "Downloads", 2),
  fc(DL, "IT 421 quiz notes.docx", ".docx", 84_210, "Downloads", 2),
  fc(DL, "it-421 summary.txt", ".txt", 12_480, "Downloads", 3),
  fc(DL, "alpha-rollout-plan.docx", ".docx", 214_016, "Downloads", 4),
  fc(DL, "alpha meeting notes.txt", ".txt", 9_220, "Downloads", 5),
  fc(DL, "invoice_2026-03.pdf", ".pdf", 188_416, "Downloads", 9),
  fc(DL, "IMG_2042.jpg", ".jpg", 4_218_880, "Downloads", 12),
  fc(DL, "holliday video.mp4", ".mp4", 918_552_576, "Downloads", 20),
  fc(DL, "holliday video (1).mp4", ".mp4", 918_552_576, "Downloads", 20),
  fc(DL, "notes.txt.bak", ".bak", 4_096, "Downloads", 21),
  fc(DL, "setup_x64.exe", ".exe", 96_468_992, "Downloads", 30),
  fc(DL, "big_project_render.mov", ".mov", 4_764_729_344, "Downloads", 33),
  fc(DL, "quarterly numbers.xlsx", ".xlsx", 1_204_224, "Downloads", 41),
  fc(DL, "video.crdownload", ".crdownload", 61_587_456, "Downloads", 0),
  fc(DL, "photos.part", ".part", 142_606_336, "Downloads", 0),
  fc(DOC, "resume_2026.docx", ".docx", 61_440, "Documents", 15),
  fc(DOC, "IT421 coursework draft.docx", ".docx", 305_152, "Documents", 7),
  fc(DOC, "family budget.xlsx", ".xlsx", 77_824, "Documents", 60),
  fc(DT, "screenshot 2026-10-20.png", ".png", 1_887_232, "Desktop", 4),
  fc(DT, "alpha roadmap.pdf", ".pdf", 540_672, "Desktop", 6),
];

/* mirrors SubjectEntry — values identical to `filemind seed-demo` in Program.cs */
FM_DATA.subjects = [
  { id: "IT421", name: "IT421 Networks", code: "IT421", aliases: ["IT 421", "IT-421", "IT_421"], keywords: ["networks", "lecture"] },
];
/* mirrors ProjectEntry — seed-demo values */
FM_DATA.projects = [
  { id: "alpha", name: "Alpha Rollout", clientName: "Contoso", aliases: ["alpha"], keywords: ["rollout"] },
];
/* mirrors CollectionEntry — seed-demo values (pathTemplate = destinationTemplate) */
FM_DATA.collections = [
  { id: "school", name: "School", destinationTemplate: "School/{Subject}", subjectIds: ["IT421"], projectIds: [] },
  { id: "work", name: "Work", destinationTemplate: "Work/{Project}", subjectIds: [], projectIds: ["alpha"] },
];

/* mirrors Rule record (schemaVersion per schemas/rules/rule.schema.json) */
const cond = (field, operator, value) => ({ field, operator, value });
FM_DATA.rules = [
  {
    id: "r-partial-downloads", name: "Skip backup leftovers", enabled: true, priority: 200,
    conditionRoot: { operator: "And", conditions: [cond("Extension", "In", "bak|old")], groups: [] },
    actions: [{ kind: "Ignore", destinationTemplate: null, namingTemplate: null, collection: null, tag: null, conflictPolicy: "KeepBoth" }],
    stopProcessing: true, mode: "all", createdAt: "2026-10-10T09:00:00Z", updatedAt: "2026-10-10T09:00:00Z",
    schemaVersion: 1, provenance: "Builtin",
  },
  {
    id: "r-alpha-docs", name: "Alpha docs to Work", enabled: true, priority: 100,
    conditionRoot: { operator: "And", conditions: [cond("Filename", "Contains", "alpha"), cond("Extension", "In", "docx|pdf")], groups: [] },
    actions: [{ kind: "RouteToCollection", destinationTemplate: null, namingTemplate: null, collection: "work", tag: null, conflictPolicy: "KeepBoth" }],
    stopProcessing: false, mode: "all", createdAt: "2026-10-12T14:30:00Z", updatedAt: "2026-10-18T11:05:00Z",
    schemaVersion: 1, provenance: "User",
  },
  {
    id: "r-course-videos", name: "Course videos to Lectures", enabled: true, priority: 50,
    conditionRoot: { operator: "And", conditions: [cond("Filename", "Contains", "lecture")], groups: [] },
    actions: [{ kind: "Move", destinationTemplate: "School/{Subject}/Lectures", namingTemplate: null, collection: null, tag: null, conflictPolicy: "KeepBoth" }],
    stopProcessing: false, mode: "all", createdAt: "2026-10-15T08:12:00Z", updatedAt: "2026-10-15T08:12:00Z",
    schemaVersion: 1, provenance: "Imported",
  },
];

/* mirrors OperationRecord / OperationItemState */
const item = (id, op, from, to, status, error = null) => ({
  itemId: id, operation: op, originalLocation: from, resultingLocation: to, status, verification: status === "Committed" ? "verified" : null, error,
});
FM_DATA.operations = [
  {
    operationId: "9f3c1a2b7d4e4f01", planId: "5ab81cd02e9f4a77", status: "NeedsAttention",
    createdAt: new Date(Date.now() - 2 * 864e5).toISOString(), committedAt: null, undoOfOperationId: null, error: null,
    items: [
      item("i1", "Move", `${DL}\\IT421_Lecture_4.mp4`, `${DL}\\organized\\School\\IT421\\IT421_Lecture_4.mp4`, "Committed"),
      item("i2", "Move", `${DL}\\week6.pdf`, `${DL}\\organized\\Files\\Document\\week6.pdf`, "Committed"),
      item("i3", "Move", `${DL}\\backup.mov`, null, "Failed", { code: "Locked", message: "The file is in use by another process.", retrySafe: true, needsReview: true }),
    ],
  },
  {
    operationId: "2e7d9c41a3b84e55", planId: "c14f6a2b88d34e99", status: "Committed",
    createdAt: new Date(Date.now() - 5 * 864e5).toISOString(), committedAt: new Date(Date.now() - 5 * 864e5 + 42e3).toISOString(), undoOfOperationId: null, error: null,
    items: [
      item("i4", "Move", `${DL}\\IT421_Lecture_3.mp4`, `${DL}\\organized\\School\\IT421\\IT421_Lecture_3.mp4`, "Committed"),
      item("i5", "Move", `${DL}\\IT 421 quiz.docx`, `${DL}\\organized\\School\\IT421\\IT 421 quiz.docx`, "Committed"),
      item("i6", "CreateFolder", `${DL}\\organized\\School\\IT421`, `${DL}\\organized\\School\\IT421`, "Committed"),
      item("i7", "Move", `${DL}\\alpha brief.docx`, `${DL}\\organized\\Work\\Alpha Rollout\\alpha brief.docx`, "Committed"),
      item("i8", "Move", `${DL}\\formulas.xlsx`, `${DL}\\organized\\Files\\Spreadsheet\\formulas.xlsx`, "Committed"),
      item("i9", "Move", `${DL}\\clip.mp4`, `${DL}\\organized\\Files\\Video\\clip.mp4`, "Committed"),
      item("i10", "Move", `${DL}\\notes.md`, `${DL}\\organized\\Files\\Document\\notes.md`, "Committed"),
      item("i11", "Move", `${DL}\\old-scan.jpg`, `${DL}\\organized\\Files\\Image\\old-scan.jpg`, "Committed"),
      item("i12", "Move", `${DL}\\pack.zip`, `${DL}\\organized\\Files\\Archive\\pack.zip`, "Committed"),
      item("i13", "Move", `${DL}\\track.mp3`, `${DL}\\organized\\Files\\Audio\\track.mp3`, "Committed"),
      item("i14", "Move", `${DL}\\slides.pptx`, `${DL}\\organized\\Files\\Presentation\\slides.pptx`, "Committed"),
      item("i15", "Move", `${DL}\\readme.txt`, `${DL}\\organized\\Files\\Document\\readme.txt`, "Committed"),
    ],
  },
];

/* mirrors Smart Inbox repository entries (Review decisions pending a human call) */
FM_DATA.smartInbox = [
  {
    id: "si-1", fileName: "alpha meeting notes.txt", sourceUri: `${DL}\\alpha meeting notes.txt`,
    reasons: ["Confidence 73 requires Smart Inbox review."],
    summary: "Document · project alias 'alpha' · band Medium", status: "Pending",
    receivedAt: new Date(Date.now() - 1 * 864e5).toISOString(),
  },
  {
    id: "si-2", fileName: "alpha roadmap.pdf", sourceUri: `${DT}\\alpha roadmap.pdf`,
    reasons: ["Confidence 73 requires Smart Inbox review."],
    summary: "Document · project alias 'alpha' · band Medium", status: "Pending",
    receivedAt: new Date(Date.now() - 2 * 864e5).toISOString(),
  },
  {
    id: "si-3", fileName: "it-421 summary.txt", sourceUri: `${DL}\\it-421 summary.txt`,
    reasons: ["Confidence 83 requires Smart Inbox review."],
    summary: "Document · subject alias 'IT-421' · band Medium", status: "Pending",
    receivedAt: new Date(Date.now() - 3 * 864e5).toISOString(),
  },
];

/* devices — mirrors platform capabilities + FEATURES.md status (Android = F-P8 MISSING) */
FM_DATA.devices = [
  {
    id: "this-pc", name: "This PC", kind: "windows", connected: true, icon: "devices",
    storage: { used: 327_155_712_000, total: 510_027_366_400 },
    capabilities: ["Full file access", "Long paths", "Hardlinks", "Watch folders (planned)"],
    restrictions: [], status: "connected",
  },
  {
    id: "pixel-8", name: "Pixel 8", kind: "android", connected: false, icon: "phone",
    storage: { used: 96_605_470_720, total: 128_849_018_880 },
    capabilities: ["MediaStore", "SAF folders"], restrictions: ["Android backend not implemented (F-P8)"],
    status: "disconnected", reconnect: "Connect the phone with a USB cable or enable wireless pairing, then refresh.",
  },
  {
    id: "usb-e", name: "USB Drive (E:)", kind: "removable", connected: true, icon: "usb",
    storage: { used: 41_943_040_000, total: 125_829_120_000 },
    capabilities: ["Move/Copy", "External drive scope"], restrictions: ["Unplug safely before removing"],
    status: "connected",
  },
];

FM_DATA.storage = {
  drive: "C:", used: 327_155_712_000, total: 510_027_366_400,
  byKind: [
    ["Videos", 142_000_000_000], ["Images", 61_000_000_000], ["Documents", 38_000_000_000],
    ["Archives", 34_000_000_000], ["Audio", 22_000_000_000], ["Code", 12_000_000_000], ["Other", 18_155_712_000],
  ],
};

/* settings — mirrors persisted settings repository shape */
FM_DATA.settings = {
  theme: "system", density: "comfortable",
  conflictPolicy: "KeepBoth", organizationMode: "Smart",
  notifications: { operationDone: true, warnings: true, smartInbox: true },
  watchedFolders: [],
  dbPath: "%LOCALAPPDATA%\\FileMind\\filemind.db",
  schemaVersion: 1,
};

FM_DATA.recentActivity = [
  { t: new Date(Date.now() - 2 * 864e5).toISOString(), text: "Organize Downloads — 2 files moved, 1 failed (in use)", opId: "9f3c1a2b7d4e4f01" },
  { t: new Date(Date.now() - 5 * 864e5).toISOString(), text: "Organize Downloads — 12 files moved", opId: "2e7d9c41a3b84e55" },
  { t: new Date(Date.now() - 6 * 864e5).toISOString(), text: "Rule 'Alpha docs to Work' updated", opId: null },
];
