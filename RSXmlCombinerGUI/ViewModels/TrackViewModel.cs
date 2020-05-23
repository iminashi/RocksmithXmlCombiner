
using DynamicData.Binding;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Rocksmith2014Xml;

using RSXmlCombinerGUI.Extensions;
using RSXmlCombinerGUI.Models;

using Splat;

using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;

#pragma warning disable RCS1090 // Call 'ConfigureAwait(false)'.

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class TrackViewModel : ViewModelBase
    {
        public Subject<string> Messages { get; } = new Subject<string>();

        public TrackListViewModel Parent { get; }

        [Reactive]
        public string Title { get; set; }

        [Reactive]
        public float TrimAmount { get; set; }

        [Reactive]
        public string? AudioFile { get; set; }

        public float SongLength { get; set; }

        public ObservableCollectionExtended<ArrangementViewModel> Arrangements { get; } = new ObservableCollectionExtended<ArrangementViewModel>();

        public ReactiveCommand<Unit, Unit> OpenAudio { get; }

        // Constructor for the designer
        public TrackViewModel() : this("Title", 10f, 200f, new TrackListViewModel())
        {
            Arrangements.Add(new ArrangementViewModel(ArrangementType.Lead, this));
            Arrangements.Add(new ArrangementViewModel(ArrangementType.Combo, this));
            Arrangements.Add(new ArrangementViewModel(ArrangementType.JVocals, this));
        }

        public TrackViewModel(string? title, float trimAmount, float songLength, TrackListViewModel parent)
        {
            Parent = parent;
            Title = title ?? "N/A";
            TrimAmount = trimAmount;
            SongLength = songLength;

            OpenAudio = ReactiveCommand.CreateFromTask(OpenAudioImpl);
        }

        public TrackViewModel(TrackDto dto, TrackListViewModel parent)
        {
            Parent = parent;

            Title = dto.Title;
            SongLength = dto.SongLength;
            TrimAmount = dto.TrimAmount;
            AudioFile = dto.AudioFile;
            Arrangements.Load(dto.Arrangements.Select(a => new ArrangementViewModel(a, this)));

            OpenAudio = ReactiveCommand.CreateFromTask(OpenAudioImpl);
        }

        public void SetArrangement(RS2014Song arrangement, string fileName)
        {
            if (arrangement.ArrangementProperties.PathLead == 1)
                Arrangements.Add(new ArrangementViewModel(new InstrumentalArrangement(fileName, ArrangementType.Lead), this));
            else if (arrangement.ArrangementProperties.PathRhythm == 1)
                Arrangements.Add(new ArrangementViewModel(new InstrumentalArrangement(fileName, ArrangementType.Rhythm), this));
            else if (arrangement.ArrangementProperties.PathBass == 1)
                Arrangements.Add(new ArrangementViewModel(new InstrumentalArrangement(fileName, ArrangementType.Bass), this));
            else
                Messages.OnNext("Could not determine arrangement type from metadata for file " + Path.GetFileName(fileName));
        }

        public void AddNewArrangement(ArrangementType arrangementType, string fileName, string? baseTone)
        {
            if (arrangementType.IsInstrumental())
            {
                var arr = new InstrumentalArrangement(fileName, arrangementType);
                if (baseTone != null)
                    arr.BaseTone = baseTone;
                Arrangements.Add(new ArrangementViewModel(arr, this));
            }
            else if (arrangementType.IsVocals())
            {
                Arrangements.Add(new ArrangementViewModel(new VocalsArrangement(fileName, ArrangementType.Vocals), this));
            }
            else if (arrangementType.Is(ArrangementType.ShowLights))
            {
                Arrangements.Add(new ArrangementViewModel(new ShowLightsArrangement(fileName), this));
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
