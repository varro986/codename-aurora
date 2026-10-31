# Sample targets

Two programs with known printed text (dark text on a light background) that the tests attach to. The desktop target
uses a large font; the console target uses the console's default font. They
change their content on command, over a named pipe, while Aurora is attached.

## Arguments

| Argument | Meaning |
|---|---|
| `--pipe <name>` | Required. Name of the named pipe the target serves (`\\.\pipe\<name>`). |
| `--text "<line1>\|<line2>\|..."` | Optional initial lines (separator is a plain `|`); a default text otherwise. |
| `--parent <pid>` | Optional. The target exits when that process ends (the tests pass their own id, so a crashed run leaves no target behind). |

## Protocol

One command per line, UTF-8. The target answers one line, `ok` or `error <reason>`, after the change is applied (not
necessarily drawn yet). A failing command answers `error <message>` and the target keeps serving. The pipe accepts one
client at a time, and a new client after the previous one disconnects, also when it drops without saying `exit`.

| Command | Target | Effect |
|---|---|---|
| `set <line1>\|<line2>\|...` | both | Replaces the text; with no lines, an empty text. |
| `exit` | both | Answers `ok`, then closes the target. |
| `move <x> <y>` | desktop | Moves the main window to the screen position, in physical pixels. |
| `resize <w> <h>` | desktop | Sets the size of the main window's client area, in physical pixels. |
| `minimize`, `restore` | desktop | Minimises the main window, or brings it back to normal. |
| `second-window <line1>\|...` | desktop | Opens a second top-level window of the same process with that text. |
| `title` | desktop | Answers `ok <window title>`. |

## Finding the window

The desktop target's main window is titled `Aurora Target Desktop` (the second window `Aurora Target Desktop Second`),
starts at 100,100 and is 900x400 device independent pixels. Find it from the process (`MainWindowHandle`) or by title.
The console target runs in its own console window, started by the test without redirection, and reads commands only
from the pipe.
