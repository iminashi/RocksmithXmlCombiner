namespace RSXmlCombiner.FuncUI

module TrackList =
    open System
    open System.Text.Json
    open System.Text.Json.Serialization
    open System.IO
    open Elmish
    open Avalonia.Media
    open Avalonia.Controls
    open Avalonia.Controls.Shapes
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Types
    open Avalonia.Layout
    open Avalonia.Input
    open Types
    open XmlUtils
    open Rocksmith2014Xml

    /// Contains the open project and a status message
    type State = { Project : CombinerProject; StatusMessage : string }

    /// Initial state
    let init = { Project = emptyProject; StatusMessage = "" }, Cmd.none

    // Message
    type Msg =
    | AddTrack of arrangements : string[]
    | RemoveTrackAt of index : int
    | NewProject
    | OpenProject of fileNames : string[]
    | SaveProject of fileName : string
    | ChangeAudioFile of track : Track
    | ChangeAudioFileResult of track : Track * newFile:string[]
    | SelectTargetAudioFile
    | CombineAudioFiles of targetFile : string
    | ImportToolkitTemplate of fileNames : string[]
    | SelectCombinationTargetFolder
    | CombineArrangements of targetFolder : string
    | ProjectArrangementsChanged of templates : Arrangement list * commonTones : Map<string, string[]>
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * string[]
    | UpdateCombinationTitle of title : string

    let changeAudioFile track newFile = { track with AudioFile = Some newFile }

    let createTemplate arr =
        { Name = arr.Name; ArrangementType = arr.ArrangementType; FileName = None; Data = None }

    let updateTemplates (current : Arrangement list) (arrangements : Arrangement list) =
        let newTemplates =
            arrangements
            |> List.filter (fun arr -> current |> List.exists (fun temp -> arr.Name = temp.Name) |> not)
            |> List.map createTemplate
        current @ newTemplates

    let addMissingArrangements templates (arrs : Arrangement list) =
        arrs 
        |> List.append
            (templates
            |> List.except (arrs |> Seq.map createTemplate))

    let updateTrack templates track =
        let newArrangements = 
            addMissingArrangements templates track.Arrangements
            |> List.sortBy (fun a -> a.ArrangementType)

        { track with Arrangements = newArrangements }

    let updateTracks (tracks : Track list) (templates : Arrangement list) =
        tracks
        |> List.map (updateTrack templates)

    let updateTracksAndTemplates tracks (currentTemplates : Arrangement list) newTrackarrangements =
        let templates =
            if currentTemplates.IsEmpty then
                // Initialize the templates from this track
                newTrackarrangements |> List.map createTemplate
            else
                updateTemplates currentTemplates newTrackarrangements

        (updateTracks tracks templates), templates

    let createTrack (song : RS2014Song) arrangements =
        { Title = song.Title
          AudioFile = None
          SongLength = song.SongLength
          TrimAmount = song.StartBeat
          Arrangements = arrangements |> List.sortBy (fun a -> a.ArrangementType) }

    let addNewTrack state arrangementFileNames =
        let instArrFile = arrangementFileNames |> Array.tryFind (fun a -> XmlHelper.ValidateRootElement(a, "song"))
        match instArrFile with
        | None -> 
            { state with StatusMessage = "Please select at least one instrumental Rocksmith arrangement." }, Cmd.none
        | Some instArr ->
            let alreadyHasShowlights (arrs : Arrangement list) =
                arrs |> List.exists (fun a -> a.ArrangementType = ArrangementType.ShowLights)

            let arrangementFolder state fileName =
                match XmlHelper.GetRootElementName(fileName) with
                | "song" ->
                    (createInstrumental fileName None) :: state
                | "vocals" ->
                    { Name = "Vocals"; FileName = Some fileName; ArrangementType = ArrangementType.Vocals; Data = None  } :: state
                | "showlights" when state |> alreadyHasShowlights |> not ->
                    { Name = "Show Lights"; FileName = Some fileName; ArrangementType = ArrangementType.ShowLights; Data = None } :: state
                //| "showlights" -> Cannot have more than one show lights arrangement
                | _ -> state // StatusMessage = "Unknown arrangement type for file {Path.GetFileName(arr)}";
        
            let arrangements = 
                arrangementFileNames
                |> Array.fold arrangementFolder []
                // Add any missing arrangements from the project's templates
                |> addMissingArrangements state.Project.Templates

            let song = RS2014Song.Load(instArr)
            let newTrack = createTrack song arrangements
            let tracks, templates = updateTracksAndTemplates state.Project.Tracks state.Project.Templates arrangements

            let updatedProject = { state.Project with Tracks = tracks @ [ newTrack ]; Templates = templates }

            { state with Project = updatedProject }, Cmd.ofMsg (ProjectArrangementsChanged(templates, updatedProject.CommonTones))

    /// Updates the model according to the message content.
    let update (msg: Msg) (state: State) =
        match msg with
        | AddTrack arrangements -> addNewTrack state arrangements

        | NewProject -> init

        | RemoveTrackAt index ->
            { state with Project = { state.Project with Tracks = state.Project.Tracks |> List.except [ state.Project.Tracks.[index] ]}}, Cmd.none

        | ChangeAudioFile track ->
            let selectFiles = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFilters false
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) track (fun files -> ChangeAudioFileResult(track, files))

        | ChangeAudioFileResult (track, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let newTracks = state.Project.Tracks |> List.map (fun t -> if t = track then changeAudioFile t fileName else t) 
                { state with Project = { state.Project with Tracks = newTracks } }, Cmd.none
            else
                state, Cmd.none

        | ImportToolkitTemplate files ->
            if files.Length > 0 then
                let fileName = files.[0]

                let foundArrangements = ToolkitImporter.import fileName

                // Try to find an instrumental arrangement to read metadata from
                let instArrType = foundArrangements |> Map.tryFindKey (fun key _ -> (key &&& instrumentalArrangement) <> ArrangementType.Unknown)
                match instArrType with
                | Some instArrType ->
                    let instArrFile, _ = foundArrangements.[instArrType]
                    let instArr = RS2014Song.Load(instArrFile)
                    
                    let foldArrangements (state : Arrangement list) arrType (fileName, baseTone) =
                        let arrangement =
                            match arrType with
                            | t when isInstrumental t -> createInstrumental fileName baseTone
                            | _ -> { FileName = Some fileName
                                     ArrangementType = arrType
                                     Name = arrType.ToString()
                                     Data = None }
                        arrangement :: state

                    let arrangements = 
                        foundArrangements
                        |> Map.fold foldArrangements []
                        // Add any missing arrangements from the project's templates
                        |> addMissingArrangements state.Project.Templates

                    let newTrack = createTrack instArr arrangements
                    let tracks, templates = updateTracksAndTemplates state.Project.Tracks state.Project.Templates arrangements

                    let updatedProject = { state.Project with Tracks = tracks @ [ newTrack ]; Templates = templates }

                    { state with Project = updatedProject }, Cmd.ofMsg (ProjectArrangementsChanged(templates, updatedProject.CommonTones))
                | None ->
                    state, Cmd.none
            else
                state, Cmd.none

        | SelectTargetAudioFile -> 
            let targetFile = Dialogs.saveFileDialog "Select Target File" Dialogs.audioFileFilters (Some "combo.wav")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun f -> CombineAudioFiles f)

        | CombineAudioFiles targetFile ->
            if String.IsNullOrEmpty targetFile then
                // User canceled the dialog
                state, Cmd.none
            else
                let message = AudioCombiner.combineAudioFiles state.Project.Tracks targetFile
                { state with StatusMessage = message }, Cmd.none
        
        | SelectCombinationTargetFolder ->
            let targetFolder = Dialogs.openFolderDialog "Select Target Folder"
            state, Cmd.OfAsync.perform (fun _ -> targetFolder) () (fun f -> CombineArrangements f)

        | CombineArrangements targetFolder ->
            ArrangementCombiner.combineArrangements state.Project targetFolder
            { state with StatusMessage = "Arrangements combined." }, Cmd.none

        | SaveProject fileName ->
            if not (String.IsNullOrEmpty fileName) then
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
                options.WriteIndented <- true
                let json = JsonSerializer.Serialize(state.Project, options)
                File.WriteAllText(fileName, json)
            state, Cmd.none

        | OpenProject files ->
            if files.Length > 0 then
                let fileName = files.[0]
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

                // TODO: Check if all the files still exist

                let json = File.ReadAllText(fileName)
                let openedProject = JsonSerializer.Deserialize<CombinerProject>(json, options)

                // Generate the arrangement templates from the first track
                let templates = 
                    match openedProject.Tracks |> List.tryHead with
                    | Some head -> head.Arrangements |> List.map createTemplate
                    | None -> []

                { state with Project = { openedProject with Templates = templates } }, Cmd.ofMsg (ProjectArrangementsChanged(templates, openedProject.CommonTones))
            else
                state, Cmd.none

        | ProjectArrangementsChanged -> state, Cmd.none

        | SelectArrangementFile (trackIndex, arrIndex) ->
            let files = Dialogs.openFileDialog "Select Arrangement File" Dialogs.xmlFileFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun f -> ChangeArrangementFile (trackIndex, arrIndex, f))

        | ChangeArrangementFile (trackIndex, arrIndex, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let rootName = XmlHelper.GetRootElementName(fileName)
                let arrangement = state.Project.Tracks.[trackIndex].Arrangements.[arrIndex]

                match rootName, arrangement.ArrangementType with
                // For instrumental arrangements, read the tones from the file
                | "song", t when isInstrumental t ->
                    let baseTone, toneNames = getTones fileName
                    let data = {
                        Ordering = (arrangement.Data |> Option.get).Ordering
                        BaseTone = baseTone
                        ToneNames = toneNames
                        ToneReplacements = Map.empty }

                    let newArr = 
                        { FileName = Some fileName
                          ArrangementType = t
                          Name = arrangement.Name
                          Data = Some data }

                    let changeArrangement arrList =
                        arrList
                        |> List.mapi (fun i arr -> if i = arrIndex then newArr else arr)

                    let updatedTracks =
                        state.Project.Tracks
                        |> List.mapi (fun i t -> if i = trackIndex then { t with Arrangements = changeArrangement t.Arrangements } else t)
 
                    { state with Project = { state.Project with Tracks = updatedTracks } }, Cmd.none

                // For vocals and showl lights, just change the file name
                | "vocals", ArrangementType.Vocals
                | "vocals", ArrangementType.JVocals
                | "showlights", ArrangementType.ShowLights ->
                    let changeFile arrList =
                        arrList
                        |> List.mapi (fun i arr -> if i = arrIndex then { arr with FileName = Some fileName } else arr)

                    let newTracks =
                        state.Project.Tracks
                        |> List.mapi (fun i t -> if i = trackIndex then { t with Arrangements = changeFile t.Arrangements } else t)
 
                    { state with Project = { state.Project with Tracks = newTracks } }, Cmd.none

                | _ -> { state with StatusMessage = "Incorrect arrangement type" }, Cmd.none
            else
                state, Cmd.none
        
        | UpdateCombinationTitle newTitle ->
            { state with Project = { state.Project with CombinationTitle = newTitle } }, Cmd.none

    /// Creates the view for an arrangement.
    let private arrangementTemplate (arr : Arrangement) trackIndex arrIndex (commonTones : Map<string, string[]>) dispatch =
        let fileName = arr.FileName |> Option.defaultValue ""
        let color =
            match arr.FileName with
            | Some ->
                match arr.ArrangementType with
                | ArrangementType.Lead -> SolidColorBrush.Parse "#ff9242" :> ISolidColorBrush
                | ArrangementType.Rhythm | ArrangementType.Combo -> SolidColorBrush.Parse "#1ea334" :> ISolidColorBrush
                | ArrangementType.Bass -> SolidColorBrush.Parse "#0383b5" :> ISolidColorBrush
                | ArrangementType.Vocals | ArrangementType.JVocals -> Brushes.Yellow
                | ArrangementType.ShowLights -> Brushes.Violet
                | _ -> Brushes.GhostWhite
            | None -> Brushes.Gray

        Border.create [
            Border.borderThickness 1.0
            Border.borderBrush color
            Border.width 140.0
            Border.child (
                StackPanel.create [
                    StackPanel.verticalAlignment VerticalAlignment.Top
                    StackPanel.classes [ "arrangement" ]
                    StackPanel.children [
                        // Icon & Name
                        yield StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 2.0
                            StackPanel.children [
                                Path.create [
                                    Path.fill color
                                    Path.data (
                                        match arr.ArrangementType with
                                        | t when isInstrumental t -> Icons.pick
                                        | t when isVocals t -> Icons.microphone
                                        | _ -> Icons.spotlight)
                                ]
                                TextBlock.create [
                                    TextBlock.classes [ "h2"]
                                    TextBlock.text arr.Name
                                    TextBlock.foreground color
                                ]
                            ]
                        ]
                        // Short File Name
                        yield TextBlock.create [
                            TextBlock.text (System.IO.Path.GetFileNameWithoutExtension(fileName))
                            TextBlock.foreground Brushes.DarkGray
                            ToolTip.tip fileName
                        ]
                        // Open File Button
                        yield Button.create [
                            Button.width 100.0
                            Button.content (
                                match fileName with
                                    | s when s.Length > 0 -> "Change..."
                                    | _ -> "Open..."
                            )
                            Button.onClick (fun _ -> dispatch (SelectArrangementFile(trackIndex, arrIndex)))
                        ]

                        // Optional Tone Controls
                        match arr.Data with
                        | Some instArr ->
                            let getToneNames() = 
                                match Map.tryFind arr.Name commonTones with
                                | Some names -> names.AsSpan(1).ToArray() |> Array.filter (fun name -> not (String.IsNullOrEmpty(name)))
                                | None -> [||]

                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.isVisible (instArr.ToneNames.Length = 0)
                                ComboBox.dataItems (getToneNames())
                                ComboBox.selectedItem instArr.BaseTone
                                // TODO: on changed
                                // TODO: hidden/disabled on first track
                                ToolTip.tip "Base Tone"
                            ]
                            // Edit Replacement Tones Button
                            yield Button.create [
                                Button.content "Tones"
                                Button.width 100.0
                                Button.isVisible (instArr.ToneNames.Length > 0)
                                // TODO: on click
                                // TODO: warning color
                            ]
                        | _ -> () // Do nothing
                    ]
                ]
            )
        ]
       
    /// Creates the view for a track.
    let private trackTemplate (track : Track) index commonTones dispatch =
        Border.create [
            Border.classes [ "track" ]
            Border.child (
                DockPanel.create [
                    DockPanel.margin (5.0, 0.0, 0.0, 0.0)
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.dock Dock.Top
                            StackPanel.children [
                                TextBlock.create [
                                    TextBlock.text (sprintf "%i. %s" (index + 1) track.Title)
                                    TextBlock.classes [ "h1" ]
                                ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                // Delete button
                                Button.create [
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.fontSize 18.0
                                    Button.margin (0.0, 0.0, 20.0, 0.0)
                                    Button.content "X"
                                    Button.classes [ "close" ]
                                    Button.onClick (fun _ -> dispatch (RemoveTrackAt index))
                                ]

                                // Audio Part
                                StackPanel.create [
                                    StackPanel.width 150.0
                                    StackPanel.classes [ "part" ]
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                                            TextBlock.text "Audio"
                                            TextBlock.classes [ "h2" ]
                                        ]
                                        // Audio File Name
                                        TextBlock.create [
                                            TextBlock.foreground Brushes.DarkGray
                                            TextBlock.maxWidth 100.0
                                            TextBlock.text (track.AudioFile |> Option.defaultValue "" |> Path.GetFileName)
                                            ToolTip.tip (track.AudioFile |> Option.defaultValue "")
                                        ]
                                        // Open Audio File Button
                                        Button.create [
                                            Button.content (
                                                match track.AudioFile with
                                                | None -> "Open..."
                                                | Some _ -> "Change...")
                                            Button.width 100.0
                                            Button.onClick (fun _ -> dispatch (ChangeAudioFile(track)))
                                        ]

                                        // Trim Part
                                        StackPanel.create [
                                            //StackPanel.width 90.0
                                            StackPanel.classes [ "part" ]
                                            StackPanel.orientation Orientation.Horizontal
                                            StackPanel.horizontalAlignment HorizontalAlignment.Center
                                            StackPanel.children [
                                                // Hide if this is the first track
                                                if index <> 0 then
                                                    yield TextBlock.create [ 
                                                        TextBlock.classes [ "h2" ]
                                                        TextBlock.text "Trim:"
                                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                                    ]
                                                    yield NumericUpDown.create [
                                                        NumericUpDown.value (track.TrimAmount |> double)
                                                        NumericUpDown.minimum 0.0
                                                        NumericUpDown.verticalAlignment VerticalAlignment.Center
                                                        NumericUpDown.width 75.0
                                                        NumericUpDown.formatString "F3"
                                                        ToolTip.tip "Sets the amount of time in seconds to be trimmed from the start of the audio and each arrangements."
                                                    ]
                                                    yield TextBlock.create [
                                                        TextBlock.margin (2.0, 0.0, 0.0, 0.0)
                                                        TextBlock.text "s"
                                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                                    ]
                                            ]
                                        ]
                                    ]
                                ]

                                   // Arrangements
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.spacing 10.0
                                    StackPanel.children (List.mapi (fun i item -> arrangementTemplate item index i commonTones dispatch :> IView) track.Arrangements)
                                ] 
                            ]
                        ]
                    ]
                ]
            )
        ]

    /// Creates the track list view.
    let view (state: State) (dispatch : Msg -> Unit) : IView list =
        [
            // Status Bar with Message
            Border.create [
                Border.classes [ "statusbar" ]
                Border.minHeight 25.0
                Border.background "Black"
                Border.dock Dock.Bottom
                Border.padding 5.0
                Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
            ]

            // Bottom Panel
            Grid.create [
                DockPanel.dock Dock.Bottom
                Grid.margin 15.0
                Grid.columnDefinitions "auto,*,auto"
                Grid.children [
                    // Left Side Panel
                    StackPanel.create [
                        Grid.column 0
                        StackPanel.children [
                            // Combine Audio Files Button
                            Button.create [
                                Button.content "Combine Audio"
                                Button.verticalAlignment VerticalAlignment.Center
                                Button.fontSize 20.0
                                Button.onClick (fun _ -> dispatch SelectTargetAudioFile)
                                // Only enable the button if there is more than one track and every track has an audio file
                                Button.isEnabled (state.Project.Tracks.Length > 1 && state.Project.Tracks |> List.forall (fun track -> track.AudioFile |> Option.isSome))
                            ]
                            // Combine Audio Error Text
                            //TextBlock.create [
                            //    TextBlock.fontSize 20.0
                            //    TextBlock.foreground "red"
                            //    TextBlock.horizontalAlignment HorizontalAlignment.Center
                            //    TextBlock.text "ERROR"
                            //]
                        ]
                    ]

                    // Right Side Panel
                    StackPanel.create [
                        Grid.column 2
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.spacing 10.0
                        StackPanel.children [
                            // Combined Title Text Box
                            TextBox.create [
                                TextBox.watermark "Combined Title"
                                TextBox.text state.Project.CombinationTitle
                                TextBox.onTextChanged (fun text -> dispatch (UpdateCombinationTitle text))
                                TextBox.verticalAlignment VerticalAlignment.Center
                                TextBox.width 200.0
                                ToolTip.tip "Combined Title"
                            ]

                            // Options Panel
                            StackPanel.create [
                                StackPanel.verticalAlignment VerticalAlignment.Center
                                StackPanel.children [
                                    // Coerce Phrases Check Box
                                    CheckBox.create [
                                        CheckBox.content "Coerce to 100 Phrases"
                                        CheckBox.isChecked state.Project.CoercePhrases
                                        // TODO: Binding
                                        ToolTip.tip "Will combine phrases and sections so the resulting arrangements have a max of 100 phrases and sections."
                                    ]
                                    // Add Track Names to Lyrics Check Box
                                    CheckBox.create [
                                        CheckBox.content "Add Track Names to Lyrics"
                                        CheckBox.isChecked state.Project.AddTrackNamesToLyrics
                                        // TODO: Binding
                                        CheckBox.margin (0.0, 5.0, 0.0, 0.0) 
                                    ]
                                ]
                            ]

                            // Combine Arrangements Button
                            Button.create [
                                Button.content "Combine Arrangements"
                                Button.onClick (fun _ -> dispatch SelectCombinationTargetFolder)
                                Button.verticalAlignment VerticalAlignment.Center
                                Button.fontSize 20.0
                                Button.isEnabled (state.Project.Tracks.Length > 1)
                            ]
                        ]
                    ]
                ]
            ]

            // List of tracks
            ScrollViewer.create [
                ScrollViewer.content (
                    StackPanel.create [
                        StackPanel.children (List.mapi (fun i item -> trackTemplate item i state.Project.CommonTones dispatch :> IView) state.Project.Tracks)
                    ] 
                )
            ]
            
            //ListBox.create [
            //    ListBox.dataItems state.tracks
            //    ListBox.margin 5.0
            //    ListBox.itemTemplate (DataTemplateView<Track>.create(fun item -> trackTemplate item dispatch))
            //    //ListBox.virtualizationMode ItemVirtualizationMode.None
            //]
        ]
        
        