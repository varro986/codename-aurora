# 001. Capture, OCR, overlay

From: README (Description), IDEA-6 (pick what to read), IDEA-8 (OCR behind an interface), ADR 0001.

The first end-to-end slice: Aurora attaches to a window, to all the windows of a process or to a monitor, keeps reading
its text with the native Windows OCR and shows each line in place in the overlay. No translation yet: the overlay
shows the recognised text as it is, which proves capture, OCR and positions before translation arrives.

## Stories

- **US-1. Attach.** As a user, I attach Aurora to a window, to its process or to a monitor, from the screen or from the
  tray, without leaving the application I am working in.
- **US-2. Keep reading.** As a user, the text of what I attached is read on the machine and kept up to date while it
  changes.
- **US-3. Text in place.** As a user, I see each recognised line over the original one, and I keep working under it.
- **US-4. Tray only.** As a user, Aurora lives in the tray, with no main window; I detach and quit from there.

## Acceptance criteria

US-1
- **AC-1.1** Given Aurora is running, when I press the configured hotkey and click a window, then Aurora attaches to
  that window.
- **AC-1.2** Given the pick is active, when I click a window with the configured process modifier held, then Aurora
  attaches to every window of that process, including the ones it opens later.
- **AC-1.3** Given the pick is active, when I click with the configured monitor modifier held, then Aurora attaches to
  the monitor under the click.
- **AC-1.4** Given the tray menu, when I open Attach, then it lists the open windows, their processes and the monitors,
  and choosing one attaches to it.
- **AC-1.5** Given the pick is active, when I press Esc, then the pick closes and the current attachment, if any, is
  unchanged.
- **AC-1.6** Given Aurora is attached, when I attach to something else, then the new attachment replaces the old one.
- **AC-1.7** Given another application already holds the configured hotkey, when Aurora starts, then it says so in the
  tray and in the log, and keeps running with the tray menu.

US-2
- **AC-2.1** Given Aurora is attached, when the content changes, then the text is read again within the configured
  interval; when nothing changes, OCR does not run again.
- **AC-2.2** Given an attached window with printed text in a language installed for Windows OCR, then every recognised
  line comes with its position in the window.
- **AC-2.3** Given the OCR language is not installed, then the tray and the log say which language is missing and how to
  install it.
- **AC-2.4** Given any reading, then no network call is made.
- **AC-2.5** Given the attached window or process closes, then Aurora detaches, hides the overlay and says so in the
  tray.

US-3
- **AC-3.1** Given a recognised line, then the overlay draws it over the original line, covering it.
- **AC-3.2** Given the overlay is shown, when I click or type over it, then the input reaches the window below and the
  overlay never takes the focus.
- **AC-3.3** Given an attached window, when it moves or is resized, then the overlay follows it.
- **AC-3.4** Given an attached window or process, when it is no longer in the foreground or is minimised, then the
  overlay hides and reading pauses; when it comes back, both resume.
- **AC-3.5** Given a monitor with scaling other than 100%, then every line is drawn over the original within 1 pixel.

US-4
- **AC-4.1** Given Aurora starts, then only a tray icon appears, no window and no taskbar entry.
- **AC-4.2** Given Aurora is attached, when I choose Detach in the tray, then the overlay closes and reading stops.
- **AC-4.3** Given the tray menu, when I choose Quit, then Aurora exits and releases the hotkey.

## Out of scope

Translation, more than one attachment at a time, Linux and Tesseract, a settings interface: hotkey, modifiers and
reading interval come from the configuration file.
