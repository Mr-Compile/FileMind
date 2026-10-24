# Phase 3 — Windows Automation

Features: Watch Folders, FileSystemWatcher integration, event queue, debounce/coalescing, file stability/download completion checks, retries/backoff, job cancellation, startup/background host contracts, notifications, removable-drive events, and custom watched folders.

Routing:
`FS Event -> Normalize Event -> Queue -> Debounce -> Stability Check -> Scan/Enrich -> Classify -> Rules -> Plan -> Safety -> mode policy -> Execute or Smart Inbox`.

Never move `.crdownload`, `.part`, temporary/incomplete transfers, or actively changing files. Watcher events are hints; re-stat the filesystem before acting.
