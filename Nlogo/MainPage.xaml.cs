using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Nlogo
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

#if WINDOWS
            blazorWebView.BlazorWebViewInitialized += (s, e) =>
            {
                e.WebView.CoreWebView2.OpenDevToolsWindow();
            };
#endif
        }
    }
}