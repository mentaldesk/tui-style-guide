namespace MentalDesk.Tui.Shell;

public class AppWindow : Window
{
    public AppWindow(AppShell shell)
    {
        BorderStyle = LineStyle.None;
        Width = Dim.Fill();
        Height = Dim.Fill();
        var top = 0;
        if (shell.Menu is { } menu)
        {
            menu.Bar.Y = 0;
            Add(menu.Bar);
            top = 1;
        }
        Body = new View { Y = top, Width = Dim.Fill(), Height = Dim.Fill(1), CanFocus = true };
        Add(Body, shell.StatusBar);
    }

    public View Body { get; }
}
