using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DoisD.Updater
{
    public partial class MainWindow : Window
    {
        enum View { Available, Progress, Done, UpToDate, Error }

        readonly LaunchMode _mode;
        readonly UpdateFeed _feed;
        readonly Version _current;
        View _view;
        CancellationTokenSource _cts;
        static readonly CultureInfo Pt = new CultureInfo("pt-BR");
        static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        public MainWindow(LaunchMode mode, UpdateFeed feed, Version current, bool hasUpdate, Exception error)
        {
            InitializeComponent();
            _mode = mode;
            _feed = feed;
            _current = current;
            Loaded += (s, e) => AnimateIn();
            SizeChanged += (s, e) => Recenter();

            if (feed == null) ShowError("Não foi possível verificar", "Confira sua conexão com a internet e tente novamente.");
            else if (!hasUpdate) ShowUpToDate();
            else ShowAvailable();
        }

        string NewVersion => UpdateService.Pretty(UpdateService.ParseVersion(_feed.Version));

        // ------------------------------------------------------------------ estados
        void ShowAvailable()
        {
            _view = View.Available;
            TitleText.Text = _feed.Mandatory ? "Atualização importante" : "Nova versão disponível";
            SubtitleText.Text = $"Versão {NewVersion}" + (string.IsNullOrWhiteSpace(_feed.Headline) ? "" : " · " + _feed.Headline);
            BuildNotes(_feed.Notes, 3);
            var asset = UpdateService.IsPortable ? _feed.Portable : _feed.Setup;
            MetaText.Text = asset != null && asset.Size > 0 ? UpdateService.HumanSize(asset.Size) : "";
            ProgressPanel.Visibility = Visibility.Collapsed;
            SetButtons(_feed.Mandatory ? null : "Agora não", "Atualizar");
            Transition();
        }

        void ShowProgress()
        {
            _view = View.Progress;
            TitleText.Text = $"Atualizando para {NewVersion}";
            SubtitleText.Text = "Leva poucos segundos. O 2D Editor será reaberto em seguida.";
            NotesScroll.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Visible;
            MetaText.Text = "";
            Bar.Width = 0;
            PercentText.Text = "0%";
            SetButtons("Cancelar", null);
            Transition();
        }

        void ShowDone()
        {
            _view = View.Done;
            TitleText.Text = "Atualização concluída";
            SubtitleText.Text = $"Você está usando a versão {NewVersion}.";
            ProgressPanel.Visibility = Visibility.Collapsed;
            BuildNotes(_feed.Notes, 4, "O que mudou");
            MetaText.Text = "";
            SetButtons(null, "Concluir");
            Transition();
        }

        void ShowUpToDate()
        {
            _view = View.UpToDate;
            TitleText.Text = "Você está em dia";
            SubtitleText.Text = $"O 2D Editor {UpdateService.Pretty(_current)} é a versão mais recente.";
            NotesScroll.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;
            MetaText.Text = "";
            SetButtons(null, "Ok");
            Transition();
        }

        void ShowError(string title, string text)
        {
            _view = View.Error;
            TitleText.Text = title;
            SubtitleText.Text = text;
            NotesScroll.Visibility = Visibility.Collapsed;
            ProgressPanel.Visibility = Visibility.Collapsed;
            MetaText.Text = "";
            bool canRetry = _feed != null && UpdateService.ParseVersion(_feed.Version) > _current;
            SetButtons(canRetry ? "Agora não" : null, canRetry ? "Tentar de novo" : "Fechar");
            Transition();
        }

        void SetButtons(string secondary, string primary)
        {
            SecondaryButton.Visibility = secondary == null ? Visibility.Collapsed : Visibility.Visible;
            SecondaryButton.Content = secondary;
            PrimaryButton.Visibility = primary == null ? Visibility.Collapsed : Visibility.Visible;
            PrimaryButton.Content = primary;
        }

        /// <summary>Lista enxuta: agrupa por tipo, com rótulo discreto e marcador fino.</summary>
        void BuildNotes(List<ReleaseNote> notes, int max, string heading = null)
        {
            NotesPanel.Children.Clear();
            var items = (notes ?? new List<ReleaseNote>()).ToList();
            if (items.Count == 0) { NotesScroll.Visibility = Visibility.Collapsed; return; }

            string Group(ReleaseNote n)
            {
                var t = (n.Type ?? "").ToLowerInvariant();
                return t.StartsWith("corre") || t == "fix" ? "Correções" : t.StartsWith("melhor") ? "Melhorias" : "Novidades";
            }
            var order = new[] { "Novidades", "Melhorias", "Correções" };
            var shown = items.Take(max).ToList();
            bool multi = shown.Select(Group).Distinct().Count() > 1;

            if (heading != null) NotesPanel.Children.Add(Label(heading, 0));
            int i = 0;
            foreach (var g in order)
            {
                var inGroup = shown.Where(n => Group(n) == g).ToList();
                if (inGroup.Count == 0) continue;
                foreach (var n in inGroup) { NotesPanel.Children.Add(Line(n.Text)); i++; }
            }
            int rest = items.Count - shown.Count;
            if (rest > 0)
                NotesPanel.Children.Add(new TextBlock
                {
                    Text = rest == 1 ? "e mais 1 mudança" : $"e mais {rest} mudanças",
                    Foreground = (Brush)FindResource("Muted"), FontSize = 12.5, Margin = new Thickness(14, 2, 0, 0)
                });
            NotesScroll.Visibility = Visibility.Visible;
        }

        TextBlock Label(string text, double top) => new TextBlock
        {
            Text = text, Foreground = (Brush)FindResource("Muted"), FontSize = 12, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, top, 0, 6)
        };

        FrameworkElement Line(string text)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var dot = new Ellipse { Width = 4, Height = 4, Fill = (Brush)FindResource("Muted"), Opacity = 0.7,
                                    VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left,
                                    Margin = new Thickness(1, 8, 0, 0) };
            var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 19,
                                     Foreground = (Brush)FindResource("Body") };
            Grid.SetColumn(tb, 1);
            g.Children.Add(dot);
            g.Children.Add(tb);
            return g;
        }

        // ------------------------------------------------------------------ movimento
        void Recenter()
        {
            var wa = SystemParameters.WorkArea;
            Left = wa.Left + (wa.Width - ActualWidth) / 2;
            Top = wa.Top + (wa.Height - ActualHeight) / 2;
        }

        void AnimateIn()
        {
            Recenter();
            Root.Opacity = 0;
            Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = Ease });
            var s = new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = Ease };
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, s);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, s);
        }

        void Transition()
        {
            if (!IsLoaded) return;
            Body.Opacity = 0;
            Body.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)) { EasingFunction = Ease });
            ContentShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ease });
        }

        void SetProgress(double fraction, string status)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            if (status != null) StatusText.Text = status;
            PercentText.Text = ((int)Math.Round(fraction * 100)).ToString(Pt) + "%";
            Bar.BeginAnimation(WidthProperty, new DoubleAnimation(Track.ActualWidth * fraction, TimeSpan.FromMilliseconds(260)) { EasingFunction = Ease });
        }

        void FadeOutAndClose()
        {
            var a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160));
            a.Completed += (s, e) => Close();
            Root.BeginAnimation(OpacityProperty, a);
            var s2 = new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(160));
            RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, s2);
            RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, s2);
        }

        // ------------------------------------------------------------------ fluxo
        async Task RunUpdateAsync()
        {
            bool portable = UpdateService.IsPortable;
            var asset = portable ? _feed.Portable : _feed.Setup;
            if (asset == null || string.IsNullOrEmpty(asset.Url))
            {
                ShowError("Pacote indisponível", "Esta versão ainda não tem arquivo para o seu tipo de instalação.");
                return;
            }

            ShowProgress();
            await Task.Delay(120);
            _cts = new CancellationTokenSource();
            string file;

            try
            {
                SetProgress(0, "Baixando");
                var prog = new Progress<DownloadProgress>(p =>
                {
                    double f = p.Total > 0 ? (double)p.Received / p.Total : 0;
                    SetProgress(f * 0.85, p.Total > 0
                        ? $"Baixando · {UpdateService.HumanSize(p.Received)} de {UpdateService.HumanSize(p.Total)}"
                        : "Baixando");
                });
                file = await UpdateService.DownloadAsync(asset, _feed.Version, prog, _cts.Token);
            }
            catch (OperationCanceledException) { FadeOutAndClose(); return; }
            catch (Exception ex)
            {
                UpdateService.Log("Download falhou: " + ex.Message);
                ShowError("O download não foi concluído", "Verifique sua conexão com a internet e tente novamente.");
                return;
            }

            SecondaryButton.Visibility = Visibility.Collapsed;
            SetProgress(0.88, "Verificando o arquivo");
            bool ok = await Task.Run(() => UpdateService.VerifySha256(file, asset.Sha256));
            await Task.Delay(250);
            if (!ok)
            {
                ShowError("Arquivo inválido", "O arquivo baixado não passou na verificação de segurança. Tente novamente.");
                return;
            }

            SetProgress(0.9, "Fechando o 2D Editor");
            bool wasRunning = UpdateService.IsEditorRunning;
            var closeTask = UpdateService.CloseEditorAsync(TimeSpan.FromMinutes(3), CancellationToken.None);
            if (await Task.WhenAny(closeTask, Task.Delay(1500)) != closeTask)
                SubtitleText.Text = "Se houver arquivos não salvos, o 2D Editor vai perguntar antes de fechar.";
            if (!await closeTask)
            {
                ShowError("O 2D Editor continua aberto", "Salve seus arquivos, feche o 2D Editor e tente de novo.");
                return;
            }

            SubtitleText.Text = "Leva poucos segundos. O 2D Editor será reaberto em seguida.";
            SetProgress(0.93, "Instalando");
            try
            {
                if (portable) await UpdateService.ApplyPortableAsync(file);
                else
                {
                    int code = await UpdateService.RunInstallerAsync(file);
                    if (code != 0) throw new Exception("código de saída " + code);
                }
            }
            catch (Exception ex)
            {
                UpdateService.Log("Instalação falhou: " + ex.Message);
                if (wasRunning) UpdateService.LaunchEditor();
                ShowError("A instalação não foi concluída",
                    ex is System.ComponentModel.Win32Exception ? "A permissão de administrador não foi concedida." : "Tente novamente em instantes.");
                return;
            }

            SetProgress(1, "Abrindo o 2D Editor");
            UpdateService.LaunchEditor();
            await Task.Delay(700);

            var st = UpdateService.LoadState();
            st.SkippedVersion = null;
            UpdateService.SaveState(st);
            ShowDone();
            Activate();
        }

        /// <summary>Uso interno: salva o cartão como PNG (2x) sobre um fundo neutro.</summary>
        public void SaveSnapshot(string path, bool transparent = false)
        {
            const double scale = 2.0;
            int w = (int)Math.Ceiling(ActualWidth * scale), h = (int)Math.Ceiling(ActualHeight * scale);
            var content = (FrameworkElement)this.Content;
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bool dark = ((SolidColorBrush)FindResource("Surface")).Color.R < 128;
            var bg = new DrawingVisual();
            using (var dc = bg.RenderOpen())
                dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(0x14, 0x15, 0x17) : Color.FromRgb(0xEE, 0xF0, 0xF3)), null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (!transparent) rtb.Render(bg);
            rtb.Render(content);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(path)) enc.Save(fs);
        }

        /// <summary>Uso interno (capturas de tela): mostra um estado com dados de exemplo.</summary>
        public void Preview(string state)
        {
            switch (state)
            {
                case "progress":
                    ShowProgress();
                    Dispatcher.BeginInvoke(new Action(() => SetProgress(0.46, "Baixando · 3,4 MB de 7,4 MB")), System.Windows.Threading.DispatcherPriority.Loaded);
                    break;
                case "done": ShowDone(); break;
                case "uptodate": ShowUpToDate(); break;
                case "error": ShowError("O download não foi concluído", "Verifique sua conexão com a internet e tente novamente."); break;
            }
        }

        // ------------------------------------------------------------------ botões
        async void Primary_Click(object sender, RoutedEventArgs e)
        {
            if (_view == View.Available || (_view == View.Error && PrimaryButton.Content as string == "Tentar de novo"))
                await RunUpdateAsync();
            else
                FadeOutAndClose();
        }

        void Secondary_Click(object sender, RoutedEventArgs e)
        {
            if (_view == View.Progress) { _cts?.Cancel(); return; }
            FadeOutAndClose();
        }

        void Later_Click(object sender, RoutedEventArgs e)
        {
            if (_view == View.Progress && SecondaryButton.Visibility != Visibility.Visible) return; // não interrompe a instalação
            _cts?.Cancel();
            FadeOutAndClose();
        }

        void Card_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && !(e.OriginalSource is Button)) { try { DragMove(); } catch { } }
        }
    }
}
