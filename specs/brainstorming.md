# Brainstorming

Ideas and doubts before anything is decided. An idea becomes work only with the owner's yes; it is never deleted, only
its status changes.

## Ideas

| Id | Date | Idea | Status |
|---|---|---|---|
| IDEA-1 | 30/10 | Cross-platform: Windows and Linux, with an interface for each. (voice note 30/10) | accepted 30/10: Windows first, Linux later |
| IDEA-2 | 30/10 | Static, dictionary-driven translation at run time: translate what the dictionary knows, leave the rest untouched; no model while translating. (voice note 30/10) | accepted 30/10 |
| IDEA-3 | 30/10 | Recorder mode for an admin: periodic screenshots of a real interaction, from which a local model proposes translations to add to the dictionary. (voice note 30/10) | accepted 30/10: screenshots never on disk, only the proposed text is kept; local model only |
| IDEA-4 | 30/10 | The dictionary is a shared spreadsheet with four columns (application or window, or 'generic'; source label; translated value; language), kept in a synced shared location; the app imports it periodically into a local database (for example SQLite) and works offline. (voice note 30/10) | accepted 30/10: SQLite (atomic import, one file, later the recorder's proposals), lookups from an in-memory copy; the rows are the cascade: application or window, then generic, then verbatim |
| IDEA-5 | 30/10 | The import is an ETL with validation: a spreadsheet with discrepancies is rejected, never half-loaded. (voice note 30/10) | accepted 30/10: an identical duplicate row is reported and loaded once; any other discrepancy (empty column, unknown language, same entry with two translations) rejects the whole spreadsheet |
| IDEA-6 | 30/10 | Window selection: the user picks the window to translate, not the whole screen. (voice note 30/10) | accepted 30/10: a window, all the windows of a process, or a monitor, read continuously |
| IDEA-7 | 30/10 | A minimal admin interface: choose the dictionary spreadsheet, choose and download the local model (for example from Hugging Face). (voice note 30/10) | proposed |
| IDEA-8 | 30/10 | Pluggable OCR: the native Windows engine or Tesseract (on Linux, and optionally on Windows). (voice note 30/10) | accepted 30/10 |
| IDEA-9 | 30/10 | Clean architecture with design patterns: one solution, one project per concern (acquisition and OCR, overlay, translation, admin and recording, storage), so a new OCR, translator or interface does not touch the rest; C# on both systems, developed with VS Code. (voice note 30/10) | accepted 30/10: a project is added with the first feature that needs it |
| IDEA-10 | 30/10 | A test suite with sample targets: a console, a desktop app on Windows and on Linux, a shell with typed commands, a browser, a remote desktop session. (voice note 30/10) | accepted 30/10: a desktop app and a console from 001, the others with the features that need them; remote desktop stays a manual test |
| IDEA-11 | 30/10 | An updater: checks the latest GitHub release and proposes the download, launcher-style. (voice note 30/10) | proposed |

## Open questions

None.
