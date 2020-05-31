﻿namespace RSXmlCombiner.FuncUI

module TopControls =
    open Elmish
    open Avalonia.Layout
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Types
    open Avalonia.Input
    open System
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization
    open Rocksmith2014Xml
    open XmlUtils

    type State = CombinerProject

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
        | StatusMessage of string

    let private handleHotkeys dispatch (event : KeyEventArgs) =
        match event.KeyModifiers with
        | KeyModifiers.Control ->
            match event.Key with
            | Key.O -> dispatch SelectOpenProjectFile
            | Key.S -> dispatch SelectSaveProjectFile
            | Key.N -> dispatch NewProject
            | _ -> ()
        | _ -> ()

    let private createTrack instArrFile arrangements (title : string option) =
        let song = RS2014Song.Load(instArrFile)
        { Title = title |> Option.defaultValue song.Title
          AudioFile = None
          SongLength = song.SongLength
          TrimAmount = song.StartBeat
          Arrangements = arrangements |> List.sortBy (fun a -> a.ArrangementType) }

    let private addNewTrack state arrangementFileNames =
        let instArrFile = arrangementFileNames |> Array.tryFind (fun a -> XmlHelper.ValidateRootElement(a, "song"))
        match instArrFile with
        | None -> 
            state, Cmd.ofMsg (StatusMessage "Please select at least one instrumental Rocksmith arrangement.")

        | Some instArrFile ->
            let alreadyHasShowlights (arrs : Arrangement list) =
                arrs |> List.exists (fun a -> a.ArrangementType = ArrangementType.ShowLights)

            let arrangementFolder state fileName =
                match XmlHelper.GetRootElementName(fileName) with
                | "song" ->
                    (createInstrumental fileName None None) :: state
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
                |> CombinerProject.addMissingArrangements state.Templates

            let newTrack = createTrack instArrFile arrangements None

            CombinerProject.addTrack newTrack state, Cmd.none

    let private saveProject fileName project =
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonFSharpConverter())
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.WriteIndented <- true
        let json = JsonSerializer.Serialize(project, options)
        File.WriteAllText(fileName, json)

    let update (msg: Msg) state : State * Cmd<_> =
        match msg with
        | StatusMessage -> state, Cmd.none

        | SelectAddTrackFiles ->
            let selectFiles = Dialogs.openFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter true
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) () (fun files -> AddTrack files)

        | AddTrack fileNames -> 
            if fileNames.Length > 0 then
                addNewTrack state fileNames
            else
                state, Cmd.none
        
        | ImportToolkitTemplate files ->
            if files.Length > 0 then
                let fileName = files.[0]

                let foundArrangements, title = ToolkitImporter.import fileName

                // Try to find an instrumental arrangement to read metadata from
                let instArrType = foundArrangements |> Map.tryFindKey (fun arrType _ -> isInstrumental arrType)
                match instArrType with
                | Some instArrType ->
                    let instArrFile, _ = foundArrangements.[instArrType]
                    
                    let foldArrangements (state : Arrangement list) arrType (fileName, baseTone) =
                        let arrangement =
                            match arrType with
                            | t when isInstrumental t ->
                                // Respect the arrangement type from the Toolkit template
                                createInstrumental fileName baseTone (Some arrType)
                            | _ -> { FileName = Some fileName
                                     ArrangementType = arrType
                                     Name = arrTypeHumanized arrType
                                     Data = None }
                        arrangement :: state

                    let arrangements = 
                        foundArrangements
                        |> Map.fold foldArrangements []
                        // Add any missing arrangements from the project's templates
                        |> CombinerProject.addMissingArrangements state.Templates

                    let newTrack = createTrack instArrFile arrangements (Some title)

                    CombinerProject.addTrack newTrack state, Cmd.none
                | None ->
                    state, Cmd.none // TODO: Display a message
            else
                state, Cmd.none
        
        | NewProject -> emptyProject, Cmd.none

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
                    | Some head -> head.Arrangements |> List.map createTemplate |> Templates
                    | None -> Templates []

                { openedProject with Templates = templates }, Cmd.none
            else
                state, Cmd.none

        | SaveProject fileName ->
            if not (String.IsNullOrEmpty fileName) then
                state |> saveProject fileName
            state, Cmd.none

        | SelectToolkitTemplate ->
            let files = Dialogs.openFileDialog "Select Toolkit Template" Dialogs.toolkitTemplateFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun files -> ImportToolkitTemplate files)

        | SelectOpenProjectFile ->
            let files = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun files -> OpenProject files)

        | SelectSaveProjectFile ->
            let targetFile = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter (Some "combo.rscproj")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun file -> SaveProject file)

    let view state dispatch =
        // Top Panel
        let (Templates templates) = state.Templates
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

                ComboBox.create [
                    Grid.column 1
                    ComboBox.dataItems templates
                ]

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
