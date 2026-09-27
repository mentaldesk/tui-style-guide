using MentalDesk.Tui.Shell;
using MentalDesk.Tui.Theming;
using Swatch;

using var flowControl = new TerminalFlowControl();
Themes.Load();
using var app = Application.Create();
app.Init();
using var shell = new AppShell(app, TerminalCursor.ForConsole());
using var window = new SwatchWindow(shell);
shell.Run(window);
