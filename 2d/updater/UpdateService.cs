using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DoisD.Updater
{
    public struct DownloadProgress
    {
        public long Received;
        public long Total;
        public double BytesPerSecond;
    }

    /// <summary>Toda a lógica de atualização do 2D Editor (sem interface).</summary>
    public static class UpdateService
    {
        public const string DefaultFeed = "https://github.com/alanaraujo-bit/2d-editor/releases/latest/download/update.json";
        public const string EditorExeName = "2DEditor.exe";
        public const string EditorWindowClass = "2D Editor";

        public static string AppDir { get; set; }
        public static string FeedUrl { get; set; } = DefaultFeed;

        public static string EditorExe => Path.Combine(AppDir, EditorExeName);
        public static bool IsPortable => File.Exists(Path.Combine(AppDir, "doLocalConf.xml"));

        public static bool IsAllUsers
        {
            get
            {
                string dir = Path.GetFullPath(AppDir).TrimEnd('\\') + "\\";
                foreach (var sf in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
                {
                    string pf = Environment.GetFolderPath(sf);
                    if (!string.IsNullOrEmpty(pf) && dir.StartsWith(pf.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
            }
        }

        static string DataDir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "2D Editor");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(DataDir, "updater.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}\r\n"); }
            catch { }
        }

        // ------------------------------------------------------------- versões
        public static Version CurrentVersion
        {
            get
            {
                try
                {
                    var fvi = FileVersionInfo.GetVersionInfo(EditorExe);
                    return ParseVersion(fvi.ProductVersion) ?? new Version(fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart);
                }
                catch { return new Version(0, 0, 0); }
            }
        }

        public static Version ParseVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().TrimStart('v', 'V').Replace(',', '.').Replace(" ", "");
            var parts = s.Split('.').Select(p => new string(p.TakeWhile(char.IsDigit).ToArray())).Where(p => p.Length > 0).ToArray();
            if (parts.Length == 0) return null;
            while (parts.Length < 3) parts = parts.Concat(new[] { "0" }).ToArray();
            return Version.TryParse(string.Join(".", parts.Take(4)), out var v) ? v : null;
        }

        public static string Pretty(Version v) => v == null ? "?" : (v.Revision > 0 ? v.ToString(4) : v.ToString(3));

        // ------------------------------------------------------------- estado
        static string StatePath => Path.Combine(DataDir, "updater.json");

        public static UpdaterState LoadState()
        {
            try
            {
                using (var fs = File.OpenRead(StatePath))
                    return (UpdaterState)new DataContractJsonSerializer(typeof(UpdaterState)).ReadObject(fs);
            }
            catch { return new UpdaterState(); }
        }

        public static void SaveState(UpdaterState s)
        {
            try
            {
                using (var fs = File.Create(StatePath))
                    new DataContractJsonSerializer(typeof(UpdaterState)).WriteObject(fs, s);
            }
            catch { }
        }

        // ------------------------------------------------------------- rede
        static HttpClient _http;
        static HttpClient Http
        {
            get
            {
                if (_http == null)
                {
                    ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.3
                    _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate });
                    _http.Timeout = TimeSpan.FromMinutes(30);
                    _http.DefaultRequestHeaders.UserAgent.ParseAdd("2DEditor-Updater/1.0");
                }
                return _http;
            }
        }

        static bool IsLocal(string url, out string path)
        {
            path = null;
            if (url.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) { path = new Uri(url).LocalPath; return true; }
            if (url.Length > 2 && (url[1] == ':' || url.StartsWith("\\\\"))) { path = url; return true; }
            return false;
        }

        public static async Task<UpdateFeed> FetchFeedAsync(CancellationToken ct)
        {
            byte[] data;
            if (IsLocal(FeedUrl, out var local))
                data = File.ReadAllBytes(local);
            else
            {
                string url = FeedUrl + (FeedUrl.Contains("?") ? "&" : "?") + "t=" + DateTime.UtcNow.Ticks;
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromSeconds(25));
                    var resp = await Http.GetAsync(url, cts.Token).ConfigureAwait(false);
                    resp.EnsureSuccessStatusCode();
                    data = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
            using (var ms = new MemoryStream(data))
            {
                var feed = (UpdateFeed)new DataContractJsonSerializer(typeof(UpdateFeed)).ReadObject(ms);
                if (feed == null || ParseVersion(feed.Version) == null) throw new InvalidDataException("update.json inválido");
                feed.Notes = feed.Notes ?? new List<ReleaseNote>();
                return feed;
            }
        }

        public static async Task<string> DownloadAsync(UpdateAsset asset, string version, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            string dir = Path.Combine(Path.GetTempPath(), "2DEditorUpdate", version);
            Directory.CreateDirectory(dir);
            string name = Path.GetFileName(new Uri(asset.Url, UriKind.RelativeOrAbsolute).IsAbsoluteUri ? new Uri(asset.Url).LocalPath : asset.Url);
            if (string.IsNullOrEmpty(name)) name = "2D-Editor-Setup.exe";
            string target = Path.Combine(dir, name);

            if (IsLocal(asset.Url, out var local))
            {
                long total = new FileInfo(local).Length;
                using (var src = File.OpenRead(local))
                using (var dst = File.Create(target))
                    await CopyWithProgress(src, dst, total, progress, ct);
            }
            else
            {
                var resp = await Http.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? asset.Size;
                using (var src = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var dst = File.Create(target))
                    await CopyWithProgress(src, dst, total, progress, ct);
            }
            return target;
        }

        static async Task CopyWithProgress(Stream src, Stream dst, long total, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            var buf = new byte[81920];
            long got = 0;
            var sw = Stopwatch.StartNew();
            long lastReport = 0;
            int n;
            while ((n = await src.ReadAsync(buf, 0, buf.Length, ct).ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buf, 0, n, ct).ConfigureAwait(false);
                got += n;
                if (sw.ElapsedMilliseconds - lastReport > 80 || got == total)
                {
                    lastReport = sw.ElapsedMilliseconds;
                    progress?.Report(new DownloadProgress { Received = got, Total = total, BytesPerSecond = got / Math.Max(0.001, sw.Elapsed.TotalSeconds) });
                }
            }
            progress?.Report(new DownloadProgress { Received = got, Total = Math.Max(total, got), BytesPerSecond = got / Math.Max(0.001, sw.Elapsed.TotalSeconds) });
        }

        public static bool VerifySha256(string file, string expected)
        {
            if (string.IsNullOrWhiteSpace(expected)) return true;
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(file))
            {
                string h = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "");
                return string.Equals(h, expected.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        // ------------------------------------------------------------- editor
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        delegate bool EnumWindowsProc(IntPtr h, IntPtr l);

        static List<IntPtr> EditorWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows((h, l) =>
            {
                var sb = new StringBuilder(64);
                GetClassName(h, sb, sb.Capacity);
                if (sb.ToString() == EditorWindowClass) list.Add(h);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        static Process[] EditorProcesses() =>
            Process.GetProcessesByName(Path.GetFileNameWithoutExtension(EditorExeName)).Where(p =>
            {
                try { return string.Equals(Path.GetDirectoryName(p.MainModule.FileName), Path.GetFullPath(AppDir).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
                catch { return true; }
            }).ToArray();

        public static bool IsEditorRunning => EditorProcesses().Length > 0;

        /// <summary>Pede ao editor para fechar (ele pergunta se há arquivos não salvos).</summary>
        public static async Task<bool> CloseEditorAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (!IsEditorRunning) return true;
            foreach (var h in EditorWindows())
            {
                SetForegroundWindow(h);
                PostMessage(h, 0x0010 /*WM_CLOSE*/, IntPtr.Zero, IntPtr.Zero);
            }
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsEditorRunning) return true;
                await Task.Delay(300, ct);
            }
            return !IsEditorRunning;
        }

        public static void LaunchEditor()
        {
            try { Process.Start(new ProcessStartInfo(EditorExe) { UseShellExecute = true, WorkingDirectory = AppDir }); }
            catch (Exception e) { Log("Falha ao reabrir o editor: " + e.Message); }
        }

        // ------------------------------------------------------------- instalação
        public static Task<int> RunInstallerAsync(string setupPath)
        {
            return Task.Run(() =>
            {
                string mode = IsAllUsers ? "/ALLUSERS" : "/CURRENTUSER";
                string args = $"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS {mode} /DIR=\"{AppDir.TrimEnd('\\')}\" /LOG=\"{Path.Combine(DataDir, "setup.log")}\"";
                var psi = new ProcessStartInfo(setupPath, args) { UseShellExecute = true };
                if (IsAllUsers) psi.Verb = "runas";
                Log("Instalador: " + setupPath + " " + args);
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                    Log("Instalador terminou com código " + p.ExitCode);
                    return p.ExitCode;
                }
            });
        }

        static readonly HashSet<string> KeepFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "config.xml", "session.xml", "shortcuts.xml", "stylers.xml", "langs.xml", "contextMenu.xml",
            "nativeLang.xml", "toolbarButtonsConf.xml", "tabContextMenu.xml", "functionList.xml"
        };

        /// <summary>Versão portátil: troca os arquivos do programa preservando as configurações.</summary>
        public static Task ApplyPortableAsync(string zipPath)
        {
            return Task.Run(() =>
            {
                string tmp = Path.Combine(Path.GetTempPath(), "2DEditorUpdate", "portable-" + Guid.NewGuid().ToString("N"));
                ZipFile.ExtractToDirectory(zipPath, tmp);
                string root = Directory.GetFiles(tmp, EditorExeName, SearchOption.AllDirectories).Select(Path.GetDirectoryName).FirstOrDefault()
                              ?? throw new InvalidDataException("Pacote portátil sem " + EditorExeName);
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string rel = file.Substring(root.Length).TrimStart('\\');
                    string dest = Path.Combine(AppDir, rel);
                    if (KeepFiles.Contains(rel) && File.Exists(dest)) continue;
                    if (rel.StartsWith("backup\\", StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    for (int i = 0; ; i++)
                    {
                        try { File.Copy(file, dest, true); break; }
                        catch (IOException) when (i < 20) { Thread.Sleep(250); }
                    }
                }
                try { Directory.Delete(tmp, true); } catch { }
            });
        }

        public static string HumanSize(double bytes)
        {
            string[] u = { "B", "KB", "MB", "GB" };
            int i = 0;
            while (bytes >= 1024 && i < u.Length - 1) { bytes /= 1024; i++; }
            return bytes.ToString(i == 0 ? "0" : "0.0", new System.Globalization.CultureInfo("pt-BR")) + " " + u[i];
        }
    }
}
