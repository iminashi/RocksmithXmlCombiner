using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using RSXmlCombinerGUI.Extensions;
using RSXmlCombinerGUI.Models;

using Splat;

using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;

using XmlUtils;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class ArrangementViewModel : ViewModelBase
    {
        [Reactive]
        public Arrangement? Model { get; private set; }
        [Reactive]
        public ArrangementToneControlsViewModel ToneControls { get; set; }
        public string Name { get; }
        public ArrangementType ArrangementType { get; }
        public TrackViewModel Parent { get; }

        public ReactiveCommand<Unit, Unit> SelectArrangement { get; }

        // Designer constructor
        public ArrangementViewModel() : this(new InstrumentalArrangement("C:\\test.xml", ArrangementType.Bass), new TrackViewModel())
        {
        }

        public ArrangementViewModel(Arrangement model, TrackViewModel parent)
        {
            Parent = parent;
            Model = model;
            Name = Model.ArrangementType.ToString();
            ArrangementType = Model.ArrangementType;

            if (Model is InstrumentalArrangement instArr)
            {
                ToneControls = new ArrangementToneControlsViewModel(instArr, Parent.Parent.Tracks, Parent);
            }

            SelectArrangement = ReactiveCommand.CreateFromTask(SelectArrangementImpl);
        }

        public ArrangementViewModel(ArrangementType arrangementType, TrackViewModel parent)
        {
            Parent = parent;
            Name = arrangementType.ToString();
            ArrangementType = arrangementType;

            SelectArrangement = ReactiveCommand.CreateFromTask(SelectArrangementImpl);
        }

        private async Task SelectArrangementImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            string[] files = await dialogs
                .OpenFileDialog("Select RS2014 Arrangement", DialogServices.RocksmithXmlFileFilters);

            if (files.Length > 0)
            {
                string fileName = files[0];

                if (!XmlHelper.ValidateRootElement(fileName, ArrangementType.ToXmlRootElement()))
                    Debug.WriteLine("The XML file does not match the arrangement type!");
                else
                    SetModel(fileName);
            }
        }

        public void SetModel(string fileName)
        {
            if (ArrangementType.IsInstrumental())
                Model = new InstrumentalArrangement(fileName, ArrangementType);
            else if (ArrangementType.IsVocals())
                Model = new VocalsArrangement(fileName, ArrangementType);
            else if (ArrangementType.Is(ArrangementType.ShowLights))
                Model = new ShowLightsArrangement(fileName);
        }
    }
}
