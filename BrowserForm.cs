using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;

namespace ELITE.Browser;

public sealed class BrowserForm : Form
{
    private readonly TabControl tabs = new();
    private readonly TextBox address = new();
    private readonly Label status = new();
    private readonly Button shield = new();
    private bool blocking = true;
    private bool dark = true;
    private bool privateMode;
    private string? privateFolder;
    private readonly HashSet<string> blockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adservice.google.com", "adnxs.com", "adform.net", "taboola.com",
        "outbrain.com", "scorecardresearch.com", "zedo.com", "criteo.com",
        "ads.yahoo.com", "amazon-adsystem.com"
    };

    public BrowserForm()
    {
        Text = "ELITE Browser";
        MinimumSize = new Size(850, 560);
        Size = new Size(1180, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);
        BuildUi();
        ApplyTheme();
        Shown += async (_, _) => await NewTabAsync("https://www.google.com");
        FormClosing += (_, _) => CleanupPrivateData();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(8, 7, 8, 5) };
        var nav = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 178, WrapContents = false };
        nav.Controls.Add(MakeButton("‹", (_, _) => CurrentWeb()?.GoBack()));
        nav.Controls.Add(MakeButton("›", (_, _) => CurrentWeb()?.GoForward()));
        nav.Controls.Add(MakeButton("↻", (_, _) => CurrentWeb()?.Reload()));
        nav.Controls.Add(MakeButton("⌂", async (_, _) => await NavigateAsync("https://www.google.com")));
        address.Dock = DockStyle.Fill;
        address.Font = new Font("Segoe UI", 10F);
        address.BorderStyle = BorderStyle.FixedSingle;
        address.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await NavigateAsync(address.Text);
            }
        };
        var right = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 310, WrapContents = false, FlowDirection = FlowDirection.RightToLeft };
        right.Controls.Add(MakeButton("New tab +", async (_, _) => await NewTabAsync("https://www.google.com")));
        right.Controls.Add(MakeButton("Theme", (_, _) => { dark = !dark; ApplyTheme(); }));
        right.Controls.Add(MakeButton("Private", async (_, _) => { privateMode = !privateMode; await NewTabAsync("https://www.google.com"); UpdateStatus(); }));
        right.Controls.Add(MakeButton("VPN settings", (_, _) => OpenVpnSettings()));
        shield.Text = "Shield ON";
        shield.Width = 80;
        shield.Height = 30;
        shield.Click += (_, _) => { blocking = !blocking; shield.Text = blocking ? "Shield ON" : "Shield OFF"; UpdateStatus(); };
        right.Controls.Add(shield);
        top.Controls.Add(address);
        top.Controls.Add(right);
        top.Controls.Add(nav);

        tabs.Dock = DockStyle.Fill;
        tabs.SelectedIndexChanged += (_, _) => SyncAddress();
        status.Dock = DockStyle.Bottom;
        status.Height = 24;
        status.Padding = new Padding(10, 3, 4, 2);
        status.Text = "ELITE • Ready";
        Controls.Add(tabs);
        Controls.Add(status);
        Controls.Add(top);
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var b = new Button { Text = text, Width = text.Length > 5 ? 88 : 40, Height = 30, Margin = new Padding(2, 0, 2, 0), FlatStyle = FlatStyle.Flat };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private async Task NewTabAsync(string url)
    {
        var page = new TabPage("New tab");
        var web = new WebView2 { Dock = DockStyle.Fill };
        page.Controls.Add(web);
        tabs.TabPages.Add(page);
        tabs.SelectedTab = page;
        try
        {
            string? userData = null;
            if (privateMode)
            {
                privateFolder ??= Path.Combine(Path.GetTempPath(), "ELITE-Private-" + Guid.NewGuid().ToString("N"));
                userData = privateFolder;
            }
            var env = await CoreWebView2Environment.CreateAsync(null, userData);
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled = true;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.NewWindowRequested += async (_, e) =>
            {
                e.Handled = true;
                await NewTabAsync(e.Uri);
            };
            web.CoreWebView2.DownloadStarting += (_, e) =>
            {
                try
                {
                    var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "ELITE");
                    Directory.CreateDirectory(folder);
                    e.ResultFilePath = Path.Combine(folder, Path.GetFileName(new Uri(e.DownloadOperation.Uri).LocalPath));
                }
                catch { }
            };
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (blocking && IsBlocked(e.Uri))
                {
                    e.Cancel = true;
                    status.Text = "ELITE Shield • Blocked known ad/tracker request";
                }
            };
            web.CoreWebView2.WebResourceRequested += (_, e) =>
            {
                if (blocking && IsBlocked(e.Request.Uri))
                    e.Response = web.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Blocked by ELITE Shield", "Content-Type: text/plain");
            };
            web.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            web.CoreWebView2.DocumentTitleChanged += (_, _) =>
            {
                if (!IsDisposed && page.Parent is not null)
                    page.Text = string.IsNullOrWhiteSpace(web.CoreWebView2.DocumentTitle) ? "ELITE" : web.CoreWebView2.DocumentTitle;
            };
            web.Source = new Uri(SanitizeUrl(url));
            UpdateStatus();
        }
        catch (Exception ex)
        {
            page.Controls.Add(new Label { Text = "WebView2 could not start. Install Microsoft Edge WebView2 Runtime.\r\n\r\n" + ex.Message, Dock = DockStyle.Bottom, Height = 70 });
            status.Text = "ELITE • WebView2 initialization failed";
        }
    }

    private static bool IsBlocked(string value)
    {
        try
        {
            var host = new Uri(value).Host;
            return host.Contains("doubleclick.net", StringComparison.OrdinalIgnoreCase)
                || host.Contains("googlesyndication.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains("googleadservices.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains("adnxs.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains("taboola.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains("outbrain.com", StringComparison.OrdinalIgnoreCase)
                || host.Contains("criteo.com", StringComparison.OrdinalIgnoreCase)
                || host.StartsWith("ads.", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string SanitizeUrl(string input)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) return "https://www.google.com";
        if (Regex.IsMatch(input, @"^https?://", RegexOptions.IgnoreCase)) return input;
        if (input.Contains('.') && !input.Contains(' ')) return "https://" + input;
        return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
    }

    private async Task NavigateAsync(string url)
    {
        var web = CurrentWeb();
        if (web?.CoreWebView2 is null)
        {
            await NewTabAsync(url);
            return;
        }
        try { web.CoreWebView2.Navigate(SanitizeUrl(url)); }
        catch { }
    }

    private WebView2? CurrentWeb() => tabs.SelectedTab?.Controls.OfType<WebView2>().FirstOrDefault();

    private void SyncAddress()
    {
        var web = CurrentWeb();
        if (web?.Source is not null) address.Text = web.Source.ToString();
        UpdateStatus();
    }

    private void UpdateStatus() =>
        status.Text = $"ELITE • {(blocking ? "Shield ON" : "Shield OFF")} • {(privateMode ? "Private mode for new tabs" : "Standard profile")} • Downloads: Downloads\\ELITE";

    private void ApplyTheme()
    {
        var back = dark ? Color.FromArgb(17, 20, 29) : Color.FromArgb(245, 247, 250);
        var fore = dark ? Color.FromArgb(230, 235, 245) : Color.FromArgb(30, 35, 45);
        BackColor = back;
        ForeColor = fore;
        address.BackColor = dark ? Color.FromArgb(28, 33, 46) : Color.White;
        address.ForeColor = fore;
        status.BackColor = dark ? Color.FromArgb(23, 27, 38) : Color.FromArgb(230, 234, 240);
        status.ForeColor = fore;
        foreach (Control c in Controls)
        {
            if (c is Panel || c is FlowLayoutPanel) c.BackColor = back;
            foreach (Control child in c.Controls)
            {
                if (child is Button b) { b.BackColor = dark ? Color.FromArgb(35, 43, 59) : Color.FromArgb(225, 231, 239); b.ForeColor = fore; }
            }
        }
    }

    private void OpenVpnSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:network-vpn") { UseShellExecute = true }); }
        catch { status.Text = "Open Windows Settings > Network & Internet > VPN manually."; }
    }

    private void CleanupPrivateData()
    {
        if (string.IsNullOrWhiteSpace(privateFolder)) return;
        try { tabs.Dispose(); Directory.Delete(privateFolder, true); } catch { }
    }
}