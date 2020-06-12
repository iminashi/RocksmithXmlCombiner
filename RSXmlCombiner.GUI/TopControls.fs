﻿namespace RSXmlCombiner.FuncUI

module TopControls =
    open Elmish
    open Avalonia.Layout
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Input
    open System
    open System.IO
    open Rocksmith2014Xml
    open XmlUtils

    type Msg =
        | SelectAddTrackFiles
        | AddTrack of fileNames : string[]
        | SelectOpenProjectFile
        | OpenProject of fileNames : string[]
        | SelectToolkitTemplate
        | ImportToolkitTemplate of fileNames : string[]
        | NewProject
        | SaveProject of fileName : string
        | SelectSaveProjectFile

    let private handleHotkeys dispatch (event : KeyEventArgs) =
        match event.KeyModifiers with
        | KeyModifiers.Control ->
            match event.Key with
            | Key.O -> dispatch SelectOpenProjectFile
            | Key.S -> dispatch SelectSaveProjectFile
            | Key.N -> dispatch NewProject
            | _ -> ()
        | _ -> ()

    let private createTrack instArrFile (title : string option) (audioFile : string option) arrangements =
        let song = InstrumentalArrangement.Load(instArrFile)
        { Title = title |> Option.defaultValue song.Title
          AudioFile = audioFile
          SongLength = song.SongLength
          TrimAmount = song.StartBeat
          Arrangements = arrangements |> List.sortBy (fun a -> a.ArrangementType) }

    let private addNewTrack state arrangementFileNames =
        let instArrFile = arrangementFileNames |> Array.tryFind (fun a -> XmlHelper.ValidateRootElement(a, "song"))
        match instArrFile with
        | None -> 
            { state with StatusMessage = "Please select at least one instrumental Rocksmith arrangement." }, Cmd.none

        | Some instArrFile ->
            let alreadyHasShowlights (arrs : Arrangement list) =
                arrs |> List.exists (fun a -> a.ArrangementType = ArrangementType.ShowLights)

            let arrangementFolder state fileName =
                match XmlHelper.GetRootElementName(fileName) with
                | "song" ->
                    let arr = createInstrumental fileName None
                    if state |> List.exists (fun a -> a.Name = arr.Name) then
                        state
                    else
                        arr :: state
                | "vocals" ->
                    { Name = "Vocals"; FileName = Some fileName; ArrangementType = ArrangementType.Vocals; Data = None  } :: state
                | "showlights" when state |> alreadyHasShowlights |> not ->
                    { Name = "Show Lights"; FileName = Some fileName; ArrangementType = ArrangementType.ShowLights; Data = None } :: state
                //| "showlights" -> Cannot have more than one show lights arrangement
                | _ -> state // StatusMessage = "Unknown arrangement type for file {Path.GetFileName(arr)}";
        
            let newState = 
                arrangementFileNames
                |> Array.fold arrangementFolder []
                // Add any missing arrangements from the project's templates
                |> ProgramState.addMissingArrangements state.Templates
                |> createTrack instArrFile None None
                |> ProgramState.addTrack state

            newState, Cmd.none

    let update (msg: Msg) state : ProgramState * Cmd<_> =
        match msg with
        | SelectAddTrackFiles ->
            let selectFiles = Dialogs.openFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter true None
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) () AddTrack

        | AddTrack fileNames -> 
            if fileNames.Length > 0 then
                addNewTrack state fileNames
            else
                state, Cmd.none
        
        | ImportToolkitTemplate files ->
            if files.Length > 0 then
                let fileName = files.[0]
                let foundArrangements, title, audioFilePath = ToolkitImporter.import fileName

                let audioFile =
                    let wav = Path.ChangeExtension(audioFilePath, "wav")
                    //let ogg = Path.ChangeExtension(audioFilePath, "ogg")
                    if File.Exists wav then
                        Some wav
                    else 
                        None
                    //elif File.Exists ogg then
                    //    Some ogg
                    //else
                    //    // Try to find the _fixed.ogg from an unpacked psarc file
                    //    let oggFixed = audioFilePath.Substring(0, audioFilePath.Length - 4) + "_fixed.ogg"
                    //    if File.Exists oggFixed then
                    //        Some oggFixed
                    //    else
                    //        None

                // Try to find an instrumental arrangement to read metadata from
                let instArrType = foundArrangements |> Map.tryFindKey (fun arrType _ -> isInstrumental arrType)
                match instArrType with
                | None ->
                    { state with StatusMessage = "Could not find any instrumental arrangements in the template." }, Cmd.none
                | Some instArrType ->
                    let instArrFile = foundArrangements.[instArrType]
                    
                    let foldArrangements (state : Arrangement list) arrType fileName =
                        let arrangement =
                            match arrType with
                            | t when isInstrumental t ->
                                // Respect the arrangement type from the Toolkit template
                                createInstrumental fileName (Some arrType)
                            | _ -> { FileName = Some fileName
                                     ArrangementType = arrType
                                     Name = arrTypeHumanized arrType
                                     Data = None }
                        arrangement :: state

                    let newState = 
                        foundArrangements
                        |> Map.fold foldArrangements []
                        // Add any missing arrangements from the project's templates
                        |> ProgramState.addMissingArrangements state.Templates
                        |> createTrack instArrFile (Some title) audioFile
                        |> ProgramState.addTrack state

                    { newState with StatusMessage = sprintf "%i arrangements imported." foundArrangements.Count }, Cmd.none
            else
                state, Cmd.none
        
        | NewProject -> ProgramState.init, Cmd.none

        | OpenProject files ->
            if files.Length > 0 then
                let result = Project.load files.[0]
                match result with 
                | Ok project ->
                    // TODO: Check if the tone names in the files have been changed

                    // Create lists of missing audio and arrangement files
                    let missingAudioFiles, missingArrangementFiles =
                        (([], []), project.Tracks)
                        ||> List.fold (fun state track ->
                            let missingAudioFiles =
                                match track.AudioFile with
                                | Some file when not <| File.Exists(file) -> file :: (fst state)
                                | _ -> fst state
                            let missingArrangementFiles =
                                ([], track.Arrangements)
                                ||> List.fold (fun missing arr -> 
                                        match arr.FileName with
                                        | Some file when not <| File.Exists(file) -> file :: missing
                                        | _ -> missing)
                            missingAudioFiles, (snd state) @ missingArrangementFiles)

                    let statusMessage =
                        match missingAudioFiles, missingArrangementFiles with
                        | [], [] -> "Project loaded."
                        | _, _ -> "WARNING: Some of the files referenced in the project could not be found!"

                    // Generate the arrangement templates from the first track
                    let templates = 
                        match project.Tracks with
                        | head::_ -> head.Arrangements |> (List.map createTemplate >> Templates)
                        | [] -> Templates []

                    { Tracks = project.Tracks
                      CommonTones = project.CommonTones
                      CombinationTitle = project.CombinationTitle
                      AddTrackNamesToLyrics = project.AddTrackNamesToLyrics
                      CoercePhrases = project.CoercePhrases
                      Templates = templates
                      StatusMessage = statusMessage
                      ReplacementToneEditor = None
                      ProjectViewActive = true
                      OpenProjectFile = Some (files.[0]) }, Cmd.none
                | Error message ->
                    { state with StatusMessage = message }, Cmd.none
            else
                state, Cmd.none

        | SaveProject fileName ->
            if not (String.IsNullOrEmpty fileName) then
                state |> Project.save fileName
                { state with OpenProjectFile = Some fileName }, Cmd.none
            else
                state, Cmd.none

        | SelectToolkitTemplate ->
            let files = Dialogs.openFileDialog "Select Toolkit Template" Dialogs.toolkitTemplateFilter false None
            state, Cmd.OfAsync.perform (fun _ -> files) () ImportToolkitTemplate

        | SelectOpenProjectFile ->
            let files = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter false None
            state, Cmd.OfAsync.perform (fun _ -> files) () OpenProject

        | SelectSaveProjectFile ->
            let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
            let initialFile = state.OpenProjectFile |> Option.map Path.GetFileName |> Option.orElse (Some "combo.rscproj")
            let targetFile = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter initialFile initialDir
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () SaveProject

    let view state dispatch =
        // Top Panel
        Grid.create [
            DockPanel.dock Dock.Top
            Grid.margin (15.0, 0.0)
            Grid.classes [ "topcontrols" ]
            Grid.columnDefinitions "auto,*,auto"
            Grid.children [
                // Track Creation Buttons
                StackPanel.create [
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.spacing 15.0
                    StackPanel.children [
                        Button.create [
                            Button.content "Add Track..."
                            Button.onClick (fun _ -> dispatch SelectAddTrackFiles)
                        ]
                        Button.create [
                            Button.content "Import..."
                            Button.onClick (fun _ -> dispatch SelectToolkitTemplate)
                            ToolTip.tip "Imports a track from a Toolkit template file."
                         ]
                    ]
                ]

                //let (Templates templates) = state.Templates
                //ComboBox.create [
                //    Grid.column 1
                //    ComboBox.dataItems templates
                //]

                // Right Side Panel
                StackPanel.create [
                    Grid.column 2
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.spacing 15.0
                    StackPanel.children [
                        Button.create [
                            Button.content "New Project"
                            Button.onClick (fun _ -> dispatch NewProject)
                            //Button.hotKey <| KeyGesture.Parse "Ctrl+N" // TODO: Hook up hot keys
                        ]
                        Button.create [
                            Button.content "Open Project..."
                            Button.onClick (fun _ -> dispatch SelectOpenProjectFile)
                            //Button.hotKey <| KeyGesture.Parse "Ctrl+O"
                        ]
                        Button.create [
                            Button.content "Save Project..."
                            Button.onClick (fun _ -> dispatch SelectSaveProjectFile)
                            Button.isEnabled (state.Tracks.Length > 0)
                            //Button.hotKey <| KeyGesture.Parse "Ctrl+S"
                        ]
                    ]
                ]
            ]
        ]
