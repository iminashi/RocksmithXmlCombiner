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
    type State = { Project : CombinerProject }

    /// Initial state
    let init = { Project = emptyProject }, Cmd.none

    // Message
    type Msg =
    | AddTrack of arrangements : string[]
    | RemoveTrackAt of index : int
    | NewProject
    | OpenProject of fileNames : string[]
    | SaveProject of fileName : string
    | ChangeAudioFile of trackIndex : int
    | ChangeAudioFileResult of trackIndex : int * newFile:string[]
    | ImportToolkitTemplate of fileNames : string[]
    | ProjectArrangementsChanged of templates : Arrangement list
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * string[]
    | ArrangementBaseToneChanged of trackIndex : int * arrIndex : int * baseTone : string
    | StatusMessage of string
    | UpdateCommonTones of Map<string, string[]>

    let changeAudioFile track newFile = { track with AudioFile = Some newFile }

    let createTemplate arr =
        { Name = arrTypeHumanized arr.ArrangementType; ArrangementType = arr.ArrangementType; FileName = None; Data = None }

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
            state, Cmd.ofMsg (StatusMessage "Please select at least one instrumental Rocksmith arrangement.")

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

            { state with Project = updatedProject }, Cmd.ofMsg (ProjectArrangementsChanged(templates))

    /// Updates the model according to the message content.
    let update (msg: Msg) (state: State) =
        match msg with
        | StatusMessage -> state, Cmd.none

        | AddTrack fileNames -> 
            if fileNames.Length > 0 then
                addNewTrack state fileNames
            else
                state, Cmd.none

        | NewProject -> init

        | RemoveTrackAt index ->
            { state with Project = { state.Project with Tracks = state.Project.Tracks |> List.except [ state.Project.Tracks.[index] ]}}, Cmd.none

        | ChangeAudioFile trackIndex ->
            let selectFiles = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFilters false
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) trackIndex (fun files -> ChangeAudioFileResult(trackIndex, files))

        | ChangeAudioFileResult (trackIndex, files) ->
            if files.Length > 0 then
                let fileName = files.[0]
                let newTracks = state.Project.Tracks |> List.mapi (fun i t -> if i = trackIndex then changeAudioFile t fileName else t) 
                { state with Project = { state.Project with Tracks = newTracks } }, Cmd.none
            else
                state, Cmd.none

        | ImportToolkitTemplate files ->
            if files.Length > 0 then
                let fileName = files.[0]

                let foundArrangements = ToolkitImporter.import fileName

                // Try to find an instrumental arrangement to read metadata from
                let instArrType = foundArrangements |> Map.tryFindKey (fun arrType _ -> isInstrumental arrType)
                match instArrType with
                | Some instArrType ->
                    let instArrFile, _ = foundArrangements.[instArrType]
                    
                    let foldArrangements (state : Arrangement list) arrType (fileName, baseTone) =
                        let arrangement =
                            match arrType with
                            | t when isInstrumental t -> createInstrumental fileName baseTone
                            | _ -> { FileName = Some fileName
                                     ArrangementType = arrType
                                     Name = arrTypeHumanized arrType
                                     Data = None }
                        arrangement :: state

                    let arrangements = 
                        foundArrangements
                        |> Map.fold foldArrangements []
                        // Add any missing arrangements from the project's templates
                        |> addMissingArrangements state.Project.Templates

                    let instArr = RS2014Song.Load(instArrFile)
                    let newTrack = createTrack instArr arrangements
                    let tracks, templates = updateTracksAndTemplates state.Project.Tracks state.Project.Templates arrangements

                    let updatedProject = { state.Project with Tracks = tracks @ [ newTrack ]; Templates = templates }

                    { state with Project = updatedProject }, Cmd.ofMsg (ProjectArrangementsChanged(templates))
                | None ->
                    state, Cmd.none
            else
                state, Cmd.none

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

                { state with Project = { openedProject with Templates = templates } }, Cmd.none
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
                // For instrumental arrangements, create arrangement from the file
                | "song", t when isInstrumental t ->
                    let newArr = { createInstrumental fileName None with ArrangementType = t }

                    let changeArrangement arrList =
                        arrList
                        |> List.mapi (fun i arr -> if i = arrIndex then newArr else arr)

                    let updatedTracks =
                        state.Project.Tracks
                        |> List.mapi (fun i t -> if i = trackIndex then { t with Arrangements = changeArrangement t.Arrangements } else t)
 
                    { state with Project = { state.Project with Tracks = updatedTracks } }, Cmd.none

                // For vocals and show lights, just change the file name
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

                | _ -> state, Cmd.ofMsg (StatusMessage "Incorrect arrangement type")
            else
                state, Cmd.none

        | ArrangementBaseToneChanged (trackIndex, arrIndex, baseTone) ->
            let arrangement = state.Project.Tracks.[trackIndex].Arrangements.[arrIndex]
            let data = {
                Ordering = (arrangement.Data |> Option.get).Ordering
                BaseTone = Some baseTone
                ToneNames = (arrangement.Data |> Option.get).ToneNames
                ToneReplacements = Map.empty }

            let changeArrangement arrList =
                arrList
                |> List.mapi (fun i arr -> if i = arrIndex then { arr with Data = Some data } else arr)

            let updatedTracks =
                state.Project.Tracks
                |> List.mapi (fun i t -> if i = trackIndex then { t with Arrangements = changeArrangement t.Arrangements } else t)

            { state with Project = { state.Project with Tracks = updatedTracks } }, Cmd.none

        | UpdateCommonTones commonTones ->
            { state with Project = { state.Project with CommonTones = commonTones } }, Cmd.none


    /// Creates the view for an arrangement.
    let private arrangementTemplate (arr : Arrangement) trackIndex arrIndex (commonTones : Map<string, string[]>) dispatch =
        let fileName = arr.FileName
        let color =
            match fileName with
            | Some ->
                match arr.ArrangementType with
                | ArrangementType.Lead -> CustomBrushes.lead
                | ArrangementType.Rhythm | ArrangementType.Combo -> CustomBrushes.rhythm
                | ArrangementType.Bass -> CustomBrushes.bass
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
                            StackPanel.spacing 4.0
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
                        // File Name
                        yield TextBlock.create [
                            if fileName |> Option.isSome then
                                yield TextBlock.text (Path.GetFileNameWithoutExtension(fileName |> Option.get))
                            else
                                yield TextBlock.text "No file"
                            yield TextBlock.foreground Brushes.DarkGray
                            yield TextBlock.cursor (Cursor(StandardCursorType.Hand))
                            yield TextBlock.onTapped (fun _ -> dispatch (SelectArrangementFile(trackIndex, arrIndex)))
                            yield ToolTip.tip (fileName |> Option.defaultValue "Click to select file")
                        ]

                        // Optional Tone Controls
                        match arr.Data with
                        | Some instArr ->
                            let getToneNames() = 
                                match Map.tryFind arr.Name commonTones with
                                | Some names ->
                                    match names |> Array.tryFindIndexBack (fun t -> not (String.IsNullOrEmpty(t))) with
                                    | Some lastNonNullIndex -> names.[1..lastNonNullIndex] // Exclude the first one (Base Tone)
                                    | None -> names.[1..]
                                | None -> [||]

                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.isVisible (instArr.ToneNames.Length = 0 && trackIndex <> 0)
                                ComboBox.dataItems (getToneNames())
                                ComboBox.selectedItem (instArr.BaseTone |> Option.defaultValue "")
                                ComboBox.onSelectedItemChanged (fun obj -> dispatch (ArrangementBaseToneChanged(trackIndex, arrIndex, string obj)))
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
                                //Path.create [
                                //    Path.data Icons.close
                                //    Path.fill "Red"
                                //    Path.margin (0.0, 0.0, 20.0, 0.0)
                                //    Path.verticalAlignment VerticalAlignment.Center
                                //    Path.onTapped (fun _ -> dispatch (RemoveTrackAt index))
                                //    Path.classes [ "close" ]
                                //    Path.cursor (Cursor(StandardCursorType.Hand))
                                //    Path.renderTransform (ScaleTransform(1.5, 1.5))
                                //]
    
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
                                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                                            TextBlock.foreground Brushes.DarkGray
                                            TextBlock.maxWidth 100.0
                                            TextBlock.cursor (Cursor(StandardCursorType.Hand))
                                            TextBlock.onTapped (fun _ -> dispatch (ChangeAudioFile(index)))
                                            TextBlock.text (track.AudioFile |> Option.defaultValue "None selected" |> Path.GetFileName)
                                            ToolTip.tip (track.AudioFile |> Option.defaultValue "Click to select a file.")
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
    let view (state: State) (dispatch : Msg -> Unit) =
        // List of tracks
        ScrollViewer.create [
            ScrollViewer.content (
                StackPanel.create [
                    StackPanel.children (List.mapi (fun i item -> trackTemplate item i state.Project.CommonTones dispatch :> IView) state.Project.Tracks)
                ] 
            )
        ]
        
        