using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform;
using AstroPlanner.ViewModels;

namespace AstroPlanner.Views;

public partial class FovPreviewWindow : Window
{
    // Guard against slider↔numeric circular updates
    private bool _syncingRotation;

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
            // actually resized. Do an invisible 1-pixel window nudge to trigger it.
            await Task.Delay(200);
            var s = ClientSize;
            ClientSize = new Avalonia.Size(s.Width + 1, s.Height);
            await Task.Delay(32);
            ClientSize = s;
        };

        RotationSlider.ValueChanged  += OnSliderChanged;
        RotationNumeric.ValueChanged += OnNumericChanged;
        ResetRotationButton.Click    += OnResetClicked;
    }

    private void OnSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingRotation) return;
        _syncingRotation = true;
        RotationNumeric.Value = (decimal)e.NewValue;
        _syncingRotation = false;
        ApplyRotation(e.NewValue);
    }

    private void OnNumericChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncingRotation) return;
        _syncingRotation = true;
        RotationSlider.Value = (double)(e.NewValue ?? 0m);
        _syncingRotation = false;
        ApplyRotation((double)(e.NewValue ?? 0m));
    }

    private void OnResetClicked(object? sender, RoutedEventArgs e)
    {
        RotationSlider.Value = 0;
    }

    private void ApplyRotation(double deg)
    {
        var js = $"setRotation({deg.ToString("F2", CultureInfo.InvariantCulture)})";
        WebView.InvokeScript(js);
    }
}
