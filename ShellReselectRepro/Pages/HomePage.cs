namespace ShellReselectRepro.Pages;

public class HomePage : ContentPage
{
  public HomePage()
  {
    Title = "Home";
    BackgroundColor = Colors.LightSkyBlue;

    var openButton = new Button { Text = "Open child page" };
    openButton.Clicked += async (_, _) => await Shell.Current.GoToAsync(nameof(ChildPage));

    var instructions = new VerticalStackLayout
    {
      Spacing = 16,
      VerticalOptions = LayoutOptions.Center,
      Children =
      {
        new Label { Text = "HOME", FontSize = 48, HorizontalOptions = LayoutOptions.Center },
        new Label
        {
          Text = "Tap the button, then tap the Home tab below it while the child page is still sliding in or out. " +
                 "Repeat quickly until this page goes blank.",
          HorizontalTextAlignment = TextAlignment.Center
        }
      }
    };

    // The button sits in the left half of the bottom row, directly above the Home tab (the first of two tabs),
    // so the two can be alternated quickly with a mouse on the simulator.
    var layout = new Grid
    {
      Padding = 16,
      RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
      ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
    };

    layout.Add(instructions, column: 0, row: 0);
    Grid.SetColumnSpan(instructions, 2);
    layout.Add(openButton, column: 0, row: 1);

    Content = layout;
  }
}
