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

## Releasing

Run the **Release** workflow from the Actions tab. It publishes `src/MentalDesk.Tui` to nuget.org as
`MentalDesk.Tui`, then tags the commit and creates a GitHub release with generated notes. The tag is the
version: nothing in the repo holds a version number. Leave *bump* on `auto` to pick it from the labels of
PRs merged since the last release: `enhancement` for minor, otherwise patch. Before 1.0, label a breaking
change `enhancement` too, rather than `breaking`, which would bump the major version to 1.0.0.

It pushes to nuget.org with [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing),
so there's no API key to store or rotate. The job runs in the `release` environment, which only `main` can
deploy to, and the nuget.org policy only trusts that environment.

## Conventions

- Terminal.Gui 2.5.0. When it moves, the library moves first and releases; TuiCode and a-team then move
  Terminal.Gui and `MentalDesk.Tui` together, since the package requires at least the version it was built on.
- A test that boots an `Application` or touches `ThemeManager` derives from `StaticConfigurationTest`:
  those are process-wide statics. Drive the app with `Host`, which runs the real main loop on the headless
  ANSI driver and injects keys.
- `themes.json` uses Terminal.Gui 2.5's nested shape: `Themes` and `Schemes` are objects keyed by name, and
  settings are nested (`"Dialog": { "DefaultShadow": … }`). Terminal.Gui skips a file in the old array or
  dotted-key shape with only a log warning.
- Every theme defines every scheme in `SchemeNames.All`; `ThemesTests` holds them to it.
