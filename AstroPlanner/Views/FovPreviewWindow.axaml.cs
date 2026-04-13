using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class FovPreviewWindow : Window
{
    public FovPreviewWindow()
    {
        InitializeComponent();

        WebView.EnvironmentRequested += (_, e) =>
        {
            // ExperimentalOffscreen renders WebKit to an offscreen surface that
            // Avalonia composites — required for the view to appear on Linux/GTK.
            if (e is GtkWebViewEnvironmentRequestedEventArgs gtk)
                gtk.ExperimentalOffscreen = true;
        };

        WebView.AdapterCreated += (_, _) =>
        {
            if (DataContext is FovPreviewViewModel vm)
                WebView.NavigateToString(vm.HtmlContent, new Uri("https://aladin.cds.unistra.fr/"));
        };

        WebView.NavigationCompleted += async (_, _) =>
        {
            // WebKitGTK offscreen surface is only blitted when the native widget is
            // actually resized. Do an invisible 1-pixel window nudge to trigger it,
            // then repeat a few times as Aladin's tiles stream in.
            for (int i = 0; i < 1; i++)
            {
                await Task.Delay(i == 0 ? 200 : 500);
                var s = ClientSize;
                ClientSize = new Avalonia.Size(s.Width + 1, s.Height);
                await Task.Delay(32);
                ClientSize = s;
            }
        };
    }
}
