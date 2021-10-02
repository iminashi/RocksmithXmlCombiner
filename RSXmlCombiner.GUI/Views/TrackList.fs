module RSXmlCombiner.FuncUI.TrackList

open System.IO
open Avalonia.Media
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Media
open ArrangementType

/// Creates the view for an arrangement.
let private arrangementView state dispatch trackIndex arrIndex (arr: Arrangement) =
    let fileName = arr.FileName

    let fileNameBrush =
        match fileName with
        | Some fn when not <| File.Exists(fn) ->
            Brushes.Red
        | _ ->
            Brushes.DarkGray

    let color =
        match fileName with
        | Some _ ->
            match arr.ArrangementType with
            | ArrangementType.Lead ->
                Brushes.lead
            | ArrangementType.Rhythm | ArrangementType.Combo ->
                Brushes.rhythm
            | ArrangementType.Bass ->
                Brushes.bass
            | ArrangementType.Vocals | ArrangementType.JVocals ->
                Brushes.Yellow
            | ArrangementType.ShowLights ->
                Brushes.Violet
            | _ ->
                Brushes.GhostWhite
        | None ->
            Brushes.Gray

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
                        MenuItem.isEnabled fileName.IsSome
                        MenuItem.onClick (fun _ -> RemoveArrangementFile(trackIndex, arrIndex) |> dispatch)
                    ]
                    MenuItem.create [
                        MenuItem.header "Remove from All Tracks"
                        MenuItem.onClick ((fun _ ->
                            RemoveTemplate(arr.Name) |> dispatch),
                            SubPatchOptions.OnChangeOf arr.Name)
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
                                    | Instrumental _ ->
                                        Icons.pick
                                    | Vocals _ ->
                                        Icons.microphone
                                    | _ ->
                                        Icons.spotlight)
                            ]
                            // Arrangement Name
                            TextBlock.create [
                                Grid.column 1
                                TextBlock.margin (4.0, 0.0, 0.0, 0.0 )
                                TextBlock.classes [ "h2" ]
                                TextBlock.text arr.Name
                                TextBlock.foreground color
                                TextBlock.cursor Cursors.hand
                                TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                            ]
                        ]
                    ]
                    // File Name
                    yield TextBlock.create [
                        TextBlock.text (
                            fileName
                            |> Option.map Path.GetFileNameWithoutExtension
                            |> Option.defaultValue "No file")
                        TextBlock.width 100.0
                        TextBlock.foreground fileNameBrush
                        TextBlock.cursor Cursors.hand
                        TextBlock.onTapped (fun _ -> SelectArrangementFile(trackIndex, arrIndex) |> dispatch)
                        ToolTip.tip (fileName |> Option.defaultValue "Click to select a file.")
                    ]

                    // Optional Tone Controls
                    match arr.Data with
                    | None ->
                        // Do nothing
                        ()
                    | Some instArr ->
                        let baseToneNames = ProgramState.getReplacementToneNames arr.Name state.CommonTones

                        // The selection on the combobox is lost if the tone name at that index is edited in the common tone editor
                        // As a workaround, yield the combobox only when the project view is active
                        if instArr.ToneNames.Length = 0 && trackIndex <> 0 && state.ProjectViewActive then
                            // Base Tone Combo Box
                            yield ComboBox.create [
                                ComboBox.width 100.0
                                ComboBox.height 30.0
                                ComboBox.margin (0.0, 5.0) 
                                ComboBox.dataItems baseToneNames
                                ComboBox.selectedIndex instArr.BaseToneIndex
                                ComboBox.onSelectedIndexChanged (fun toneIndex ->
                                    if toneIndex <> -1 then
                                        ArrangementBaseToneChanged(trackIndex, arrIndex, toneIndex)
                                        |> dispatch)
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
                                       || instArr.ToneReplacements |> Map.exists (fun _ ti -> ti = -1 || ti >= baseToneNames.Length)
                                    then
                                        Brushes.DarkRed
                                    else
                                        Brushes.DarkGreen
                                )
                            ]
                ]
            ]
        )
    ] |> generalize

let private trackContents state dispatch trackIndex track =
    let audioFileBrush =
        match track.AudioFile with
        | Some fn when not <| File.Exists(fn) ->
            Brushes.Red
        | _ ->
            Brushes.DarkGray

    StackPanel.create [
        StackPanel.orientation Orientation.Horizontal
        StackPanel.children [
            // Delete Track Button
            ContentControl.create [
                ContentControl.classes [ "close" ]
                ContentControl.margin (2.0, 0.0, 5.0, 0.0)
                ContentControl.renderTransform <| ScaleTransform(1.5, 1.5)
                ContentControl.cursor Cursors.hand
                ContentControl.onTapped (fun _ -> RemoveTrack trackIndex |> dispatch)
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
                    // Audio Filename
                    TextBlock.create [
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text "Audio"
                        TextBlock.classes [ "h2" ]
                    ]
                    TextBlock.create [
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.foreground audioFileBrush
                        TextBlock.maxWidth 100.0
                        TextBlock.cursor Cursors.hand
                        TextBlock.onTapped (fun _ -> ChangeAudioFile trackIndex |> dispatch)
                        TextBlock.text (
                            track.AudioFile
                            |> Option.map Path.GetFileName
                            |> Option.defaultValue "None selected")
                        ToolTip.tip (
                            track.AudioFile
                            |> Option.defaultValue "Click to select a file.")
                    ]

                    // Trim Part
                    StackPanel.create [
                        StackPanel.classes [ "part" ]
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                        StackPanel.children [
                            // Hide if this is the first track
                            if trackIndex <> 0 then
                                TextBlock.create [ 
                                    TextBlock.classes [ "h2" ]
                                    TextBlock.text "Trim:"
                                    TextBlock.verticalAlignment VerticalAlignment.Center
                                ]
                                NumericUpDown.create [
                                    NumericUpDown.value <| (double track.TrimAmount) / 1000.0
                                    NumericUpDown.minimum 0.0
                                    NumericUpDown.verticalAlignment VerticalAlignment.Center
                                    NumericUpDown.width 75.0
                                    NumericUpDown.formatString "F3"
                                    NumericUpDown.onValueChanged (fun trim -> TrimAmountChanged(trackIndex, trim) |> dispatch)
                                    ToolTip.tip "Sets the amount of time in seconds to be trimmed from the start of the audio and each arrangement."
                                ]
                                TextBlock.create [
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
                StackPanel.children <| List.mapi (arrangementView state dispatch trackIndex) track.Arrangements
            ]
        ]
    ]

/// Creates the view for a track.
let private trackView state dispatch trackIndex (track: Track) =
    Border.create [
        Border.classes [ "track" ]
        Border.child (
            DockPanel.create [
                DockPanel.margin (5.0, 0.0, 0.0, 0.0)
                DockPanel.children [
                    // Track Number and Title
                    TextBlock.create [
                        DockPanel.dock Dock.Top
                        TextBlock.text (sprintf "%i. %s" (trackIndex + 1) track.Title)
                        TextBlock.classes [ "h1" ]
                    ]

                    trackContents state dispatch trackIndex track
                ]
            ]
        )
    ] |> generalize

/// Creates the track list view.
let view state dispatch =
    // List of tracks
    ScrollViewer.create [
        ScrollViewer.horizontalScrollBarVisibility Primitives.ScrollBarVisibility.Auto
        ScrollViewer.content (
            StackPanel.create [
                StackPanel.children <| List.mapi (trackView state dispatch) state.Tracks
            ]
        )
    ]
