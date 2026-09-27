# Contributing to the MentalDesk TUI style guide

`README.md` is the guide. `src/` is the code that shows it working:

- `src/MentalDesk.Tui` is the shared library: themes, commands and chord key bindings, focus, the menu and
  status bars, dialogs, the command palette and diagnostics. Apps are meant to use it rather than copy from it.
- `src/Swatch` is the reference app, a theme editor. It exists to exercise every convention in the guide,
  not because apps should edit themes.

When the guide changes, the library and Swatch change with it, in the same PR.

## Build, test, run

```bash
dotnet build MentalDesk.Tui.slnx
dotnet test MentalDesk.Tui.slnx
dotnet run --project src/Swatch
```

CI builds with `-warnaserror` on Linux, macOS and Windows, and publishes Swatch with native AOT, as
TuiCode and a-team ship.

## Conventions

- Terminal.Gui 2.1.0, the version TuiCode and a-team pin. Move all three together.
- A test that boots an `Application` or touches `ThemeManager` derives from `StaticConfigurationTest`:
  those are process-wide statics. Drive the app with `Host`, which runs the real main loop on the headless
  ANSI driver and injects keys.
- `ConfigurationManager.Apply()` puts Terminal.Gui's Quit key back on `Esc`, so anything that applies
  configuration goes through `AppShell.ApplyTheme`, which moves it off again.
- Every theme defines every scheme in `SchemeNames.All`; `ThemesTests` holds them to it.
