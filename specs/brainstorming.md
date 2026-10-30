# Brainstorming

Ideas and doubts before anything is decided. An idea becomes work only with the owner's yes; it is never deleted, only
its status changes.

## Ideas

| Id | Date | Idea | Status |
|---|---|---|---|
| IDEA-1 | 30/10 | Cross-platform: Windows and Linux, with an interface for each. (voice note 30/10) | proposed |
| IDEA-2 | 30/10 | Static, dictionary-driven translation at run time: translate what the dictionary knows, leave the rest untouched; no model while translating. (voice note 30/10) | proposed |
| IDEA-3 | 30/10 | Recorder mode for an admin: periodic screenshots of a real interaction, from which a local model proposes translations to add to the dictionary. (voice note 30/10) | proposed |
| IDEA-4 | 30/10 | The dictionary is a shared spreadsheet with four columns (application or window, or 'generic'; source label; translated value; language), kept in a synced shared location; the app imports it periodically into a local database (for example SQLite) and works offline. (voice note 30/10) | proposed |
| IDEA-5 | 30/10 | The import is an ETL with validation: a spreadsheet with discrepancies is rejected, never half-loaded. (voice note 30/10) | proposed |
| IDEA-6 | 30/10 | Window selection: the user picks the window to translate, not the whole screen. (voice note 30/10) | proposed |
| IDEA-7 | 30/10 | A minimal admin interface: choose the dictionary spreadsheet, choose and download the local model (for example from Hugging Face). (voice note 30/10) | proposed |
| IDEA-8 | 30/10 | Pluggable OCR: the native Windows engine or Tesseract (on Linux, and optionally on Windows). (voice note 30/10) | proposed |
| IDEA-9 | 30/10 | Clean architecture with design patterns: one solution, one project per concern (acquisition and OCR, overlay, translation, admin and recording, storage), so a new OCR, translator or interface does not touch the rest; C# on both systems, developed with VS Code. (voice note 30/10) | proposed |
| IDEA-10 | 30/10 | A test suite with sample targets: a console, a desktop app on Windows and on Linux, a shell with typed commands, a browser, a remote desktop session. (voice note 30/10) | proposed |
| IDEA-11 | 30/10 | An updater: checks the latest GitHub release and proposes the download, launcher-style. (voice note 30/10) | proposed |

## Open questions

- The README says Windows only, on purpose (native OCR); the voice note says Windows and Linux, with Tesseract as an option. Which one holds? (it changes Technologies and the stack ADR) (voice note 30/10)
- The README describes a local dictionary cascade (private, then generic, then verbatim); the voice note describes per-application and generic rows in a shared spreadsheet. Same idea, or does the spreadsheet replace the cascade? (voice note 30/10)
- Recorder mode takes screenshots of real screens: where are they kept, for how long, and how is sensitive content on them protected? (voice note 30/10)
- Admin model: local only, or a remote model as an option later? (voice note 30/10)
- Which UI framework runs on both Windows and Linux? (voice note 30/10)
