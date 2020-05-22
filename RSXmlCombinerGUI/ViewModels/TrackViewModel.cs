using Avalonia.Media;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Rocksmith2014Xml;

using RSXmlCombinerGUI.Models;

using Splat;

using System;
using System.IO;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

using XmlUtils;

#pragma warning disable RCS1090 // Call 'ConfigureAwait(false)'.

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class TrackViewModel : ViewModelBase
    {
        public Subject<string> Messages { get; } = new Subject<string>();

        [Reactive]
        public string Title { get; set; }

        [Reactive]
        public float TrimAmount { get; set; }

        [Reactive]
        public string? AudioFile { get; set; }

        public float SongLength { get; set; }

        [Reactive]
        public InstrumentalArrangement? LeadArrangement { get; set; }

        [Reactive]
        public InstrumentalArrangement? RhythmArrangement { get; set; }

        [Reactive]
        public InstrumentalArrangement? BassArrangement { get; set; }

        [Reactive]
        public string? VocalsArrangement { get; set; }

        [Reactive]
        public string? ShowLightsArrangement { get; set; }

        public ReactiveCommand<Unit, Unit> OpenAudio { get; private set; }
        public ReactiveCommand<ArrangementType, Unit> AddArrangement { get; private set; }

        public IObservable<ISolidColorBrush> LeadColor { get; private set; }
        public IObservable<ISolidColorBrush> RhythmColor { get; private set; }
        public IObservable<ISolidColorBrush> BassColor { get; private set; }
        public IObservable<ISolidColorBrush> VocalsColor { get; private set; }
        public IObservable<ISolidColorBrush> ShowLightsColor { get; private set; }
        public IObservable<string?> LeadFileNameShort { get; private set; }
        public IObservable<string?> RhythmFileNameShort { get; private set; }
        public IObservable<string?> BassFileNameShort { get; private set; }
        public IObservable<string?> VocalsFileNameShort { get; private set; }
        public IObservable<string?> ShowLightsFileNameShort { get; private set; }

        // Constructor for the designer
        public TrackViewModel() : this("Title", 10f, 200f) { }

        public TrackViewModel(string? title, float trimAmount, float songLength)
        {
            Title = title ?? "N/A";
            TrimAmount = trimAmount;
            SongLength = songLength;

            CreateObservables();
        }

        public TrackViewModel(RS2014Song arrangement, string fileName)
        {
            SetArrangement(arrangement, fileName);

            SongLength = arrangement.SongLength;
            Title = arrangement.Title ?? "N/A";
            TrimAmount = arrangement.StartBeat;

            CreateObservables();
        }

        public TrackViewModel(TrackDto dto)
        {
            Title = dto.Title;
            SongLength = dto.SongLength;
            TrimAmount = dto.TrimAmount;
            AudioFile = dto.AudioFile;
            LeadArrangement = dto.LeadArrangement;
            LeadArrangement?.UpdateTones();
            RhythmArrangement = dto.RhythmArrangement;
            RhythmArrangement?.UpdateTones();
            BassArrangement = dto.BassArrangement;
            BassArrangement?.UpdateTones();
            VocalsArrangement = dto.VocalsArrangement;
            ShowLightsArrangement = dto.ShowLightsArrangement;

            CreateObservables();
        }

        private void CreateObservables()
        {
            LeadColor = CreateColorObservable(x => x.LeadArrangement, Brushes.Orange);
            RhythmColor = CreateColorObservable(x => x.RhythmArrangement, Brushes.Green);
            BassColor = CreateColorObservable(x => x.BassArrangement, Brushes.Blue);
            VocalsColor = CreateColorObservable(x => x.VocalsArrangement, Brushes.DarkRed);
            ShowLightsColor = CreateColorObservable(x => x.ShowLightsArrangement, Brushes.DarkViolet);

            LeadFileNameShort = this.WhenAnyValue(x => x.LeadArrangement)
                .Where(x => !(x is null))
                .Select(x => x!.FileName)
                .Select(Path.GetFileNameWithoutExtension);

            RhythmFileNameShort = this.WhenAnyValue(x => x.RhythmArrangement)
                .Where(x => !(x is null))
                .Select(x => x!.FileName)
                .Select(Path.GetFileNameWithoutExtension);

            BassFileNameShort = this.WhenAnyValue(x => x.BassArrangement)
                .Where(x => !(x is null))
                .Select(x => x!.FileName)
                .Select(Path.GetFileNameWithoutExtension);

            VocalsFileNameShort = this.WhenAnyValue(x => x.VocalsArrangement)
                .Where(fn => !string.IsNullOrEmpty(fn))
                .Select(Path.GetFileNameWithoutExtension);

            ShowLightsFileNameShort = this.WhenAnyValue(x => x.ShowLightsArrangement)
                .Where(fn => !string.IsNullOrEmpty(fn))
                .Select(Path.GetFileNameWithoutExtension);

            OpenAudio = ReactiveCommand.CreateFromTask(OpenAudioImpl);
            AddArrangement = ReactiveCommand.CreateFromTask<ArrangementType>(AddArrangementImpl);
        }

        private IObservable<ISolidColorBrush> CreateColorObservable(Expression<Func<TrackViewModel, object?>> prop, ISolidColorBrush brush)
            => this.WhenAnyValue(prop)
                   .Select(a => a is null ? Brushes.Gray : brush);

        public void SetArrangement(RS2014Song arrangement, string fileName)
        {
            if (arrangement.ArrangementProperties.PathLead == 1)
                LeadArrangement = new InstrumentalArrangement(fileName);
            else if (arrangement.ArrangementProperties.PathRhythm == 1)
                RhythmArrangement = new InstrumentalArrangement(fileName);
            else if (arrangement.ArrangementProperties.PathBass == 1)
                BassArrangement = new InstrumentalArrangement(fileName);
            else
                Messages.OnNext("Could not determine arrangement type from metadata for file " + Path.GetFileName(fileName));
        }

        private async Task AddArrangementImpl(ArrangementType arrangementType)
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            string[] files = await dialogs
                .OpenFileDialog("Select RS2014 Arrangement", DialogServices.RocksmithXmlFileFilters);

            if (files.Length > 0)
            {
                string fileName = files[0];

                if (!XmlHelper.ValidateRootElement(fileName, arrangementType.ToXmlRootElement()))
                    Messages.OnNext("The XML file does not match the arrangement type!");
                //else if (arrangementType.IsInstrumental() && fileName.GetDifficultyLevels() != 1)
                //    Messages.OnNext("The XML file contains DD levels.");
                else
                    AddNewArrangement(arrangementType, fileName);
            }
        }

        public void AddNewArrangement(ArrangementType arrangementType, string fileName, string? baseTone = null)
        {
            switch (arrangementType)
            {
                case ArrangementType.Lead:
                    LeadArrangement = new InstrumentalArrangement(fileName);
                    if (baseTone != null)
                        LeadArrangement.BaseTone = baseTone;
                    break;
                case ArrangementType.Rhythm:
                    RhythmArrangement = new InstrumentalArrangement(fileName);
                    if (baseTone != null)
                        RhythmArrangement.BaseTone = baseTone;
                    break;
                case ArrangementType.Bass:
                    BassArrangement = new InstrumentalArrangement(fileName);
                    if (baseTone != null)
                        BassArrangement.BaseTone = baseTone;
                    break;
                case ArrangementType.Vocals:
                    VocalsArrangement = fileName;
                    break;
                case ArrangementType.ShowLights:
                    ShowLightsArrangement = fileName;
                    break;
            }
        }

        private async Task OpenAudioImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            var files = await dialogs
                .OpenFileDialog("Select Audio File", DialogServices.AudioFileFiltersOpen);

            if (files.Length > 0)
                AudioFile = files[0];
        }
    }
}
