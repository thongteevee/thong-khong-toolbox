# Toolbox logo

The TK monogram uses **Segoe UI Bold**, matching Toolbox's Windows typography. The navy square and blue tiles are vector shapes.

- `toolbox-logo.svg` contains outlined font glyphs and shapes for consistent rendering at any size.
- `toolbox-logo.png` is a 1024-pixel transparent export.
- `toolbox.ico` contains separately rendered 16, 24, 32, 48, 64, 128, and 256-pixel frames.

Run `powershell -NoProfile -STA -File assets/Build-Logo.ps1` on Windows to rebuild using the installed Segoe UI font and WPF vector rendering.
