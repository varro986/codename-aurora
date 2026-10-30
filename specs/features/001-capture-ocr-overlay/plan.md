# 001. Plan

How `spec.md` is built. Stack: ADR 0001. Projects: IDEA-9, one per concern, only the ones this feature needs.

## Projects

```
src/
|-- Aurora.Core/          contracts and pure logic; no Windows or Avalonia reference
|-- Aurora.Windows/       Windows adapters: capture, OCR, window events, hotkey, overlay window styles
|-- Aurora.Overlay/       Avalonia overlay and pick layer, drawn from Core types only
\-- Aurora.App/           composition root: tray, configuration, logging, wiring
tests/
|-- Aurora.Core.Tests/    unit tests with fakes
|-- Aurora.Windows.Tests/ adapter tests against the sample targets
|-- Aurora.EndToEnd.Tests/ the app attached to a sample target, criteria checked from outside
\-- targets/
    |-- Aurora.Targets.Desktop/  Avalonia window with known text, changed on command (IDEA-10)
    \-- Aurora.Targets.Console/  console printing known text, changed on command (IDEA-10)
```

Dependencies point inwards: `App -> Overlay, Windows -> Core`. Libraries: Avalonia, `Microsoft.Extensions.Configuration`
and `Logging`, xUnit v3 on Microsoft.Testing.Platform (all MIT or Apache 2.0); versions in `Directory.Packages.props`.

## Flow

1. Hotkey (or tray Attach) -> pick layer -> `ITargetResolver` turns the click and modifiers into a `Target`.
2. `Attachment` holds one `Target` at a time; a new one disposes the old (AC-1.6).
3. `IWindowEvents` reports foreground, move, resize, minimise, new windows of the process and close for the target.
4. `ReadingLoop`, while the target is in the foreground: frame from `IFrameSource` every interval -> `FrameChange`
   decides whether it differs from the last read -> `IOcrEngine` -> `LayoutMapper` turns OCR pixels into overlay
   coordinates -> `IOverlay.Show`.
5. Not in the foreground or minimised: `IOverlay.Hide`, loop paused (AC-3.4). Closed: detach (AC-2.5).

With a process target, the window read is the process's window currently in the foreground.

## Contracts

Written first, in series, by the orchestrator (task T0); no executor changes them. They live in
`src/Aurora.Core/Contracts/`, one file per concern: targets and resolver, capture, OCR, window events and hotkey,
overlay and its window styler. Coordinates are physical pixels: OCR lines in frame pixels, overlay input in screen
pixels.

## Configuration

`Aurora.App/appsettings.json` holds every value: hotkey, the two pick modifiers, reading interval, OCR language (empty
means the user's profile language). Read as `RawSettings`, validated by `AuroraSettingsParser` (`src/Aurora.Core/Settings/`)
at start: an invalid value stops the start with one message per key saying what is allowed.

## Risks

- **Monitor capture sees the overlay**: reading a monitor would read Aurora's own lines. The overlay and pick windows
  are excluded from capture (`SetWindowDisplayAffinity` with `WDA_EXCLUDEFROMCAPTURE`); a test covers it.
- **Capture border**: Windows Graphics Capture draws a yellow border on the captured window; `IsBorderRequired = false`
  removes it where the system allows, otherwise it stays and is documented.
- **Click-through and focus**: Avalonia has no option for it; the overlay gets `WS_EX_TRANSPARENT | WS_EX_LAYERED |
  WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW` from `Aurora.Windows` through a small styler interface, tested with real input
  (AC-3.2).
- **DPI**: the app is per-monitor DPI aware v2; `LayoutMapper` (frame to screen pixels, screen pixels to overlay units)
  is pure and tested on 100, 125, 150 and 200% (AC-3.5).
- **OCR language**: `Windows.Media.Ocr` depends on installed language packs; the CI machine and the tests declare the
  language they need (en-US).
- **Namespace `Aurora.Windows`**: inside it, `Windows.Media.Ocr` resolves to `Aurora.Windows.Media`; WinRT types are
  written as `global::Windows...` (or with a `using` alias).
- **End-to-end on CI**: the tests need an interactive desktop session; they run locally in `cicd/`, and on a remote
  runner only if one with a desktop session is available.

## Tasks

T0 first; then T1, T3, T5, T6 and T7 in parallel (no shared files), T2 and T4 as soon as T7 is ready; T8 after them,
T9 last.

| Id | Task | Files | Depends on | Role | Done when |
|---|---|---|---|---|---|
| T0 | Solution, projects, contracts, settings binding | `Aurora.slnx`, `global.json`, `Directory.*.props`, `src/*/*.csproj`, `src/Aurora.Core/Contracts/`, `src/Aurora.Core/Settings/`, `tests/**/*.csproj` | - | orchestrator | solution builds with no warnings; settings tests pass |
| T1 | `ReadingLoop`, `Attachment`, `FrameChange`, `LayoutMapper` with unit tests | `src/Aurora.Core/Reading/`, `tests/Aurora.Core.Tests/Reading/` | T0 | executor | AC-1.6, AC-2.1, AC-2.5, AC-3.4, AC-3.5 pass with fakes |
| T2 | Frame source on Windows Graphics Capture (window and monitor) | `src/Aurora.Windows/Capture/`, `tests/Aurora.Windows.Tests/Capture/` | T0, T7 | executor | frames of the sample desktop target; monitor frames exclude an excluded window |
| T3 | OCR on `Windows.Media.Ocr` | `src/Aurora.Windows/Ocr/`, `tests/Aurora.Windows.Tests/Ocr/` | T0 | executor | AC-2.2 on fixed images; AC-2.3 with a language not installed |
| T4 | Target resolver, window list, window events (`SetWinEventHook`) | `src/Aurora.Windows/Targets/`, `tests/Aurora.Windows.Tests/Targets/` | T0, T7 | executor | resolves window, process and monitor; events for move, foreground, new window, close |
| T5 | Global hotkey (`RegisterHotKey`) | `src/Aurora.Windows/Hotkey/`, `tests/Aurora.Windows.Tests/Hotkey/` | T0 | executor | registers, fires, reports a hotkey already taken (AC-1.7), releases on dispose |
| T6 | Overlay and pick layer, window styler, capture exclusion | `src/Aurora.Overlay/`, `src/Aurora.Windows/Overlay/` | T0 | executor | lines drawn at given bounds; click-through, no focus, excluded from capture |
| T7 | Sample targets | `tests/targets/` | T0 | microtask | each shows known text and changes it on a command from the tests |
| T8 | App: tray menu (Attach, Detach, Quit), configuration, logging, wiring | `src/Aurora.App/` | T1-T6 | orchestrator | AC-1.4, AC-4.1, AC-4.2, AC-4.3; invalid configuration stops with a clear message |
| T9 | End-to-end tests and `cicd/` checks | `tests/Aurora.EndToEnd.Tests/`, `cicd/` | T8 | executor | AC-1.1-1.5, AC-2.4, AC-3.1-3.3 pass against both targets; `cicd/` runs build and all tests |

Reviewer: after each task, a fresh agent checks the diff against the criteria it names. Remote desktop and the yellow
border are checked by hand and noted in the pull request.
