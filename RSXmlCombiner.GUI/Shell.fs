module RSXmlCombiner.FuncUI.Shell

open Elmish
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.Media
open Rocksmith2014.XML
open RSXmlCombiner.FuncUI.ArrangementType
open RSXmlCombiner.FuncUI.AudioCombiner
open System
open System.IO

let init () = ProgramState.init, Cmd.none

let private createTrack instArrFile (title: string option) (audioFile: string option) arrangements =
    let song = InstrumentalArrangement.Load(instArrFile)
    let songLength =
        audioFile
        |> Option.map Audio.getLength
        |> Option.defaultValue (song.MetaData.SongLength * 1<ms>)

    { Title = title |> Option.defaultValue song.MetaData.Title
      AudioFile = audioFile
      SongLength = songLength
      TrimAmount = song.StartBeat * 1<ms>
      Arrangements = arrangements |> List.sortBy arrangementSort }

let private addNewTrack state arrangementFileNames =
    let instArrFile =
        arrangementFileNames
        |> Array.tryFind (XmlUtils.validateRootName "song")

    match instArrFile with
    | None ->
        { state with StatusMessage = "Please select at least one instrumental Rocksmith arrangement." }

    | Some instArrFile ->
        let canInclude arrType = List.exists (fun a -> a.ArrangementType = arrType) >> not

        let arrangementFolder (state: Arrangement list) fileName =
            match XmlUtils.getRootElementName fileName with
            | "song" ->
                let arr = createInstrumental fileName None
                if state |> List.exists (fun a -> a.Name = arr.Name) then
                    state
                else
                    arr::state
            | "vocals" when state |> canInclude ArrangementType.Vocals ->
                (createOther "Vocals" fileName ArrangementType.Vocals)::state
            | "showlights" when state |> canInclude ArrangementType.ShowLights ->
                (createOther "Show Lights" fileName ArrangementType.ShowLights)::state
            | _ ->
                state

        arrangementFileNames
        |> Array.fold arrangementFolder []
        // Add any missing arrangements from the project's templates
        |> ProgramState.addMissingArrangements state.Templates
        |> createTrack instArrFile None None
        |> ProgramState.addTrack state

let private changeAudioFile newFile track =
    let length = Audio.getLength newFile
    { track with AudioFile = Some newFile; SongLength = length }

let private getInitialDir (fileName: string option) state trackIndex =
    fileName
    |> Option.orElseWith (fun () ->
        // If no file is set, use the directory of the first arrangement that has a file
        state.Tracks.[trackIndex].Arrangements
        |> List.tryPick (fun a -> a.FileName))
    |> Option.map Path.GetDirectoryName

let private getArr trackIndex arrIndex state = state.Tracks.[trackIndex].Arrangements.[arrIndex]

let update msg state : ProgramState * Cmd<_> =
    match msg with
    | ToneReplacementClosed ->
        { state with ReplacementToneEditor = None }, Cmd.none

    | SetReplacementTone (trackIndex, arrIndex, toneName, replacementIndex) ->
        let arr = getArr trackIndex arrIndex state
        let data =
            arr.Data
            |> Option.map (fun data ->
                let updatedReplacements = data.ToneReplacements |> Map.add toneName replacementIndex
                { data with ToneReplacements = updatedReplacements })
        
        let updatedArr = { arr with Data = data }
        let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex updatedArr

        { state with Tracks = updatedTracks }, Cmd.none

    | ProjectViewActiveChanged isActive ->
        { state with ProjectViewActive = isActive }, Cmd.none

    | CombineAudioProgressChanged progress ->
        { state with AudioCombinerProgress = Some progress }, Cmd.none

    | CombineArrangementsProgressChanged progress ->
        let combineProgress =
            state.ArrangementCombinerProgress
            |> Option.map (fun (curr, max) -> (curr + progress, max))

        { state with ArrangementCombinerProgress = combineProgress }, Cmd.none

    | SelectAddTrackFiles ->
        let dialog = Dialogs.openMultiFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter
        state, Cmd.OfAsync.perform dialog None AddTrack

    | AddTrack (Some files) ->
        addNewTrack state files, Cmd.none

    | ImportProjectLoaded (foundArrangements, title, audioFilePath) ->
        let audioFile =
            let wav = Path.ChangeExtension(audioFilePath, "wav")
            let ogg = Path.ChangeExtension(audioFilePath, "ogg")
            let oggFixed = audioFilePath.Substring(0, audioFilePath.Length - 4) + "_fixed.ogg"
            Option.create File.Exists wav
            |> Option.orElse (Option.create File.Exists ogg)
            // Try to find the _fixed.ogg from an unpacked PSARC file
            |> Option.orElse (Option.create File.Exists oggFixed)

        // Try to find an instrumental arrangement to read metadata from
        let instArrType =
            foundArrangements
            |> Map.tryFindKey (fun arrType _ -> isInstrumental arrType)

        match instArrType with
        | None ->
            { state with StatusMessage = "Could not find any instrumental arrangements in the template." }, Cmd.none
        | Some instArrType ->
            let instArrFile = foundArrangements.[instArrType]
            
            let foldArrangements (state : Arrangement list) arrType fileName =
                let arrangement =
                    match arrType with
                    | Instrumental _ ->
                        // Respect the arrangement type from the imported project
                        createInstrumental fileName (Some arrType)
                    | _ ->
                        { FileName = Some fileName
                          ArrangementType = arrType
                          Name = humanize arrType
                          Data = None }

                arrangement::state

            let newState =
                foundArrangements
                |> Map.fold foldArrangements []
                // Add any missing arrangements from the project's templates
                |> ProgramState.addMissingArrangements state.Templates
                |> createTrack instArrFile (Some title) audioFile
                |> ProgramState.addTrack state

            let message = sprintf "%i arrangements imported." foundArrangements.Count
            { newState with StatusMessage = message }, Cmd.none
    
    | ImportProject (Some projectPath) ->
        let task (path: string) = async {
            if path.EndsWith("rs2dlc", StringComparison.OrdinalIgnoreCase) then
                return! DLCBuilderProject.import path
            else
                return ToolkitImporter.import path }

        state, Cmd.OfAsync.either task projectPath ImportProjectLoaded ErrorOccured

    | ErrorOccured ex ->
        { state with StatusMessage = ex.Message }, Cmd.none
    
    | NewProject ->
        ProgramState.init, Cmd.none

    | OpenProject (Some projectFile) ->
        match Project.load projectFile with
        | Error message ->
            { state with StatusMessage = message }, Cmd.none
        | Ok project ->
            // TODO: Check if the tone names in the files have been changed

            // Create lists of missing audio and arrangement files
            let missingAudioFiles, missingArrangementFiles =
                (([], []), project.Tracks)
                ||> List.fold (fun state track ->
                    let missingAudioFiles =
                        match track.AudioFile with
                        | Some file when not <| File.Exists file ->
                            file::(fst state)
                        | _ ->
                            fst state

                    let missingArrangementFiles =
                        ([], track.Arrangements)
                        ||> List.fold (fun missing arr -> 
                            match arr.FileName with
                            | Some file when not <| File.Exists file ->
                                file::missing
                            | _ ->
                                missing)

                    missingAudioFiles, (snd state) @ missingArrangementFiles)

            let statusMessage =
                match missingAudioFiles, missingArrangementFiles with
                | [], [] -> "Project loaded."
                | _, _ -> "WARNING: Some of the files referenced in the project could not be found!"

            // Generate the arrangement templates from the first track
            let templates = 
                match project.Tracks with
                | head::_ ->
                    head.Arrangements |> (List.map createTemplate >> Templates)
                | [] ->
                    Templates []

            Project.toProgramState templates projectFile statusMessage project, Cmd.none

    | SaveProject (Some fileName) ->
        let task () = async {
            do! Project.save fileName state
            return fileName }

        state, Cmd.OfAsync.either task () ProjectSaved ErrorOccured

    | ProjectSaved fileName ->
        { state with OpenProjectFile = Some fileName
                     StatusMessage = "Project saved." }, Cmd.none

    | SelectImportProject ->
        let dialog = Dialogs.openFileDialog "Select Project to Import" Dialogs.projectImportFilter
        state, Cmd.OfAsync.perform dialog None ImportProject

    | SelectOpenProjectFile ->
        let dialog = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter
        state, Cmd.OfAsync.perform dialog None OpenProject

    | SelectSaveProjectFile ->
        let initialDir =
            state.OpenProjectFile
            |> Option.map Path.GetDirectoryName

        let initialFile =
            state.OpenProjectFile
            |> Option.map Path.GetFileName
            |> Option.orElse (Some "combo.rscproj")

        let dialog = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter initialFile
        state, Cmd.OfAsync.perform dialog initialDir SaveProject

    | AddTemplate (arrType, ordering) ->
        let (Templates templates) = state.Templates
        let tempArr =
            let data =
                ordering
                |> Option.map (fun o -> { Ordering = o; BaseToneIndex = -1; ToneNames = []; ToneReplacements = Map.empty })
            { ArrangementType = arrType
              Name = String.Empty
              FileName = None
              Data = data }

        let updatedTemplates = Templates ((createTemplate tempArr)::templates)
        let updatedTracks = state.Tracks |> ProgramState.updateTracks updatedTemplates

        { state with Tracks = updatedTracks; Templates = updatedTemplates }, Cmd.none

    | SelectTargetAudioFile (defaultFileName, cmd) ->
        let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
        let dialog = Dialogs.saveFileDialog "Select Target File" Dialogs.audioFileFiltersSave defaultFileName
        state, Cmd.OfAsync.perform dialog initialDir cmd

    | CombineAudioFiles (Some targetFile) ->
        let task() = async {
            try
                state.Tracks
                |> List.map (fun track ->
                    { AudioReader = Audio.AudioReader.Create track.AudioFile.Value
                      TrimAmount = track.TrimAmount })
                |> combineWithResampling targetFile
                return $"Audio files combined as {targetFile}"
            with e ->
                return $"Error: {e.Message}" }

        { state with AudioCombinerProgress = Some 0.0
                     StatusMessage = "Combining audio files..." },
        Cmd.OfAsync.perform task () CombineAudioCompleted
    
    | CreatePreview (Some targetFile) ->
        let task file = async {
            try
                state.Tracks
                |> List.map (fun track ->
                    { AudioReader = Audio.AudioReader.Create track.AudioFile.Value
                      SongLength = track.SongLength })
                |> createPreview file
                return "Preview created."
            with e ->
                return $"Error: {e.Message}" }

        { state with AudioCombinerProgress = Some 0.0
                     StatusMessage = "Creating preview audio..." },
        Cmd.OfAsync.perform task targetFile CombineAudioCompleted

    | CombineAudioCompleted message ->
        { state with StatusMessage = message; AudioCombinerProgress = None }, Cmd.none

    | CombineArrangements (Some targetFolder) ->
        let trackCount = state.Tracks.Length
        // Calculate the maximum value for the progress bar
        let max =
            ((0, 0), state.Tracks.Head.Arrangements)
            ||> Seq.fold (fun (i, count) arr ->
                let hasFile track = track.Arrangements.[i].FileName |> Option.isSome
                let next = i + 1
                // For instrumental arrangement, the progress is increased by one for each file
                // Combining vocals and show lights is so fast that individual files are not reported
                match arr.ArrangementType with
                | Instrumental _ ->
                    if state.Tracks |> List.forall hasFile then next, count + trackCount else next, count
                | ArrangementType.ShowLights ->
                    if state.Tracks |> List.forall hasFile then next, count + 1 else next, count
                | _ ->
                    if state.Tracks |> List.exists hasFile then next, count + 1 else next, count
                )
            |> snd

        let task = ArrangementCombiner.combine state
        { state with ArrangementCombinerProgress = Some(0, max) },
        Cmd.OfAsync.perform task targetFolder CombineArrangementsCompleted

    | CombineArrangementsCompleted _ ->
        { state with StatusMessage = "Arrangements combined."
                     ArrangementCombinerProgress = None }, Cmd.none

    | SelectCombinationTargetFolder ->
        let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
        let dialog = Dialogs.openFolderDialog "Select Target Folder"
        state, Cmd.OfAsync.perform dialog initialDir CombineArrangements

    | UpdateCombinationTitle newTitle ->
        { state with CombinationTitle = newTitle }, Cmd.none

    | CoercePhrasesChanged value ->
        { state with CoercePhrases = value }, Cmd.none

    | OnePhrasePerTrackChanged value ->
        { state with OnePhrasePerTrack = value }, Cmd.none

    | AddTrackNamesChanged value ->
        { state with AddTrackNamesToLyrics = value }, Cmd.none

    | RemoveTrack trackIndex ->
        { state with Tracks = state.Tracks |> List.except (seq { state.Tracks.[trackIndex] }) }, Cmd.none

    | ChangeAudioFile trackIndex ->
        let initialDir = getInitialDir state.Tracks.[trackIndex].AudioFile state trackIndex
        let dialog = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFiltersOpen
        state, Cmd.OfAsync.perform dialog initialDir (fun file -> ChangeAudioFileResult (trackIndex, file))

    | ChangeAudioFileResult (trackIndex, file) ->
        match file with
        | None ->
            state, Cmd.none
        | Some fileName ->
            let oldSongLength = state.Tracks.[trackIndex].SongLength
            let updatedTracks =
                state.Tracks
                |> List.mapAt trackIndex (changeAudioFile fileName)
            let newSongLength = updatedTracks.[trackIndex].SongLength

            let message =
                if oldSongLength <> newSongLength then
                    sprintf "Old song length: %.3f, new: %.3f" (float oldSongLength / 1000.0) (float newSongLength / 1000.0)
                else
                    "Audio file changed."

            { state with Tracks = updatedTracks; StatusMessage = message }, Cmd.none

    | SelectArrangementFile (trackIndex, arrIndex) ->
        let initialDir = getInitialDir (state |> getArr trackIndex arrIndex).FileName state trackIndex
        let dialog = Dialogs.openFileDialog "Select Arrangement File" Dialogs.xmlFileFilter
        state, Cmd.OfAsync.perform dialog initialDir (fun file -> ChangeArrangementFile (trackIndex, arrIndex, file))

    | ChangeArrangementFile (trackIndex, arrIndex, file) ->
        match file with
        | None ->
            state, Cmd.none
        | Some fileName ->
            let rootName = XmlUtils.getRootElementName fileName
            let arrangement = state |> getArr trackIndex arrIndex

            match rootName, arrangement.ArrangementType with
            | "song", Instrumental t ->
                // For instrumental arrangements, create an arrangement from the file, preserving the arrangement type and name
                Ok { createInstrumental fileName (Some t) with Name = arrangement.Name }
            | "vocals", Vocals _
            | "showlights", ArrangementType.ShowLights ->
                // For vocals and show lights, just change the file name
                Ok { arrangement with FileName = Some fileName }
            | _ ->
                Error "Incorrect arrangement type."
            |> function
            | Ok arr ->
                let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex arr
                { state with Tracks = updatedTracks }, Cmd.none
            | Error message ->
                { state with StatusMessage = message }, Cmd.none

    | ArrangementBaseToneChanged (trackIndex, arrIndex, toneIndex) ->
        match state |> getArr trackIndex arrIndex with
        | { Data = Some arrData } as arrangement ->
            let data = { arrData with BaseToneIndex = toneIndex }

            let newArr = { arrangement with Data = Some data }
            let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr

            { state with Tracks = updatedTracks }, Cmd.none

        | { Data = None } ->
            // Should not be able to get here
            { state with StatusMessage = "Critical program error." }, Cmd.none

    | RemoveArrangementFile (trackIndex, arrIndex) ->
        let newArr = { (state |> getArr trackIndex arrIndex) with FileName = None; Data = None }
        let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex newArr

        { state with Tracks = updatedTracks }, Cmd.none

    | ShowReplacementToneEditor (trackIndex, arrIndex) ->
        { state with ReplacementToneEditor = Some(trackIndex, arrIndex) }, Cmd.none

    | TrimAmountChanged (trackIndex, trimAmount) ->
        let trim = int (Math.Round(trimAmount * 1000.0)) * 1<ms>
        let newTracks =
            state.Tracks
            |> List.mapAt trackIndex (fun track -> { track with TrimAmount = trim })

        { state with Tracks = newTracks }, Cmd.none

    | RemoveTemplate name ->
        let (Templates templates) = state.Templates
        let updatedTemplates =
            templates
            |> List.filter (fun t -> t.Name <> name)
            |> Templates

        // Remove the arrangement from all the tracks
        let updatedTracks =
            state.Tracks
            |> List.map (fun track ->
                let arrangements =
                    track.Arrangements
                    |> List.filter (fun arr -> arr.Name <> name)
                { track with Arrangements = arrangements })

        let updatedCommonTones = state.CommonTones |> Map.remove name
        let updatedSelectedTones = state.SelectedFileTones |> Map.remove name

        { state with
            Tracks = updatedTracks
            Templates = updatedTemplates
            CommonTones = updatedCommonTones
            SelectedFileTones = updatedSelectedTones }, Cmd.none

    | UpdateToneName (arrName, index, newName) ->
        let names = state.CommonTones.[arrName]
        let oldName = names.[index]
        if oldName = newName then
            state, Cmd.none
        else 
            let newTones = 
                state.CommonTones
                |> Map.add arrName (names |> Array.updateAt index newName)

            { state with CommonTones = newTones }, Cmd.none

    | SelectedToneFromFileChanged (arrName, selectedTone) ->
        let selectedTones =
            state.SelectedFileTones
            |> Map.add arrName selectedTone
        { state with SelectedFileTones = selectedTones }, Cmd.none

    | AddSelectedToneFromFile arrName ->
        let tones = state.CommonTones.[arrName]
        // Find an empty index that is not the base tone
        let availableIndex = tones.[1..] |> Array.tryFindIndex String.IsNullOrEmpty
        let selectedTone = state.SelectedFileTones |> Map.tryFind arrName

        match availableIndex, selectedTone with
        | Some i, Some newTone ->
            let updatedTones =
                let i = i + 1
                if i = 1 && String.IsNullOrEmpty tones.[0] then
                    // If the base tone and tone A are empty, use this name for them both
                    tones |> Array.mapi (fun j t -> if j = 0 || j = i then newTone else t)
                else
                    tones |> Array.updateAt i newTone

            let updatedCommonTones = state.CommonTones |> Map.add arrName updatedTones
            { state with CommonTones = updatedCommonTones }, Cmd.none
        | _ ->
            state, Cmd.none

    // User canceled the dialog
    | CombineAudioFiles None | CreatePreview None | CombineArrangements None
    | ImportProject None | OpenProject None | SaveProject None | AddTrack None ->
        state, Cmd.none

let private replacementToneView state trackIndex arrIndex dispatch =
    let arrangement = state.Tracks.[trackIndex].Arrangements.[arrIndex]
    let data = arrangement.Data |> Option.get
    let replacementToneNames = ProgramState.getReplacementToneNames arrangement.Name state.CommonTones

    DockPanel.create [
        DockPanel.background "#77000000"
        DockPanel.children [
            Border.create [
                Border.padding 20.0
                Border.cornerRadius 5.0
                Border.horizontalAlignment HorizontalAlignment.Center
                Border.verticalAlignment VerticalAlignment.Center
                Border.background "#444444"
                Border.child (
                    Grid.create [
                        Grid.columnDefinitions "150, 150"
                        Grid.rowDefinitions (Seq.replicate (data.ToneNames.Length + 3) "*" |> String.concat ",")
                        Grid.children [
                            // Track name - Arrangement name
                            yield TextBlock.create [
                                Grid.columnSpan 2
                                TextBlock.horizontalAlignment HorizontalAlignment.Center
                                TextBlock.fontSize 18.0
                                TextBlock.margin (0.0, 0.0, 0.0, 8.0)
                                TextBlock.text (sprintf "%s - %s" state.Tracks.[trackIndex].Title arrangement.Name)
                            ]
                            // "Tone Name"
                            yield TextBlock.create [
                                Grid.row 1
                                TextBlock.text "Tone Name"
                                TextBlock.fontSize 16.0
                            ]
                            // "Replace With"
                            yield TextBlock.create [
                                Grid.row 1
                                Grid.column 1
                                TextBlock.text "Replace With"
                                TextBlock.fontSize 16.0
                            ]
                            for i, tone in data.ToneNames |> List.indexed do
                                // Original tone name
                                yield TextBlock.create [
                                        Grid.row (i + 2)
                                        TextBlock.margin 2.0
                                        TextBlock.text tone
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                      ]
                                // Replacement selector
                                yield ComboBox.create [
                                        Grid.row (i + 2)
                                        Grid.column 1
                                        ComboBox.margin 2.0
                                        ComboBox.height 30.0
                                        ComboBox.dataItems replacementToneNames
                                        ComboBox.selectedIndex (
                                            data.ToneReplacements
                                            |> Map.tryFind tone
                                            |> Option.defaultValue -1)
                                        ComboBox.onSelectedIndexChanged (fun item ->
                                            SetReplacementTone(trackIndex, arrIndex, tone, item)
                                            |> dispatch)
                                      ]
                            // OK Button
                            yield Button.create [
                                Grid.row (data.ToneNames.Length + 2)
                                Grid.columnSpan 2
                                Button.content "OK"
                                Button.isDefault true
                                Button.fontSize 15.0
                                Button.horizontalAlignment HorizontalAlignment.Center
                                Button.width 120.0
                                Button.margin 5.0
                                Button.onClick (fun _ -> ToneReplacementClosed |> dispatch)
                            ]
                        ]
                    ]
                )
            ]
        ]
    ]

let view state dispatch =
    TabControl.create [
        TabControl.tabStripPlacement Dock.Top
        TabControl.viewItems [
            TabItem.create [
                TabItem.header "Tracks"
                TabItem.foreground Brushes.AntiqueWhite
                TabItem.onIsSelectedChanged (ProjectViewActiveChanged >> dispatch)
                TabItem.content (
                    Grid.create [
                        Grid.children [
                            DockPanel.create [
                                DockPanel.children [
                                    TopControls.view state dispatch

                                    // Status Bar with Message
                                    Border.create [
                                        Border.classes [ "statusbar" ]
                                        DockPanel.dock Dock.Bottom
                                        Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                                    ]

                                    // Progress Bar for Audio Combining
                                    ProgressBar.create [
                                          DockPanel.dock Dock.Bottom
                                          ProgressBar.background "#181818"
                                          ProgressBar.height 1.0
                                          ProgressBar.minHeight 1.0
                                          ProgressBar.value (state.AudioCombinerProgress |> Option.defaultValue 0.0)
                                          ProgressBar.maximum 1.0
                                          ProgressBar.isIndeterminate state.AudioCombinerProgress.IsSome
                                    ]

                                    // Progress Bar for Arrangement Combining
                                    ProgressBar.create [
                                          DockPanel.dock Dock.Bottom
                                          ProgressBar.background "#181818"
                                          ProgressBar.foreground Brushes.Red
                                          ProgressBar.height 1.0
                                          ProgressBar.minHeight 1.0
                                          ProgressBar.value (
                                            state.ArrangementCombinerProgress
                                            |> Option.map (fst >> double)
                                            |> Option.defaultValue 0.0)
                                          ProgressBar.maximum (
                                            state.ArrangementCombinerProgress
                                            |> Option.map (snd >> double)
                                            |> Option.defaultValue 1.0)
                                    ]

                                    BottomControls.view state dispatch

                                    TrackList.view state dispatch
                                ]
                            ]

                            match state.ReplacementToneEditor with
                            | Some (trackIndex, arrIndex) ->
                                replacementToneView state trackIndex arrIndex dispatch |> generalize
                            | None ->
                                ()
                        ]
                    ]
                )
            ]

            TabItem.create [
                TabItem.header "Common Tones"
                TabItem.content (CommonToneEditor.view state dispatch)
                TabItem.foreground Brushes.AntiqueWhite
            ]

            TabItem.create [ TabItem.header "Help"; TabItem.foreground Brushes.AntiqueWhite; TabItem.content Help.helpView ]
        ]
    ]
