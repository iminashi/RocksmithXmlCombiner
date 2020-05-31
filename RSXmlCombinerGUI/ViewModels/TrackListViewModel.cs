using DynamicData;
using DynamicData.Binding;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using Rocksmith2014Xml;

using RSXmlCombinerGUI.Extensions;
using RSXmlCombinerGUI.Models;

using Splat;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using XmlCombiners;

using XmlUtils;

#pragma warning disable RCS1090 // Call 'ConfigureAwait(false)'.

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class TrackListViewModel : ViewModelBase
    {
        [Reactive]
        public string StatusMessage { get; set; } = " ";

        [Reactive]
        public string CombineAudioError { get; set; } = string.Empty;

        [Reactive]
        public bool CoercePhrases { get; set; } = true;

        [Reactive]
        public bool AddTrackNamesToLyrics { get; set; } = true;

        [Reactive]
        public string CombinedTitle { get; set; } = string.Empty;

        public ObservableCollectionExtended<TrackViewModel> Tracks { get; } = new ObservableCollectionExtended<TrackViewModel>();

        public ReactiveCommand<Unit, Unit> CombineAudioFiles { get; }
        public ReactiveCommand<Unit, Unit> CombineArrangements { get; }
        public ReactiveCommand<Unit, Unit> AddTrack { get; }
        public ReactiveCommand<Unit, Unit> NewProject { get; }
        public ReactiveCommand<Unit, Unit> OpenProject { get; }
        public ReactiveCommand<Unit, Unit> SaveProject { get; }
        public ReactiveCommand<Unit, Unit> ToolkitImport { get; }

        private string? LoadedProjectFile { get; set; }

        private Dictionary<TrackViewModel, IDisposable> MessageSubscriptions { get; } = new Dictionary<TrackViewModel, IDisposable>();

        public TrackListViewModel()
        {
            // Audio can be combined when there is more than one track and every track has an audio file set
            var canCombineAudio = Tracks
                .ToObservableChangeSet()
                .AutoRefresh(vm => vm.AudioFile)
                .Select(_ => Tracks.Count > 1 && Tracks.All(vm => !string.IsNullOrEmpty(vm.AudioFile)));

            // Arrangements can be combined when there is more than one track
            var canCombineArrangements = this.WhenAnyValue(x => x.Tracks.Count)
                .Select(count => count > 1);

            // Project can be saved if there is at least one track
            var canSave = this.WhenAnyValue(x => x.Tracks.Count)
                .Select(count => count != 0);

            CombineAudioFiles = ReactiveCommand.CreateFromTask(CombineAudioFilesImpl, canCombineAudio);
            CombineArrangements = ReactiveCommand.CreateFromTask(CombineArrangementsImpl, canCombineArrangements);
            AddTrack = ReactiveCommand.CreateFromTask(AddTrackImpl);

            NewProject = ReactiveCommand.Create(() =>
            {
                CombinedTitle = string.Empty;
                foreach (var kv in MessageSubscriptions)
                {
                    kv.Value.Dispose();
                }
                MessageSubscriptions.Clear();
                Tracks.Clear();
            });

            SaveProject = ReactiveCommand.CreateFromTask(SaveProjectImpl, canSave);
            OpenProject = ReactiveCommand.CreateFromTask(OpenProjectImpl);
            ToolkitImport = ReactiveCommand.CreateFromTask(ToolkitImportImpl);
        }

        private async Task ToolkitImportImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            var dialogResult = await dialogs.OpenFileDialog("Select Toolkit Template", DialogServices.ToolkitXmlFileFilters);

            if (dialogResult?.Length > 0)
            {
                var importer = new ToolkitTemplateImporter(this);
                var import = importer.Import(dialogResult[0]);
                if (import != null)
                {
                    var d = import.Messages.Subscribe(x => StatusMessage = x);
                    MessageSubscriptions.Add(import, d);
                    Tracks.Add(import);
                }
                else
                {
                    StatusMessage = "Import failed!";
                }
            }
        }

        private async Task OpenProjectImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            var fileNames = await dialogs.OpenFileDialog("Select Project File", DialogServices.ProjectFileFilters);

            if (fileNames.Length > 0)
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    };

                    string json = File.ReadAllText(fileNames[0]);
                    var project = JsonSerializer.Deserialize<ProjectDto>(json, options);
                    SetProperties(project);
                    LoadedProjectFile = fileNames[0];
                }
                catch (JsonException)
                {
                    StatusMessage = "Loading project file failed";
                }
            }
        }

        private void SetProperties(ProjectDto project)
        {
            CombinedTitle = project.CombinedTitle;
            CoercePhrases = project.CoercePhrases;
            AddTrackNamesToLyrics = project.AddTrackNamesToLyrics;

            foreach (var kv in project.CommonToneNames)
            {
                CommonTonesRepository.SetCommonTones(Enum.Parse<ArrangementType>(kv.Key), kv.Value);
            }

            Tracks.Load(project.Tracks.Select(t => new TrackViewModel(t, this)));
            foreach (var tvm in Tracks)
            {
                var d = tvm.Messages.Subscribe(x => StatusMessage = x);
                MessageSubscriptions.Add(tvm, d);
            }
        }

        private async Task SaveProjectImpl()
        {
            string projectFile = DetermineInitialFileName();

            var dialogs = Locator.Current.GetService<IDialogServices>();
            string fileName = await dialogs
                .SaveFileDialog("Save Project As", DialogServices.ProjectFileFilters, projectFile);

            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    };

                    var json = JsonSerializer.Serialize(new ProjectDto(this), options);
                    File.WriteAllText(fileName, json);
                }
                catch (Exception e)
                {
                    StatusMessage = "Saving project file failed: " + e.Message;
                }
            }
        }

        private string DetermineInitialFileName()
        {
            string? projectFile = Path.GetFileName(LoadedProjectFile);
            if (string.IsNullOrEmpty(projectFile))
            {
                if (!string.IsNullOrEmpty(CombinedTitle))
                    projectFile = CombinedTitle;
                else
                    projectFile = "Combined Project";

                projectFile += ".rscproj";
            }

            return projectFile;
        }

        private void CombineInstrumentalArrangement(ArrangementType arrType, string targetFolder)
        {
            var arrCombine = Tracks[0].Arrangements.FirstOrDefault(a => a.ArrangementType == arrType);
            if (arrCombine is null)
                return;
            int arrIndex = Tracks[0].Arrangements.IndexOf(arrCombine);

            // Combine the arrangements only if all tracks have an arrangement set
            if (Tracks.All(t => t.Arrangements[arrIndex].Model is InstrumentalArrangement))
            {
                var combiner = new InstrumentalCombiner();

                for (int i = 0; i < Tracks.Count; i++)
                {
                    bool isLast = i == Tracks.Count - 1;
                    var arr = Tracks[i].Arrangements[arrIndex].Model as InstrumentalArrangement;
                    var next = RS2014Song.Load(arr!.FileName);
                    if (arr.ToneNames != null)
                    {
                        next.ReplaceToneNames(arr.ToneReplacements);
                    }
                    else
                    {
                        next.UpdateBaseTone(arr);
                    }

                    combiner.AddNext(next, Tracks[i].TrimAmount, isLast);
                }

                if (!string.IsNullOrEmpty(CombinedTitle))
                    combiner.SetTitle(CombinedTitle);

                combiner.Save(Path.Combine(targetFolder, $"Combined_{arrType}_RS2.xml"), CoercePhrases);
            }
        }

        private async Task CombineArrangementsImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();

            string targetFolder = await dialogs
                .OpenFolderDialog("Select Target Folder");

            if (string.IsNullOrEmpty(targetFolder))
                return;

            Task[] tasks =
            {
                Task.Run(() => CombineInstrumentalArrangement(ArrangementType.Lead, targetFolder)),
                Task.Run(() => CombineInstrumentalArrangement(ArrangementType.Rhythm, targetFolder)),
                Task.Run(() => CombineInstrumentalArrangement(ArrangementType.Bass, targetFolder)),
                Task.Run(() => CombineVocalsArrangement(targetFolder)),
                Task.Run(() => CombineShowLightsArrangement(targetFolder))
            };

            await Task.WhenAll(tasks);

            StatusMessage = "Arrangements combined.";
        }

        private void CombineShowLightsArrangement(string targetFolder)
        {
            var arrCombine = Tracks[0].Arrangements.FirstOrDefault(a => a.ArrangementType == ArrangementType.ShowLights);
            if (arrCombine is null)
                return;
            int arrIndex = Tracks[0].Arrangements.IndexOf(arrCombine);

            // Combine the arrangements only if all tracks have one
            if (Tracks.All(t => t.Arrangements[arrIndex].Model is Arrangement))
            {
                var combiner = new ShowLightsCombiner();

                for (int i = 0; i < Tracks.Count; i++)
                {
                    var next = ShowLights.Load(Tracks[i].Arrangements[arrIndex].Model!.FileName);
                    combiner.AddNext(next, Tracks[i].SongLength, Tracks[i].TrimAmount);
                }

                combiner.Save(Path.Combine(targetFolder, "Combined_Showlights_RS2.xml"));
            }
        }

        private void CombineVocalsArrangement(string targetFolder)
        {
            var arrCombine = Tracks[0].Arrangements.FirstOrDefault(a => a.ArrangementType == ArrangementType.Vocals);
            if (arrCombine is null)
                return;
            int arrIndex = Tracks[0].Arrangements.IndexOf(arrCombine);

            // Combine the arrangements if at least one vocals arrangement is set
            if (Tracks.Any(t => t.Arrangements[arrIndex].Model is Arrangement))
            {
                var combiner = new VocalsCombiner();

                for (int i = 0; i < Tracks.Count; i++)
                {
                    var next = Tracks[i].Arrangements[arrIndex].Model is null ?
                        new Vocals() :
                        Vocals.Load(Tracks[i].Arrangements[arrIndex].Model!.FileName);

                    if (AddTrackNamesToLyrics)
                    {
                        next.AddTitleToLyrics($"{i + 1}. {Tracks[i].Title}+", Math.Abs(Tracks[i].TrimAmount));
                    }

                    combiner.AddNext(next, Tracks[i].SongLength, Tracks[i].TrimAmount);
                }

                combiner.Save(Path.Combine(targetFolder, "Combined_Vocals_RS2.xml"));
            }
        }

        private async Task AddTrackImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            var dialogResult = await dialogs.OpenFileDialog("Select RS2014 Arrangement(s)", DialogServices.RocksmithXmlFileFilters, multiSelect: true);
            var fileNames = dialogResult.ToList();

            if (fileNames.Count > 0)
            {
                // Find the first instrumental arrangement
                var instArrFile = fileNames.Find(a => XmlHelper.ValidateRootElement(a, "song"));
                if (instArrFile is null)
                {
                    StatusMessage = "Please select at least one instrumental Rocksmith arrangement.";
                    return;
                }

                try
                {
                    var s = RS2014Song.Load(instArrFile);
                    var tvm = new TrackViewModel(s.Title, s.StartBeat, s.SongLength, this);

                    foreach (var fn in fileNames)
                    {
                        if (XmlHelper.ValidateRootElement(fn, "song"))
                        {
                            tvm.SetArrangement(RS2014Song.Load(fn), fn);
                        }
                        else if (XmlHelper.ValidateRootElement(fn, "vocals"))
                        {
                            tvm.Arrangements.Add(new ArrangementViewModel(new VocalsArrangement(fn, ArrangementType.Vocals), tvm));
                        }
                        else if (XmlHelper.ValidateRootElement(fn, "showlights"))
                        {
                            tvm.Arrangements.Add(new ArrangementViewModel(new ShowLightsArrangement(fn), tvm));
                        }
                        else
                        {
                            StatusMessage = $"Unknown arrangement type for file {Path.GetFileName(fn)}";
                        }
                    }

                    var d = tvm.Messages.Subscribe(x => StatusMessage = x);
                    MessageSubscriptions.Add(tvm, d);
                    Tracks.Add(tvm);
                }
                catch (Exception e)
                {
                    StatusMessage = "Loading failed: " + e.Message;
                }
            }
        }

        private async Task CombineAudioFilesImpl()
        {
            var dialogs = Locator.Current.GetService<IDialogServices>();
            var targetFile = await dialogs
                .SaveFileDialog("Select Target File", DialogServices.AudioFileFiltersSave, initialFilename: "combo.wav");

            if (string.IsNullOrEmpty(targetFile))
                return;

            string arguments = CreateArguments(targetFile);

            using Process soxProcess = new Process();
            //soxProcess.StartInfo.UseShellExecute = false;
            //soxProcess.StartInfo.CreateNoWindow = true;

            soxProcess.StartInfo.FileName = Path.Combine(Program.AppPath, "tools", "sox", "sox");
            soxProcess.StartInfo.Arguments = arguments;

            int exitCode = await soxProcess.RunAsync();
            if (exitCode != 0)
            {
                CombineAudioError = $"sox exited with code {exitCode}";
            }
            else
            {
                StatusMessage = "Audio files combined as " + targetFile;
            }
        }

        private string CreateArguments(string targetFile)
        {
            string arguments = $@"--multi−threaded --buffer 131072 -S ""{Tracks[0].AudioFile}""";

            for (int i = 1; i < Tracks.Count; i++)
            {
                // Format: "|sox foo.wav -p trim 5.500"
                arguments += $@" ""|sox \""{Tracks[i].AudioFile}\"" -p trim {Tracks[i].TrimAmount.ToString("F3", NumberFormatInfo.InvariantInfo)}""";
            }

            arguments += $@" ""{targetFile}""";
            return arguments;
        }

        public void RemoveTrack(TrackViewModel tvm)
        {
            MessageSubscriptions[tvm].Dispose();
            MessageSubscriptions.Remove(tvm);
            Tracks.Remove(tvm);
        }

        internal void UpdateTones(List<ToneNamesViewModel> tones)
        {
            var oldCommonTones = CommonTonesRepository.GetCopy();

            foreach (var vm in tones)
            {
                CommonTonesRepository.SetCommonTones(vm.ArrangementType, vm.Tones.ToArray());
            }

            foreach (var kv in oldCommonTones)
            {
                var commonTones = CommonTonesRepository.GetCommonTones(kv.Key);

                for (int i = 0; i < commonTones.Length; i++)
                {
                    if (oldCommonTones[kv.Key][i] != commonTones[i])
                        UpdateToneReplacements(kv.Key, oldCommonTones[kv.Key][i], commonTones[i]);
                }
            }

            if (Tracks.Count > 0)
            {
                // Update the base tones of the first track
                foreach (var arrVm in Tracks[0].Arrangements)
                {
                    if (arrVm.Model is InstrumentalArrangement instArr)
                    {
                        instArr.BaseTone = CommonTonesRepository.GetCommonTones(instArr.ArrangementType)[0];
                    }
                }

                // Update the base tone combo boxes
                foreach (var arrVm in Tracks.SelectMany(t => t.Arrangements).Where(a => a.Model is InstrumentalArrangement))
                {
                    arrVm.ToneControls.UpdateTones();
                }
            }
        }

        private void UpdateToneReplacements(ArrangementType type, string oldName, string newName)
        {
            foreach (var arrVm in Tracks.SelectMany(t => t.Arrangements))
            {
                if (arrVm.Model is InstrumentalArrangement arr && arr.ArrangementType == type)
                {
                    foreach (var oldNew in arr.ToneReplacements)
                    {
                        if (oldNew.Value == oldName)
                        {
                            arr.ToneReplacements[oldNew.Key] = newName;
                            break;
                        }
                    }
                }
            }
        }
    }
}
