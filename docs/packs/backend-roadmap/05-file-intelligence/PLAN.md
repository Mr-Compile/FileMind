# Phase 5 — Local File Intelligence

Use plugin-style analyzers. Core interface returns normalized metadata/content evidence; parsers remain replaceable.

Initial analyzers: PDF text, DOCX text/properties, PPTX text/properties, TXT/RTF where practical, spreadsheet metadata/limited text, EXIF/image metadata, audio tags, video metadata, archive manifest metadata, code-file signals.

Add smart rename/naming templates only after extracted evidence is reliable. Content extraction is local by default and bounded by file size/time limits. Corrupt/unsupported files must fail safely without blocking the scan.
