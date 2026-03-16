using Mystical.WinUI.ViewModels;
using Mystical.WinUI.Services;

namespace Mystical.WinUI.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private async void OnConnectEpicClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Control control)
        {
            control.IsEnabled = false;
        }

        try
        {
            var imported = await ViewModel.ConnectEpicViaImportAsync();
            if (imported)
            {
                return;
            }

            await ShowEpicConnectDialogAsync();
        }
        finally
        {
            if (sender is Control controlRestore)
            {
                controlRestore.IsEnabled = true;
            }
        }
    }

    private async Task ShowEpicConnectDialogAsync()
    {
        var authUrl = ViewModel.GetEpicAuthorizationUrl();
        var viewportWidth = XamlRoot?.Size.Width ?? 1280;
        var viewportHeight = XamlRoot?.Size.Height ?? 720;
        var dialogWidth = Math.Clamp(viewportWidth * 0.66, 360, 760);
        var contentWidth = Math.Max(320, dialogWidth - 48);
        var webViewHeight = Math.Clamp(viewportHeight * 0.44, 220, 460);

        var instructions = new TextBlock
        {
            Text = "Sign in to Epic below. If auto-detect misses the redirect code, paste it manually from the final URL.",
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxWidth = contentWidth
        };

        var webView = new WebView2
        {
            Height = webViewHeight,
            MinHeight = 220,
            MaxHeight = 460,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var codeBox = new TextBox
        {
            Header = "Authorization code",
            PlaceholderText = "Auto-filled from redirect URL when available"
        };

        var externalLink = new HyperlinkButton
        {
            Content = "Open login in external browser",
            NavigateUri = new Uri(authUrl)
        };

        webView.NavigationStarting += (_, args) =>
        {
            var code = TryExtractAuthorizationCode(args.Uri);
            if (!string.IsNullOrWhiteSpace(code))
            {
                codeBox.Text = code;
            }
        };

        webView.CoreWebView2Initialized += (_, args) =>
        {
            if (args.Exception is not null)
            {
                ViewModel.StatusMessage = $"Epic login web view failed to initialize: {args.Exception.Message}";
            }
        };

        webView.Source = new Uri(authUrl);

        var content = new StackPanel
        {
            Spacing = 10,
            Width = contentWidth,
            MaxWidth = contentWidth
        };

        content.Children.Add(instructions);
        content.Children.Add(webView);
        content.Children.Add(codeBox);
        content.Children.Add(externalLink);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Connect Epic account",
            Content = content,
            PrimaryButtonText = "Connect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            MaxWidth = dialogWidth
        };

        dialog.PrimaryButtonClick += async (_, args) =>
        {
            var code = codeBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                args.Cancel = true;
                ViewModel.StatusMessage = "Epic connect requires an authorization code from the login flow.";
                return;
            }

            var deferral = args.GetDeferral();
            try
            {
                var connected = await ViewModel.ConnectEpicWithAuthorizationCodeAsync(code);
                if (!connected)
                {
                    args.Cancel = true;
                }
            }
            finally
            {
                deferral.Complete();
            }
        };

        _ = await dialog.ShowAsync();
    }

    private static string TryExtractAuthorizationCode(string? uriText)
    {
        if (string.IsNullOrWhiteSpace(uriText))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return string.Empty;
        }

        return TryExtractParameter(uri.Query, "authorizationCode")
               ?? TryExtractParameter(uri.Query, "code")
               ?? TryExtractParameter(uri.Query, "exchangeCode")
               ?? TryExtractParameter(uri.Fragment, "authorizationCode")
               ?? TryExtractParameter(uri.Fragment, "code")
               ?? string.Empty;
    }

    private static string? TryExtractParameter(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.TrimStart('?', '#');
        foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = segment.Split('=', 2, StringSplitOptions.None);
            if (pair.Length != 2 || !string.Equals(pair[0], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Uri.UnescapeDataString(pair[1]);
        }

        return null;
    }

    private void OnHelpTopicClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string sectionKey })
        {
            return;
        }

        HelpNavigationService.RequestTopic(sectionKey);
    }
}
