using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Windows.Media;

namespace DoisD.Updater
{
    /// <summary>Conteúdo do update.json publicado em cada versão.</summary>
    [DataContract]
    public class UpdateFeed
    {
        [DataMember(Name = "version")] public string Version { get; set; }
        [DataMember(Name = "date")] public string Date { get; set; }
        [DataMember(Name = "headline")] public string Headline { get; set; }
        [DataMember(Name = "notes")] public List<ReleaseNote> Notes { get; set; }
        [DataMember(Name = "setup")] public UpdateAsset Setup { get; set; }
        [DataMember(Name = "portable")] public UpdateAsset Portable { get; set; }
        [DataMember(Name = "mandatory")] public bool Mandatory { get; set; }
    }

    [DataContract]
    public class ReleaseNote
    {
        [DataMember(Name = "type")] public string Type { get; set; }
        [DataMember(Name = "text")] public string Text { get; set; }
    }

    [DataContract]
    public class UpdateAsset
    {
        [DataMember(Name = "url")] public string Url { get; set; }
        [DataMember(Name = "sha256")] public string Sha256 { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
    }

    /// <summary>Estado local do atualizador (%LOCALAPPDATA%\2D Editor\updater.json).</summary>
    [DataContract]
    public class UpdaterState
    {
        [DataMember(Name = "lastCheckUtc")] public string LastCheckUtc { get; set; }
        [DataMember(Name = "skippedVersion")] public string SkippedVersion { get; set; }
    }

    /// <summary>Item da lista "O que há de novo" já pronto para exibir.</summary>
    public class NoteView
    {
        public string Label { get; set; }
        public Brush Accent { get; set; }
        public Brush Tint { get; set; }
        public string Text { get; set; }

        static Brush B(string hex) => (Brush)new BrushConverter().ConvertFromString(hex);

        public static NoteView From(ReleaseNote n)
        {
            var t = (n.Type ?? "").Trim().ToLowerInvariant();
            if (t.StartsWith("corre") || t == "fix")
                return new NoteView { Label = "CORREÇÃO", Accent = B("#B7791F"), Tint = B("#FDF3DD"), Text = n.Text };
            if (t.StartsWith("melhor") || t == "improvement")
                return new NoteView { Label = "MELHORIA", Accent = B("#1F8A5B"), Tint = B("#E3F5EC"), Text = n.Text };
            return new NoteView { Label = "NOVO", Accent = B("#005AAB"), Tint = B("#E3EEFA"), Text = n.Text };
        }
    }

    public enum StepState { Pending, Active, Done, Failed }

    /// <summary>Etapa do processo de atualização mostrada ao vivo.</summary>
    public class StepItem : INotifyPropertyChanged
    {
        string _detail;
        StepState _state;
        public string Title { get; set; }
        public string Detail { get => _detail; set { _detail = value; Raise(nameof(Detail)); } }
        public StepState State { get => _state; set { _state = value; Raise(nameof(State)); } }
        public event PropertyChangedEventHandler PropertyChanged;
        void Raise(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
