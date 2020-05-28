namespace RSXmlCombiner.FuncUI

module TrackList =
    open System
    open System.Text.Json
    open System.Text.Json.Serialization
    open System.IO
    open Elmish
    open Avalonia.Media
    open Avalonia.Controls
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
    | AddTrackSelectFiles
    | AddTrack of arrangements : string[]
    | RemoveTrackAt of index : int
    | NewProject
    | OpenProject of fileNames : string[]
    | SelectOpenProjectFile
    | SaveProject of fileName : string
    | SelectSaveProjectFile
    | ChangeAudioFile of track : Track
    | ChangeAudioFileResult of track : Track * newFile:string[]
    | SelectTargetAudioFile
    | CombineAudioFiles of targetFile : string
    | SelectToolkitTemplate
    | ImportToolkitTemplate of fileNames : string[]
    | SelectCombinationTargetFolder
    | CombineArrangements of targetFolder : string
    | ProjectArrangementsChanged

    let changeAudioFile track newFile = { track with AudioFile = Some newFile }

    let createTemplate arr =
        { Name = arr.Name; ArrangementType = arr.ArrangementType; FileName = None; Data = None }

    let updateTemplates (current : Arrangement list) (arrangements : Arrangement list) =
        let newTemplates =
            arrangements
            |> List.filter (fun arr -> current |> List.exists (fun temp -> arr.Name = temp.Name) |> not)
            |> List.map createTemplate
        current @ newTemplates

    let getMissingArrangements (arrs : Arrangement list) temps =
        temps
        |> List.except (arrs |> Seq.map createTemplate)

    let updateTrack templates track =
        let newArrangements = 
            track.Arrangements @ (getMissingArrangements track.Arrangements templates)
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

            let mutable trackArrangements = []

            for fileName in arrangementFileNames do
                match XmlHelper.GetRootElementName(fileName) with
                | "song" ->
                    trackArrangements <- (createInstrumental fileName None) :: trackArrangements
                | "vocals" ->
                    trackArrangements <- { Name = "Vocals"; FileName = Some fileName; ArrangementType = ArrangementType.Vocals; Data = None  } :: trackArrangements
                | "showlights" when trackArrangements |> alreadyHasShowlights |> not ->
                    trackArrangements <- { Name = "Show Lights"; FileName = Some fileName; ArrangementType = ArrangementType.ShowLights; Data = None } :: trackArrangements
                //| "showlights" -> Cannot have more than one show lights arrangement
                | _ -> () // StatusMessage = "Unknown arrangement type for file {Path.GetFileName(arr)}";

            // Add any missing arrangements from the project's templates
            if state.Project.Templates.Length > 0 then
                trackArrangements <- trackArrangements @ (getMissingArrangements trackArrangements state.Project.Templates)

            let song = RS2014Song.Load(instArr)
            let newTrack = createTrack song trackArrangements
            let tracks, templates = updateTracksAndTemplates state.Project.Tracks state.Project.Templates trackArrangements

            let updatedProject = { state.Project with Tracks = tracks @ [ newTrack ]; Templates = templates }

            { state with Project = updatedProject }, Cmd.ofMsg ProjectArrangementsChanged

    /// Updates the model according to the message content.
    let update (msg: Msg) (state: State) =
        match msg with
        | AddTrackSelectFiles ->
            let selectFiles = Dialogs.openFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter true
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) () (fun files -> AddTrack files)

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

        | SelectToolkitTemplate ->
            let files = Dialogs.openFileDialog "Select Toolkit Template" Dialogs.toolkitTemplateFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun f -> ImportToolkitTemplate f)

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
                            | ArrangementType.Lead
                            | ArrangementType.Bass
                            | ArrangementType.Rhythm
                            | ArrangementType.Combo ->
                                createInstrumental fileName baseTone
                            | _ -> { FileName = Some fileName
                                     ArrangementType = arrType
                                     Name = arrType.ToString()
                                     Data = None }
                        arrangement :: state

                    let mutable arrangements = foundArrangements |> Map.fold foldArrangements []

                    // Add any missing arrangements from the project's templates
                    if state.Project.Templates.Length > 0 then
                        arrangements <- arrangements @ (getMissingArrangements arrangements state.Project.Templates)

                    let newTrack = createTrack instArr arrangements
                    let tracks, templates = updateTracksAndTemplates state.Project.Tracks state.Project.Templates arrangements

                    let updatedProject = { state.Project with Tracks = tracks @ [ newTrack ]; Templates = templates }

                    { state with Project = updatedProject }, Cmd.ofMsg ProjectArrangementsChanged
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

        | SelectSaveProjectFile ->
            let targetFile = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter (Some "combo.rscproj")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun f -> SaveProject f)

        | SaveProject fileName ->
            if not (String.IsNullOrEmpty fileName) then
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
                options.WriteIndented <- true
                let json = JsonSerializer.Serialize(state.Project, options)
                File.WriteAllText(fileName, json)
            state, Cmd.none

        | SelectOpenProjectFile ->
            let files = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun f -> OpenProject f)

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

                { state with Project = { openedProject with Templates = templates } }, Cmd.ofMsg ProjectArrangementsChanged
            else
                state, Cmd.none

        | ProjectArrangementsChanged -> state, Cmd.none

    /// Creates the view for an arrangement.
    let private arrangementTemplate (arr : Arrangement) (commonTones : Map<string, string[]>) dispatch =
        let fileName = arr.FileName |> Option.defaultValue ""
        let color =
            match arr.FileName with
            | Some ->
                match arr.ArrangementType with
                | ArrangementType.Lead -> Brushes.Orange
                | ArrangementType.Rhythm | ArrangementType.Combo -> Brushes.Green
                | ArrangementType.Bass -> Brushes.LightBlue
                | ArrangementType.Vocals | ArrangementType.JVocals -> Brushes.Yellow
                | ArrangementType.ShowLights -> Brushes.Pink
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
                        // Name
                        yield TextBlock.create [
                            TextBlock.classes [ "h2"]
                            TextBlock.text arr.Name
                            TextBlock.foreground color
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
                        ]

                        // Optional Tone Controls
                        match arr.Data with
                        | Some instArr ->
                            let getToneNames() = 
                                match Map.tryFind arr.Name commonTones with
                                | Some names -> names |> Array.filter (fun name -> not (String.IsNullOrEmpty(name)))
                                | None -> [||]

                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.isVisible (instArr.ToneNames.Length = 0)
                                ComboBox.dataItems (getToneNames())
                                // TODO: on changed
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
            Border.borderBrush Brushes.SlateGray
            Border.background Brushes.Black
            Border.borderThickness 3.0
            Border.padding 5.0
            Border.margin 5.0
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
                                    ]
                                ]

                                // Trim Part
                                StackPanel.create [
                                    StackPanel.width 90.0
                                    StackPanel.classes [ "part" ]
                                    StackPanel.children [
                                        // Hide if this is the first track
                                        if index <> 0 then
                                            yield TextBlock.create [ 
                                                TextBlock.classes [ "h2" ]
                                                TextBlock.text "Trim:"
                                            ]
                                            yield NumericUpDown.create [
                                                NumericUpDown.value (track.TrimAmount |> double)
                                                NumericUpDown.minimum 0.0
                                                NumericUpDown.horizontalAlignment HorizontalAlignment.Left
                                                NumericUpDown.width 75.0
                                                NumericUpDown.formatString "F3"
                                                ToolTip.tip "Sets the amount of time in seconds to be trimmed from the start of the audio and the arrangements."
                                            ]
                                            yield TextBlock.create [
                                                TextBlock.margin (10.0, 0.0, 0.0, 0.0)
                                                TextBlock.text "seconds"
                                            ]
                                    ]
                                ]

                                // Arrangements
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.spacing 10.0
                                    StackPanel.children (List.map (fun item -> arrangementTemplate item commonTones dispatch :> IView) track.Arrangements)
                                ] 
                            ]
                        ]
                    ]
                ]
            )
        ]

    /// Creates the track list view.
    let view (state: State) (dispatch : Msg -> Unit) =
        DockPanel.create [
            DockPanel.children [
                // Top Panel
                Grid.create [
                    DockPanel.dock Dock.Top
                    Grid.columnDefinitions "auto,*,auto"
                    Grid.children [
                        // Left Side Panel
                        StackPanel.create [
                            Grid.column 0
                            StackPanel.children [
                                // Top Buttons
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.children [
                                        Button.create [
                                            Button.content "Add Track..."
                                            Button.onClick (fun _ -> dispatch AddTrackSelectFiles)
                                            Button.horizontalAlignment HorizontalAlignment.Left
                                            Button.verticalAlignment VerticalAlignment.Center
                                            Button.margin (15.0, 15.0, 15.0, 0.0)
                                            Button.fontSize 18.0
                                        ]
                                        Button.create [
                                            Button.content "Import..."
                                            Button.onClick (fun _ -> dispatch SelectToolkitTemplate)
                                            Button.horizontalAlignment HorizontalAlignment.Left
                                            Button.verticalAlignment VerticalAlignment.Center
                                            Button.margin (0.0, 15.0, 15.0, 0.0)
                                            Button.fontSize 18.0
                                         ]
                                    ]
                                ]
                                // Bottom Button
                                //Button.create [
                                //    Button.content "Edit Common Tones"
                                //    Button.horizontalAlignment HorizontalAlignment.Center
                                //    Button.verticalAlignment VerticalAlignment.Center
                                //    Button.margin (15.0, 15.0, 15.0, 0.0)
                                //    Button.fontSize 18.0
                                //    // TODO: On Click
                                //]
                                ComboBox.create [
                                    ComboBox.dataItems state.Project.Templates
                                ]
                            ]
                        ]

                        // Right Side Panel
                        StackPanel.create [
                            Grid.column 2
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                Button.create [
                                    Button.content "New Project"
                                    Button.onClick (fun _ -> dispatch NewProject)
                                    Button.horizontalAlignment HorizontalAlignment.Right
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.margin (15.0, 15.0, 0.0, 0.0)
                                    Button.fontSize 18.0
                                    Button.hotKey (KeyGesture.Parse "Ctrl+N") // TODO: Hook up hot keys
                                ]
                                Button.create [
                                    Button.content "Open Project..."
                                    Button.horizontalAlignment HorizontalAlignment.Right
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.margin (15.0, 15.0, 0.0, 0.0)
                                    Button.fontSize 18.0
                                    Button.onClick (fun _ -> dispatch SelectOpenProjectFile)
                                    Button.hotKey (KeyGesture.Parse "Ctrl+O")
                                ]
                                Button.create [
                                    Button.content "Save Project..."
                                    Button.horizontalAlignment HorizontalAlignment.Right
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.margin (15.0, 15.0, 15.0, 0.0)
                                    Button.fontSize 18.0
                                    Button.onClick (fun _ -> dispatch SelectSaveProjectFile)
                                    Button.hotKey (KeyGesture.Parse "Ctrl+S")
                                ]
                            ]
                        ]
                    ]
                ]

                // Status Bar with Message
                Border.create [
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
                                    // TODO: Binding
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
        ]
        