﻿module RSXmlCombiner.FuncUI.TrackList

open System
open System.IO
open Elmish
open Avalonia.Media
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Layout
open Avalonia.Input
open XmlUtils
open Media
open ArrangementType

type Msg =
    | RemoveTrack of trackIndex : int
    | ChangeAudioFile of trackIndex : int
    | ChangeAudioFileResult of trackIndex : int * newFile : string option
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * newFile : string option
    | ArrangementBaseToneChanged of trackIndex : int * arrIndex : int * toneIndex : int
    | RemoveArrangementFile of trackIndex : int * arrIndex : int
    | ShowReplacementToneEditor of trackIndex : int * arrIndex : int
    | TrimAmountChanged of trackIndex : int * trimAmunt : double
    | RemoveTemplate of name : string

let private changeAudioFile track newFile =
    let length = Audio.getLength newFile
    { track with AudioFile = Some newFile; SongLength = length }

let private getInitialDir (fileName : string option) state trackIndex =
    fileName
    // If no file is set, use the directory of the first arrangement that has a file
    |> Option.orElse (state.Tracks.[trackIndex].Arrangements |> List.tryPick (fun a -> a.FileName))
    |> Option.map Path.GetDirectoryName

let private getArr trackIndex arrIndex state = state.Tracks.[trackIndex].Arrangements.[arrIndex]

/// Updates the model according to the message content.
let update (msg: Msg) (state: ProgramState) =
    match msg with
    | RemoveTrack trackIndex ->
        { state with Tracks = state.Tracks |> List.except (seq { state.Tracks.[trackIndex] }) }, Cmd.none

    | ChangeAudioFile trackIndex ->
        let initialDir = getInitialDir state.Tracks.[trackIndex].AudioFile state trackIndex
        let dialog = Dialogs.openFileDialog "Select Audio File" Dialogs.audioFileFiltersOpen
        state, Cmd.OfAsync.perform dialog initialDir (fun file -> ChangeAudioFileResult (trackIndex, file))

    | ChangeAudioFileResult (trackIndex, file) ->
        match file with
        | None -> state, Cmd.none
        | Some fileName ->
            let oldSongLength = state.Tracks.[trackIndex].SongLength
            let updatedTracks =
                state.Tracks
                |> List.mapi (fun i t -> if i = trackIndex then changeAudioFile t fileName else t)
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
        | None -> state, Cmd.none
        | Some fileName ->
            let rootName = XmlHelper.GetRootElementName(fileName)
            let arrangement = state |> getArr trackIndex arrIndex

            let newArr =
                match rootName, arrangement.ArrangementType with
                // For instrumental arrangements, create an arrangement from the file, preserving the arrangement type and name
                | "song", Instrumental t ->
                    Ok { createInstrumental fileName (Some t) with Name = arrangement.Name }

                // For vocals and show lights, just change the file name
                | "vocals", Vocals _
                | "showlights", ArrangementType.ShowLights -> Ok { arrangement with FileName = Some fileName }

                | _ -> Error "Incorrect arrangement type."

            match newArr with
            | Ok arr ->
                let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex arr
                { state with Tracks = updatedTracks }, Cmd.none
            | Error message->
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
        let newTracks = state.Tracks |> List.mapi (fun i t -> if i = trackIndex then { t with TrimAmount = trim } else t)
        { state with Tracks = newTracks }, Cmd.none

    | RemoveTemplate name ->
        let (Templates templates) = state.Templates
        let updatedTemplates = templates |> List.filter (fun t -> t.Name <> name) |> Templates

        // Remove the arrangement from all the tracks
        let updatedTracks =
            state.Tracks
            |> List.map (fun t ->
                let arrs = t.Arrangements |> List.filter (fun a -> a.Name <> name)
                { t with Arrangements = arrs })

        let updatedCommonTones = state.CommonTones |> Map.remove name
        let updatedSelectedTones = state.SelectedFileTones |> Map.remove name

        { state with
            Tracks = updatedTracks
            Templates = updatedTemplates
            CommonTones = updatedCommonTones
            SelectedFileTones = updatedSelectedTones }, Cmd.none

/// Creates the view for an arrangement.
let private arrangementView (arr : Arrangement) trackIndex arrIndex state dispatch =
    let fileName = arr.FileName
    let fileNameBrush =
        match fileName with
        | Some fn when not <| File.Exists(fn) -> Brushes.Red
        | _ -> Brushes.DarkGray

    let color =
        match fileName with
        | Some ->
            match arr.ArrangementType with
            | ArrangementType.Lead -> Brushes.lead
            | ArrangementType.Rhythm | ArrangementType.Combo -> Brushes.rhythm
            | ArrangementType.Bass -> Brushes.bass
            | ArrangementType.Vocals | ArrangementType.JVocals -> Brushes.Yellow
            | ArrangementType.ShowLights -> Brushes.Violet
            | _ -> Brushes.GhostWhite
        | None -> Brushes.Gray

    Border.create [
        Border.borderThickness 1.0
        Border.borderBrush color
        Border.minWidth 140.0
        Border.classes [ "arrangement" ]
        Border.contextMenu (
            ContextMenu.create [
                ContextMenu.viewItems [
                    MenuItem.create [
                        MenuItem.header "Remove File"
                        MenuItem.isEnabled (fileName |> Option.isSome)
                        MenuItem.onClick (fun _ -> RemoveArrangementFile(trackIndex, arrIndex) |> dispatch)
                    ]
                    MenuItem.create [
                        MenuItem.header "Remove from All Tracks"
                        MenuItem.onClick ((fun _ -> RemoveTemplate(arr.Name) |> dispatch), SubPatchOptions.OnChangeOf(arr.Name))
                    ]
                ]
            ]
        )

        Border.child (
            StackPanel.create [
                StackPanel.verticalAlignment VerticalAlignment.Top
                StackPanel.classes [ "arrangement" ]
                StackPanel.children [
                    // Header
                    yield Grid.create [
                        Grid.columnDefinitions "auto,auto"
                        Grid.children [
                            // Arrangement Icon
                            Path.create [
                                Path.fill color
                                Path.data (
                                    match arr.ArrangementType with
                                    | Instrumental _ -> Icons.pick
                                    | Vocals _ -> Icons.microphone
                                    | _ -> Icons.spotlight)
                            ]
                            // Arrangement Name
                            TextBlock.create [
                                Grid.column 1
                                TextBlock.margin (4.0, 0.0, 0.0, 0.0 )
                                TextBlock.classes [ "h2"]
                                TextBlock.text arr.Name
                                TextBlock.foreground color
                                TextBlock.cursor Cursors.hand
                                TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                            ]
                        ]
                    ]
                    // File Name
                    yield TextBlock.create [
                        yield TextBlock.text (fileName |> Option.map Path.GetFileNameWithoutExtension |> Option.defaultValue "No file")
                        yield TextBlock.width 100.0
                        yield TextBlock.foreground fileNameBrush
                        yield TextBlock.cursor Cursors.hand
                        yield TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                        yield ToolTip.tip (fileName |> Option.defaultValue "Click to select a file.")
                    ]

                    // Optional Tone Controls
                    match arr.Data with
                    | None -> () // Do nothing
                    | Some instArr ->
                        let baseToneNames = ProgramState.getReplacementToneNames arr.Name state.CommonTones

                        // The selection on the combo box is lost if the tone name at that index is edited in the common tone editor
                        // As a workaround, yield the combo box only when the project view is active
                        if instArr.ToneNames.Length = 0 && trackIndex <> 0 && state.ProjectViewActive then
                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.margin (0.0, 5.0) 
                                ComboBox.dataItems baseToneNames
                                ComboBox.selectedIndex instArr.BaseToneIndex
                                ComboBox.onSelectedIndexChanged (fun toneIndex -> if toneIndex <> -1 then ArrangementBaseToneChanged(trackIndex, arrIndex, toneIndex) |> dispatch)
                                ToolTip.tip "Base Tone"
                            ]
                        elif instArr.ToneNames.Length > 0 then
                            // Edit Replacement Tones Button
                            yield Button.create [
                                Button.content "Tones"
                                Button.width 100.0
                                Button.margin (0.0, 5.0)
                                Button.onClick (fun _ -> ShowReplacementToneEditor(trackIndex, arrIndex) |> dispatch)
                                Button.borderThickness 0.0
                                Button.background (
                                    if instArr.ToneReplacements.IsEmpty 
                                       || instArr.ToneReplacements |> Map.exists (fun _ ti -> ti = -1 || ti >= baseToneNames.Length) then
                                        Brushes.DarkRed
                                    else
                                        Brushes.DarkGreen
                                )
                            ]
                ]
            ]
        )
    ]
  
/// Creates the view for a track.
let private trackView (track : Track) index state dispatch =
    let audioFileBrush =
        match track.AudioFile with
        | Some fn when not <| File.Exists(fn) -> Brushes.Red
        | _ -> Brushes.DarkGray

    Border.create [
        Border.classes [ "track" ]
        Border.child (
            DockPanel.create [
                DockPanel.margin (5.0, 0.0, 0.0, 0.0)
                DockPanel.children [
                    // Track Number and Title
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
                            // Delete Track Button
                            ContentControl.create [
                                ContentControl.classes [ "close" ]
                                ContentControl.margin (2.0, 0.0, 5.0, 0.0)
                                ContentControl.renderTransform <| ScaleTransform(1.5, 1.5)
                                ContentControl.cursor Cursors.hand
                                ContentControl.onTapped (fun _ -> RemoveTrack index |> dispatch)
                                ContentControl.content (
                                    Path.create [
                                        Path.data Icons.close
                                        Path.classes [ "close" ]
                                    ]
                                )
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
                                        TextBlock.foreground audioFileBrush
                                        TextBlock.maxWidth 100.0
                                        TextBlock.cursor Cursors.hand
                                        TextBlock.onTapped (fun _ -> ChangeAudioFile index |> dispatch)
                                        TextBlock.text (track.AudioFile |> Option.map Path.GetFileName |> Option.defaultValue "None selected" )
                                        ToolTip.tip (track.AudioFile |> Option.defaultValue "Click to select a file.")
                                    ]

                                    // Trim Part
                                    StackPanel.create [
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
                                                    NumericUpDown.value <| (double track.TrimAmount) / 1000.0
                                                    NumericUpDown.minimum 0.0
                                                    NumericUpDown.verticalAlignment VerticalAlignment.Center
                                                    NumericUpDown.width 75.0
                                                    NumericUpDown.formatString "F3"
                                                    NumericUpDown.onValueChanged (fun trim -> TrimAmountChanged(index, trim) |> dispatch)
                                                    ToolTip.tip "Sets the amount of time in seconds to be trimmed from the start of the audio and each arrangement."
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
                                StackPanel.children <| List.mapi (fun i item -> arrangementView item index i state dispatch :> IView) track.Arrangements
                            ]
                        ]
                    ]
                ]
            ]
        )
    ]

/// Creates the track list view.
let view state dispatch =
    // List of tracks
    ScrollViewer.create [
        ScrollViewer.horizontalScrollBarVisibility Primitives.ScrollBarVisibility.Auto
        ScrollViewer.content (
            StackPanel.create [
                StackPanel.children <| List.mapi (fun i item -> trackView item i state dispatch :> IView) state.Tracks
            ] 
        )
    ]
