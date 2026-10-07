namespace ShellReselectRepro.Pages;

public class ChildPage : ContentPage
{
  public ChildPage()
  {
    Title = "Child";
    BackgroundColor = Colors.LightGreen;

    Content = new Label
    {
      Text = "CHILD",
      FontSize = 48,
      HorizontalOptions = LayoutOptions.Center,
      VerticalOptions = LayoutOptions.Center
    };
  }
}
