# 0002. Dictionary: shared spreadsheet, SQLite, all-or-nothing import

Status: accepted

## Context

Translation must stay local and predictable (IDEA-2): what the dictionary knows is translated, the rest is left as it
is, and no model runs while translating. The dictionary is maintained by people in a shared spreadsheet (IDEA-4) and
must never be loaded half-valid (IDEA-5). OCR runs continuously, so a lookup happens for every recognised line.

## Decision

- **Source**: a spreadsheet with four columns (application or window, or `generic`; source label; translated value;
  language), in a synced shared location; its path comes from configuration.
- **Cascade**: for each line, the entry of the attached application or window, then the `generic` one, then the text
  as it is.
- **Storage**: the spreadsheet is imported periodically into a local SQLite database (`Microsoft.Data.Sqlite`, MIT;
  SQLite itself is public domain, more permissive than MIT, so the licence rule is kept in substance). Lookups run on an
  in-memory copy, loaded at start and after every import.
- **Import**: validated before loading, inside one transaction. An identical duplicate row is reported and loaded once;
  any other discrepancy (an empty column, an unknown language, the same entry with two translations) rejects the whole
  spreadsheet, reports every row and reason, and keeps the last good version.

## Consequences

- The app works offline; the shared location is only read at import.
- The recorder (IDEA-3) can store its proposed translations in the same database, for the admin to approve.
- A JSON file swapped atomically would be simpler for a read-only dictionary; it is ruled out because the recorder
  needs a database.
