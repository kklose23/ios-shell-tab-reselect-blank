# iOS: re-tapping the selected Shell tab during a navigation leaves the tab blank

## Summary

On iOS 26, tapping the already-selected tab of a Shell `TabBar` while a push or a pop-to-root is still animating
leaves that tab permanently blank: the navigation bar and tab bar stay, the page content is gone, and only an app
restart recovers it.

There are two causes, both in the iOS Shell:

1. **During `GoToAsync`, MAUI approves every tab selection without raising `Navigating`.** iOS then pops the tab to
   its root in the middle of the push, and Shell's stack and route disagree from then on.
2. **Nothing stops a second pop-to-root while UIKit's first one is still animating.** That pop either never
   completes on MAUI's side, so Shell's route stays stale, or the root page is left off screen while Shell's state
   is correct.

Possibly the same family as dotnet/maui#35232, which hits a blank page from rapid push/back on iOS 26 and is
worked around with `animate: false`. That report involves no tabs; this one has a specific code path and
reproduces on the simulator.

## Environment

- .NET SDK 10.0.401
- Microsoft.Maui.Controls 10.0.110
- `net10.0-ios`, iOS 26.5 Simulator
- No third-party packages

## Steps to reproduce

1. Run the project on the iOS Simulator and turn on **Debug > Slow Animations** (⌘T) in the Simulator menu. It
   stretches the push animation so the tap in step 3 is easy to land; without it the window is the normal slide-in,
   a fraction of a second.
2. On **Home**, tap **Open child page** once. The button sits directly above the Home tab.
3. While the child page is still sliding in, tap the **Home** tab.
4. If the tab isn't blank yet, repeat steps 2–3. Also land some Home taps while the child page is sliding back *out*
   from an earlier Home tap.

A single button tap is enough; a double tap is not needed.

The `[Repro]` lines in the debug output show Shell's route, the page on screen and the stack after each
navigation event.

## Expected

Either the tab tap is ignored until the navigation finishes, or the tab pops cleanly to Home.

## Actual

The Home tab goes blank and stays blank, including after switching to **Other** and back. Output from this
project:

```
[Repro] Navigated source=ShellItemChanged location='//HomePage' page='HomePage' stack='root'
[Repro] Navigating source=Push target='ChildPage' location='//HomePage' page='HomePage' stack='root'
[Repro] Navigating source=Push target='ChildPage' location='//HomePage' page='HomePage' stack='root'
[Repro] Navigated source=PopToRoot location='//HomePage' page='HomePage' stack='root'
[Repro] Navigating source=ShellSectionChanged target='//HomePage' location='//HomePage' page='HomePage' stack='root'
    (five more identical Home-tab re-selections)
[Repro] Navigating source=ShellSectionChanged target='//OtherPage' location='//HomePage' page='HomePage' stack='root'
[Repro] Navigated source=ShellSectionChanged location='//OtherPage' page='OtherPage' stack='root'
[Repro] Navigating source=ShellSectionChanged target='//HomePage' location='//OtherPage' page='OtherPage' stack='root'
[Repro] Navigated source=ShellSectionChanged location='//HomePage' page='HomePage' stack='root'
```

Two things in it:

- **The pop-to-root has no `Navigating`.** It is a Home-tab tap that landed during a push and was approved without
  being proposed (cause 1 below). The tab taps after it, made once no push was in flight, each raise `Navigating`
  as expected.
- **The push never reports `Navigated source=Push`.** The only `Navigated` from that `GoToAsync` carries
  `source=PopToRoot`.

From then on Shell's own state is consistent (route `//HomePage`, page `HomePage`, stack `root`), yet the page is
not on screen.

## Cause 1: tab selection is approved without `Navigating` during `GoToAsync`

`ShellItemRenderer`'s `ShouldSelectViewController` asks Shell whether the tab may be selected
([ShellItemRenderer.cs#L144](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Shell/iOS/ShellItemRenderer.cs#L144)).
That reaches `ShellNavigationManager.ProposeNavigationOutsideGotoAsync`, which returns `true` immediately while
`AccumulateNavigatedEvents` is set
([ShellNavigationManager.cs#L392-L393](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Shell/ShellNavigationManager.cs#L392-L393)):

```csharp
if (AccumulateNavigatedEvents)
    return true;
```

`AccumulateNavigatedEvents` is set for the whole of `GoToAsync`, from just after `Navigating` until the push has
finished
([L91](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Shell/ShellNavigationManager.cs#L91) to
[L205](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Shell/ShellNavigationManager.cs#L205)).
So no `Navigating` handler, and no `ShellNavigatingEventArgs.Cancel()`, ever sees the tap.

With the selection approved, UIKit treats it as a re-selection and pops the tab's navigation controller to its root
while the pushed page is still animating in:

```
-[UINavigationController _updateForReselectionInContainingTabBarController]
-[_UITabBarControllerVisualStyle updateViewControllerForReselection:]
-[UITabBarController _setSelectedViewControllerAndNotify:]
-[UITabBarController _tabBarDidChangeSelectionToItem:]
-[UITabBar _didSelectButtonForItem:]
```

`ShellSectionRenderer.PopToRootViewController` sees two view controllers and runs `ProcessPopToRoot`
([ShellSectionRenderer.cs#L552](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Shell/iOS/ShellSectionRenderer.cs#L552)),
which resets the section's stack to the root page while the push is still in progress. In this project the result
is the consistent-but-blank state above. In a larger app with a slower page, the interrupted push instead went on
to record its route, which left Shell's route at `//HomePage/ChildPage` with only the root page on its stack. `DidPopItem`
([L139](https://github.com/dotnet/maui/blob/10.0.110/src/Controls/src/Core/Compatibility/Handlers/Shell/iOS/ShellSectionRenderer.cs#L139))
then finds the navigation bar out of step with the stack, treats it as a swipe-back, and `SendPop` issues
`GoToAsync("..")` from the root page.

Observed in the app: every re-tap that arrived *without* a push in flight raised `Navigating` with
`source=ShellSectionChanged`, and cancelling it made `ShouldSelectViewController` return `false`, which UIKit
respected. The re-tap that arrived *during* the push raised no `Navigating` and returned `true`.

## Cause 2: re-tap during an earlier pop-to-root

With cause 1 worked around, a second failure remains. Tap Home once with the child page fully shown, which starts a
legitimate pop-to-root, then tap Home again before it finishes. That second tap is approved and UIKit calls
`PopToRootViewController` again mid-transition. Two outcomes were observed:

- MAUI never sees the first pop finish, so Shell's route keeps ending in the child page while the root page is on
  screen. Every later push from that tab then misbehaves.
- Shell's state is fully consistent (route `//HomePage`, page `HomePage`, stack `root`, one view controller in the
  navigation controller), but the tab is blank.

## Workaround

Wrap `ShouldSelectViewController` in a `ShellItemRenderer` subclass, and refuse selection while a `GoToAsync` started
by the app is in flight or while the selected tab's navigation controller has a transition coordinator:

```csharp
public override void ViewDidLoad()
{
    base.ViewDidLoad();

    var shouldSelect = ShouldSelectViewController;
    ShouldSelectViewController = (tabController, viewController) =>
    {
        if (navigationInFlight || tabController.SelectedViewController?.GetTransitionCoordinator() is not null)
        {
            return false;
        }

        return shouldSelect(tabController, viewController);
    };
}
```

`navigationInFlight` is the app's own count of awaited `GoToAsync` calls. The transition coordinator alone does not
cover a push, because it does not exist yet while the page is being created or any more while Shell finishes up
after the animation.
