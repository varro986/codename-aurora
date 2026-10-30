# 0001. Stack: .NET 10 and Avalonia

Status: accepted

## Context

Aurora starts on Windows and adds Linux later (IDEA-1), with OCR behind an interface (IDEA-8). The interface layer is
the part most costly to rewrite, so it must already run on both systems. .NET 8 leaves support in November 2026.

## Decision

- C# on .NET 10 (LTS).
- Avalonia (MIT) for the overlay, the tray icon and any later admin window.
- Windows-specific behaviour (click-through, no focus, global hotkeys, native OCR) sits in Windows adapters behind
  interfaces; the rest of the code does not reference Windows APIs.

## Consequences

- Linux needs new adapters (OCR with Tesseract, hotkeys, overlay behaviour), not a new interface.
- The click-through, never-focused overlay on Windows needs Win32 interop (extended window styles), tested on its own.
- WPF and WinUI, simpler for a Windows-only overlay, are ruled out because they would be rewritten for Linux.
