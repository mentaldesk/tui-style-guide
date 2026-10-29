using MentalDesk.Tui.Paths;

namespace MentalDesk.Tui.Tests;

public class HomePathTests
{
    [Fact]
    public void Show_PathUnderHome_StartsWithTilde() =>
        Assert.Equal("~/code", HomePath.Show("/home/sam/code", "/home/sam"));

    [Fact]
    public void Show_NoHome_LeavesPathAlone() =>
        Assert.Equal("/home/sam/code", HomePath.Show("/home/sam/code", ""));
}
