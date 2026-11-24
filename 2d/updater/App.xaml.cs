using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DoisD.Updater
{
    public enum LaunchMode { Auto, Manual }

    /// <summary>
    /// Argumentos:
    ///   --auto            verificação silenciosa ao abrir o editor (só aparece se houver novidade)
    ///   --manual          verificação pedida pelo usuário (sempre mostra o resultado)
    ///   --feed &lt;url&gt;     fonte alternativa do update.json (testes)
    ///   --appdir &lt;dir&gt;   pasta do 2D Editor (definida automaticamente)
    /// </summary>
    public partial class App : Application
    {
        static Mutex _single;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var args = e.Args.ToList();
            string Arg(string name) { int i = args.IndexOf(name); return i >= 0 && i + 1 < args.Count ? args[i + 1] : null; }

            var mode = args.Contains("--manual") ? LaunchMode.Manual : LaunchMode.Auto;
            string selfPath = Process.GetCurrentProcess().MainModule.FileName;
            UpdateService.AppDir = Arg("--appdir") ?? Path.GetFullPath(Path.Combine(Path.GetDirectoryName(selfPath), ".."));
            UpdateService.FeedUrl = Arg("--feed") ?? Environment.GetEnvironmentVariable("DOISD_UPDATE_FEED") ?? UpdateService.DefaultFeed;

            string preview = Arg("--preview");
            if (preview != null)
            {
                ApplyTheme();
                var f = await UpdateService.FetchFeedAsync(CancellationToken.None);
                var w = new MainWindow(LaunchMode.Manual, f, new Version(1, 0, 0), preview != "uptodate", null);
                w.Closed += (s, a) => Shutdown(0);
                w.Show();
                w.Preview(preview);
                string shot = Arg("--shot");
                if (shot != null)
                {
                    await Task.Delay(1200);
                    w.SaveSnapshot(shot, args.Contains("--transparent"));
                    Shutdown(0);
                }
                return;
            }

            // Roda sempre a partir de uma cópia temporária: assim o instalador pode
            // substituir o atualizador original sem conflito.
            if (!args.Contains("--relocated"))
            {
                try
                {
                    string dir = Path.Combine(Path.GetTempPath(), "2DEditorUpdater", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(dir);
                    string copy = Path.Combine(dir, "2DUpdater.exe");
                    File.Copy(selfPath, copy, true);
                    var rest = string.Join(" ", e.Args.Where(a => a != "--appdir" && a != Arg("--appdir")).Select(Quote));
                    Process.Start(new ProcessStartInfo(copy, $"{rest} --relocated --appdir {Quote(UpdateService.AppDir)}") { UseShellExecute = false });
                    Shutdown(0);
                    return;
                }
                catch (Exception ex) { UpdateService.Log("Não foi possível copiar o atualizador: " + ex.Message); }
            }

            _single = new Mutex(false, "Local\\2DEditorUpdaterUI");
            bool owned;
            try { owned = _single.WaitOne(mode == LaunchMode.Manual ? TimeSpan.FromSeconds(40) : TimeSpan.Zero); }
            catch (AbandonedMutexException) { owned = true; }
            if (!owned) { Shutdown(0); return; } // já existe uma janela do atualizador

            try
            {
                if (mode == LaunchMode.Auto)
                {
                    await Task.Delay(TimeSpan.FromSeconds(6)); // deixa o editor abrir primeiro
                    var st = UpdateService.LoadState();
                    if (DateTime.TryParse(st.LastCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last)
                        && DateTime.UtcNow - last < TimeSpan.FromHours(1))
                    { Shutdown(0); return; }
                }

                UpdateFeed feed = null;
                Exception error = null;
                try { feed = await UpdateService.FetchFeedAsync(CancellationToken.None); }
                catch (Exception ex) { error = ex; UpdateService.Log("Falha ao consultar atualizações: " + ex.Message); }

                var state = UpdateService.LoadState();
                state.LastCheckUtc = DateTime.UtcNow.ToString("o");
                UpdateService.SaveState(state);

                var current = UpdateService.CurrentVersion;
                var latest = feed == null ? null : UpdateService.ParseVersion(feed.Version);
                bool hasUpdate = latest != null && latest > current;
                UpdateService.Log($"Versão instalada {UpdateService.Pretty(current)}, publicada {feed?.Version ?? "?"} ({mode})");

                if (mode == LaunchMode.Auto)
                {
                    if (!hasUpdate || (!feed.Mandatory && state.SkippedVersion == feed.Version)) { Shutdown(0); return; }
                }

                ApplyTheme();
                var win = new MainWindow(mode, feed, current, hasUpdate, error);
                win.Closed += (s, a) => Shutdown(0);
                win.Show();
            }
            catch (Exception ex)
            {
                UpdateService.Log("Erro inesperado: " + ex);
                Shutdown(1);
            }
        }

        /// <summary>Segue o tema de aplicativos do Windows (claro/escuro).</summary>
        void ApplyTheme()
        {
            bool dark = false;
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    dark = k?.GetValue("AppsUseLightTheme") is int v && v == 0;
            }
            catch { }
            if (Environment.GetEnvironmentVariable("DOISD_UPDATER_THEME") == "dark") dark = true;
            if (Environment.GetEnvironmentVariable("DOISD_UPDATER_THEME") == "light") dark = false;
            if (!dark) return;

            void Set(string key, string hex) =>
                Resources[key] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
            Set("Surface", "#202225");
            Set("Ink", "#F2F3F5");
            Set("Body", "#C8CCD2");
            Set("Muted", "#959BA4");
            Set("Line", "#33363B");
            Set("Track", "#33363B");
            Set("Hover", "#2C2F33");
            Set("Accent", "#2F80D6");
            Set("AccentHover", "#3D8EE3");
            Resources["ShadowOpacity"] = 0.45;
        }

        static string Quote(string s) => s.Contains(" ") && !s.StartsWith("\"") ? "\"" + s + "\"" : s;
    }
}
