module RSXmlCombiner.FuncUI.TopControls

open Elmish
open Avalonia.Layout
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open System
open System.IO
open Rocksmith2014.XML
open XmlUtils
open ArrangementType

type Msg =
    | SelectAddTrackFiles
    | AddTrack of arrangementFiles : string[] option
    | SelectOpenProjectFile
    | OpenProject of projectFile : string option
    | SelectToolkitTemplate
    | ImportToolkitTemplate of templateFile : string option
    | NewProject
    | SaveProject of fileName : string option
    | SelectSaveProjectFile
    | AddTemplate of arrType : ArrangementType * ordering : ArrangementOrdering option

let private createTrack instArrFile (title : string option) (audioFile : string option) arrangements =
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
    let instArrFile = arrangementFileNames |> Array.tryFind (fun f -> XmlHelper.ValidateRootElement(f, "song"))
    match instArrFile with
    | None -> 
        { state with StatusMessage = "Please select at least one instrumental Rocksmith arrangement." }

    | Some instArrFile ->
        let alreadyHas arrType = List.exists (fun a -> a.ArrangementType = arrType)

        let arrangementFolder state fileName =
            match XmlHelper.GetRootElementName(fileName) with
            | "song" ->
                let arr = createInstrumental fileName None
                if state |> List.exists (fun a -> a.Name = arr.Name) then
                    state
                else
                    arr :: state
            | "vocals" when state |> alreadyHas ArrangementType.Vocals |> not ->
                { Name = "Vocals"; FileName = Some fileName; ArrangementType = ArrangementType.Vocals; Data = None  } :: state
            | "showlights" when state |> alreadyHas ArrangementType.ShowLights |> not ->
                { Name = "Show Lights"; FileName = Some fileName; ArrangementType = ArrangementType.ShowLights; Data = None } :: state
            | _ -> state

        arrangementFileNames
        |> Array.fold arrangementFolder []
        // Add any missing arrangements from the project's templates
        |> ProgramState.addMissingArrangements state.Templates
        |> createTrack instArrFile None None
        |> ProgramState.addTrack state

let update (msg: Msg) state : ProgramState * Cmd<_> =
    match msg with
    | SelectAddTrackFiles ->
        let dialog = Dialogs.openMultiFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter
        state, Cmd.OfAsync.perform dialog None AddTrack

    | AddTrack arrangementFiles -> 
        match arrangementFiles with
        | None -> state, Cmd.none
        | Some files -> addNewTrack state files, Cmd.none
    
    | ImportToolkitTemplate (Some templateFile) ->
        let foundArrangements, title, audioFilePath = ToolkitImporter.import templateFile

        let audioFile =
            let wav = Path.ChangeExtension(audioFilePath, "wav")
            let ogg = Path.ChangeExtension(audioFilePath, "ogg")
            let oggFixed = lazy (audioFilePath.Substring(0, audioFilePath.Length - 4) + "_fixed.ogg")
            Option.create File.Exists wav
            |> Option.orElse (Option.create File.Exists ogg)
            // Try to find the _fixed.ogg from an unpacked psarc file
            |> Option.orElse (Option.create File.Exists oggFixed.Value)

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
                    | Instrumental _ ->
                        // Respect the arrangement type from the Toolkit template
                        createInstrumental fileName (Some arrType)
                    | _ -> { FileName = Some fileName
                             ArrangementType = arrType
                             Name = humanize arrType
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
    
    | NewProject -> ProgramState.init, Cmd.none

    | OpenProject (Some projectFile) ->
        let result = Project.load projectFile
        match result with
        | Error message -> { state with StatusMessage = message }, Cmd.none
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

            Project.toProgramState templates projectFile statusMessage project, Cmd.none

    | SaveProject (Some fileName) ->
        state |> Project.save fileName
        { state with OpenProjectFile = Some fileName; StatusMessage = "Project saved." }, Cmd.none

    | SelectToolkitTemplate ->
        let dialog = Dialogs.openFileDialog "Select Toolkit Template" Dialogs.toolkitTemplateFilter
        state, Cmd.OfAsync.perform dialog None ImportToolkitTemplate

    | SelectOpenProjectFile ->
        let dialog = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter
        state, Cmd.OfAsync.perform dialog None OpenProject

    | SelectSaveProjectFile ->
        let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
        let initialFile = state.OpenProjectFile |> Option.map Path.GetFileName |> Option.orElse (Some "combo.rscproj")
        let dialog = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter initialFile
        state, Cmd.OfAsync.perform dialog initialDir SaveProject

    | AddTemplate (arrtype, ordering) ->
        let (Templates templates) = state.Templates
        let tempArr =
            let data =
                ordering
                |> Option.map (fun o -> { Ordering = o; BaseToneIndex = -1; ToneNames = []; ToneReplacements = Map.empty })
            { ArrangementType = arrtype
              Name = "" 
              FileName = None
              Data = data }

        let updatedTemplates = Templates ((createTemplate tempArr) :: templates)
        let updatedTracks = state.Tracks |> ProgramState.updateTracks updatedTemplates

        { state with Tracks = updatedTracks; Templates = updatedTemplates }, Cmd.none

    // User canceled the dialog
    | ImportToolkitTemplate None | OpenProject None | SaveProject None -> state, Cmd.none

/// Creates the menu items for adding arrangements.
let addArrangementMenuItems state dispatch : IView list =
    let (Templates templates) = state.Templates
    let notIncluded arrType = templates |> List.exists (fun t -> t.ArrangementType = arrType) |> not

    let createMenuItem arrType =
        MenuItem.create [
            MenuItem.header (humanize arrType)
            MenuItem.isEnabled (notIncluded arrType)
            MenuItem.onClick (fun _ -> AddTemplate(arrType, None) |> dispatch)
        ]

    [ createMenuItem ArrangementType.JVocals
      createMenuItem ArrangementType.ShowLights ]

let view state dispatch =
    // Top Panel
    Grid.create [
        DockPanel.dock Dock.Top
        Grid.margin (15.0, 0.0)
        Grid.classes [ "topcontrols" ]
        Grid.columnDefinitions "auto,*,auto"
        Grid.rowDefinitions "*,*"
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

            Menu.create [
                Grid.row 1
                Menu.isEnabled (not state.Tracks.IsEmpty)
                Menu.margin (15.0, 0.0, 0.0, 0.0)
                Menu.viewItems [
                    MenuItem.create [
                        MenuItem.header "Add Arrangement"
                        MenuItem.viewItems (addArrangementMenuItems state dispatch)
                    ]
                ]
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
                    ]
                    Button.create [
                        Button.content "Open Project..."
                        Button.onClick (fun _ -> dispatch SelectOpenProjectFile)
                    ]
                    Button.create [
                        Button.content "Save Project..."
                        Button.onClick (fun _ -> dispatch SelectSaveProjectFile)
                        Button.isEnabled (not state.Tracks.IsEmpty)
                    ]
                ]
            ]
        ]
    ]
