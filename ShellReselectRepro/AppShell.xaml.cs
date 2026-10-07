using System.Diagnostics;
using ShellReselectRepro.Pages;

namespace ShellReselectRepro;

public partial class AppShell : Shell
{
  public AppShell()
  {
    InitializeComponent();

    Routing.RegisterRoute(nameof(ChildPage), typeof(ChildPage));

    // Logs Shell's view of the current tab after each navigation event. The broken state shows up as a
    // location ending in ChildPage while the stack holds only the root page.
    Navigating += (_, e) => Debug.WriteLine($"[Repro] Navigating source={e.Source} target='{e.Target?.Location}' {DescribeState()}");
    Navigated += (_, e) => Debug.WriteLine($"[Repro] Navigated source={e.Source} {DescribeState()}");
  }

  private string DescribeState()
  {
    // A Shell section's stack starts with null for its root page.
    var stack = Navigation.NavigationStack.Select(page => page?.GetType().Name ?? "root");

    return $"location='{CurrentState?.Location}' page='{CurrentPage?.GetType().Name ?? "null"}' stack='{string.Join(">", stack)}'";
  }
}
