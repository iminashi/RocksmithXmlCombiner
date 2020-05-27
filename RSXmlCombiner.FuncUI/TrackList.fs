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

    let rand = Random()

    /// The state of the program. MVU model.
    type State = {
        Tracks : Track list
        [<JsonIgnoreAttribute>]
        StatusMessage : string
        CombinationTitle : string
        CoercePhrases : bool
        AddTrackNamesToLyrics : bool }

    /// Initialization function.
    let init = {
        Tracks = []
        StatusMessage = ""
        CombinationTitle = ""
        CoercePhrases = true
        AddTrackNamesToLyrics = true }, Cmd.none

    // Message
    type Msg =
    | AddTrackSelectFiles
    | AddTrack of arrangements : string[]
    | RemoveTrackAt of index : int
    | NewProject
    | OpenProject of fileNames : string[]
    | SelectOpenProjectFile
    | SaveProject of fileName : string
    | SelectSaveProjectTarget
    | ChangeAudioFile of track : Track
    | ChangeAudioFileResult of track : Track * newFile:string[]
    | SelectTargetAudioFile
    | CombineAudioFiles of targetFile : string
    | SelectToolkitTemplate
    | ImportToolkitTemplate of fileNames : string[]

    let changeAudioFile track newFile =
        { track with AudioFile = Some(newFile) }

    let createNewTrack state arrangementFileNames =
        let instArrFile = arrangementFileNames |> Array.tryFind (fun a -> XmlHelper.ValidateRootElement(a, "song"))
        match instArrFile with
        | None -> 
            { state with StatusMessage = "Please select at least one instrumental Rocksmith arrangement." }, Cmd.none
        | Some instArr ->
            let alreadyHasShowlights arrs =
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

            let song = RS2014Song.Load(instArr)
            let newTrack = {
                Title = song.Title
                AudioFile = None
                SongLength = song.SongLength
                TrimAmount = double song.StartBeat
                Arrangements = trackArrangements |> List.sortBy (fun a -> a.ArrangementType) }

            { state with Tracks = state.Tracks @ [ newTrack ] }, Cmd.none


    /// Updates the model according to the message content.
    let update (msg: Msg) (state: State) =
        match msg with
        | AddTrackSelectFiles ->
            let selectFiles = Dialogs.openFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter true
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) () (fun files -> AddTrack files)

        | AddTrack arrangements -> createNewTrack state arrangements

        | NewProject -> init

        | RemoveTrackAt index ->
            { state with Tracks = state.Tracks |> List.except [ state.Tracks.[index] ]}, Cmd.none

        | ChangeAudioFile track ->
            let selectFiles = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFilters false
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) track (fun files -> ChangeAudioFileResult(track, files))

        | ChangeAudioFileResult (track, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let newTracks = state.Tracks |> List.map (fun t -> if t = track then changeAudioFile t fileName else t) 
                { state with Tracks = newTracks }, Cmd.none
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
                let instArrType = foundArrangements |> Map.tryFindKey (fun key _ -> (key &&& InstrumentalArrangement) <> ArrangementType.Unknown)
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

                    let arrangements = foundArrangements |> Map.fold foldArrangements [] 

                    let newTrack = {
                        Title = instArr.Title
                        TrimAmount = double instArr.StartBeat
                        AudioFile = None
                        SongLength = instArr.SongLength
                        Arrangements = arrangements |> List.sortBy (fun a -> a.ArrangementType) }

                    { state with Tracks = state.Tracks @ [ newTrack ] }, Cmd.none
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
                let message = AudioCombiner.combineAudioFiles state.Tracks targetFile
                { state with StatusMessage = message }, Cmd.none

        | SelectSaveProjectTarget ->
            let targetFile = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter (Some "combo.rscproj")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun f -> SaveProject f)

        | SaveProject fileName ->
            if not (String.IsNullOrEmpty fileName) then
                let options = JsonSerializerOptions()
                options.Converters.Add(JsonFSharpConverter())
                options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
                options.WriteIndented <- true
                let json = JsonSerializer.Serialize(state, options)
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
                let newState = JsonSerializer.Deserialize<State>(json, options)
                newState, Cmd.none
            else
                state, Cmd.none

    /// Creates the view for an arrangement.
    let private arrangementTemplate (arr : Arrangement) dispatch =
        let name = arr.Name
        let fileName = arr.FileName |> Option.defaultValue ""
        let color =
            match arr.FileName with
            | Some ->
                match arr.ArrangementType with
                | ArrangementType.Lead -> Brushes.Orange
                | ArrangementType.Rhythm | ArrangementType.Combo -> Brushes.DarkGreen
                | ArrangementType.Bass -> Brushes.LightBlue
                | ArrangementType.Vocals | ArrangementType.JVocals -> Brushes.Yellow
                | ArrangementType.ShowLights -> Brushes.Pink
                | _ -> Brushes.GhostWhite
            | None -> Brushes.Gray

        Border.create [
            Border.borderThickness 1.0
            Border.borderBrush color
            Border.child (
                StackPanel.create [
                    StackPanel.verticalAlignment VerticalAlignment.Top
                    StackPanel.classes [ "arrangement" ]
                    StackPanel.children [
                        // Name
                        yield TextBlock.create [
                            TextBlock.classes [ "h2"]
                            TextBlock.text name
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
                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.isVisible (instArr.ToneNames.Length = 0)
                                // Items?
                                ToolTip.tip "Base Tone"
                            ]
                            // Edit Replacement Tones Button
                            yield Button.create [
                                Button.content "Tones"
                                Button.width 100.0
                                Button.isVisible (instArr.ToneNames.Length > 0)
                                // on click
                                // warning color
                            ]
                        | _ -> () // Do nothing
                    ]
                ]
            )
        ]
       
    /// Creates the view for a track.
    let private trackTemplate (track : Track) index dispatch =
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
                                        // Hide if this s the first track
                                        if index <> 0 then
                                            yield TextBlock.create [ 
                                                TextBlock.classes [ "h2" ]
                                                TextBlock.text "Trim:"
                                            ]
                                            yield NumericUpDown.create [
                                                NumericUpDown.value track.TrimAmount
                                                NumericUpDown.minimum 0.0
                                                NumericUpDown.horizontalAlignment HorizontalAlignment.Left
                                                NumericUpDown.width 75.0
                                                NumericUpDown.formatString "F3"
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
                                    StackPanel.children (List.map (fun item -> arrangementTemplate item dispatch :> IView) track.Arrangements)
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
                DockPanel.create [
                    DockPanel.dock Dock.Top
                    DockPanel.children [
                        // Left Side Panel
                        StackPanel.create [
                            StackPanel.dock Dock.Left
                            StackPanel.orientation Orientation.Vertical
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
                                Button.create [
                                    Button.content "Edit Common Tones"
                                    Button.horizontalAlignment HorizontalAlignment.Center
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.margin (15.0, 15.0, 15.0, 0.0)
                                    Button.fontSize 18.0
                                    // TODO: On Click
                                ]
                            ]
                        ]

                        // Right Side Panel
                        StackPanel.create [
                            StackPanel.dock Dock.Right
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
                                    Button.onClick (fun _ -> dispatch SelectSaveProjectTarget)
                                    Button.hotKey (KeyGesture.Parse "Ctrl+S")
                                ]
                            ]
                        ]

                        TextBlock.create []
                    ]
                ]

                // Status Bar with Message
                Border.create [
                    Border.dock Dock.Bottom
                    Border.padding 5.0
                    Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                ]

                // Bottom Panel
                DockPanel.create [
                    DockPanel.dock Dock.Bottom
                    DockPanel.margin 15.0
                    DockPanel.children [
                        // Left Side Panel
                        StackPanel.create [
                            StackPanel.dock Dock.Left
                            StackPanel.children [
                                // Combine Audio Files Button
                                Button.create [
                                    Button.content "Combine Audio"
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.fontSize 20.0
                                    Button.onClick (fun _ -> dispatch SelectTargetAudioFile)
                                    Button.isEnabled (state.Tracks.Length > 1 && state.Tracks |> List.forall (fun track -> track.AudioFile |> Option.isSome))
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
                            StackPanel.dock Dock.Right
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 10.0
                            StackPanel.children [
                                // Combined Title Text Box
                                TextBox.create [
                                    TextBox.watermark "Combined Title"
                                    TextBox.text state.CombinationTitle
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
                                            CheckBox.isChecked state.CoercePhrases
                                            // TODO: Binding
                                            ToolTip.tip "Will combine phrases and sections so the resulting arrangements have a max of 100 phrases and sections."
                                        ]
                                        // Add Track Names to Lyrics Check Box
                                        CheckBox.create [
                                            CheckBox.content "Add Track Names to Lyrics"
                                            CheckBox.isChecked state.AddTrackNamesToLyrics
                                            // TODO: Binding
                                            CheckBox.margin (0.0, 5.0, 0.0, 0.0) 
                                        ]
                                    ]
                                ]

                                // Combine Arrangements Button
                                Button.create [
                                    Button.content "Combine Arrangements"
                                    Button.verticalAlignment VerticalAlignment.Center
                                    Button.fontSize 20.0
                                    Button.isEnabled (state.Tracks.Length > 1)
                                ]
                            ]
                        ]

                        TextBlock.create []
                    ]
                ]

                // List of tracks
                ScrollViewer.create [
                    ScrollViewer.content (
                        StackPanel.create [
                            StackPanel.children (List.mapi (fun i item -> trackTemplate item i dispatch :> IView) state.Tracks)
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
        