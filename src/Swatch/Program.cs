using MentalDesk.Tui.Keys;
using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

using var flowControl = new TerminalFlowControl();
SampleSchemes.Register();
Themes.Load();
using var app = Application.Create();
app.Init();
using var shell = new AppShell(app, TerminalCursor.ForConsole());
shell.UseMacKeys(new MacKeys("Swatch"));
using var window = new SwatchWindow(shell);
shell.Run(window);
