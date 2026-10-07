namespace ShellReselectRepro.Pages;

// Exists so the tab bar shows; Shell hides it when there is only one tab.
public class OtherPage : ContentPage
{
  public OtherPage()
  {
    Title = "Other";

    Content = new Label
    {
      Text = "OTHER",
      FontSize = 48,
      HorizontalOptions = LayoutOptions.Center,
      VerticalOptions = LayoutOptions.Center
    };
  }
}
