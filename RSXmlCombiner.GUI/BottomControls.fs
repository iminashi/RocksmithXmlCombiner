module RSXmlCombiner.FuncUI.BottomControls

open Elmish
open System
open System.IO
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout

type Msg = 
    | SelectCombinationTargetFolder
    | CombineAudioFiles of targetFile : string option
    | CombineArrangements of targetFolder :string option
    | UpdateCombinationTitle of newTitle : string
    | CoercePhrasesChanged of bool
    | OnePhrasePerTrackChanged of bool
    | AddTrackNamesChanged of bool
    | CombineAudioCompleted of message : string
    | CombineArrangementsCompleted of unit
    | CreatePreview of targetFile : string option
    | SelectTargetAudioFile of defaultFileName : string option * cmd : (string option -> Msg)

let update msg state : ProgramState * Cmd<_> =
    match msg with
    | SelectTargetAudioFile (defaultFileName, cmd) ->
        let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
        let dialog = Dialogs.saveFileDialog "Select Target File" Dialogs.audioFileFiltersSave defaultFileName
        state, Cmd.OfAsync.perform dialog initialDir cmd

    | CombineAudioFiles targetFile ->
        match targetFile with
        | None -> state, Cmd.none // User canceled the dialog
        | Some file ->
            let task = AudioCombiner.combineWithResampling state.Tracks
            { state with AudioCombinerProgress = Some(0.0); StatusMessage = "Combining audio files..." }, Cmd.OfAsync.perform task file CombineAudioCompleted
    
    | CreatePreview targetFile ->
        match targetFile with
        | None -> state, Cmd.none // User canceled the dialog
        | Some file ->
            let task file = 
                async {
                    try
                        AudioCombiner.createPreview state.Tracks file
                        return "Preview created."
                    with
                    e -> return sprintf "Error: %s" e.Message
                }

            { state with AudioCombinerProgress = Some(0.0); StatusMessage = "Creating preview audio..." }, Cmd.OfAsync.perform task file CombineAudioCompleted

    | CombineAudioCompleted message ->
        { state with StatusMessage = message; AudioCombinerProgress = None }, Cmd.none

    | CombineArrangements targetFolder ->
        match targetFolder with
        | None -> state, Cmd.none // User canceled the dialog
        | Some folder ->
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
                    | ArrangementType.Instrumental _ ->
                        if state.Tracks |> List.forall hasFile then next, count + trackCount else next, count
                    | ArrangementType.ShowLights ->
                        if state.Tracks |> List.forall hasFile then next, count + 1 else next, count
                    | _ ->
                        if state.Tracks |> List.exists hasFile then next, count + 1 else next, count
                    )
                |> snd

            let task = ArrangementCombiner.combine state
            { state with ArrangementCombinerProgress = Some(0, max) }, Cmd.OfAsync.perform task folder CombineArrangementsCompleted

    | CombineArrangementsCompleted ->
        { state with StatusMessage = "Arrangements combined."; ArrangementCombinerProgress = None }, Cmd.none

    | SelectCombinationTargetFolder ->
        let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
        let dialog = Dialogs.openFolderDialog "Select Target Folder"
        state, Cmd.OfAsync.perform dialog initialDir CombineArrangements

    | UpdateCombinationTitle newTitle -> { state with CombinationTitle = newTitle }, Cmd.none
    | CoercePhrasesChanged value -> { state with CoercePhrases = value }, Cmd.none
    | OnePhrasePerTrackChanged value -> { state with OnePhrasePerTrack = value }, Cmd.none
    | AddTrackNamesChanged value -> { state with AddTrackNamesToLyrics = value }, Cmd.none

let view state dispatch =
    // Only enable the button if there is more than one track and every track has an audio file
    let canCombineAudio =
        state.AudioCombinerProgress |> Option.isNone
        && state.Tracks.Length > 1
        && state.Tracks |> List.forall hasAudioFile

    // TODO: More comprehensive validation when arrangements can be combined?
    let canCombineArrangements =
        state.ArrangementCombinerProgress |> Option.isNone
        && state.Tracks.Length > 1

    // Bottom Panel
    Grid.create [
        DockPanel.dock Dock.Bottom
        Grid.margin (15.0, 5.0)
        Grid.columnDefinitions "auto,*,auto"
        Grid.children [
            // Left Side Panel
            StackPanel.create [
                StackPanel.children [
                    // Combine Audio Files Button
                    Button.create [
                        Button.content "Combine Audio"
                        Button.fontSize 20.0
                        Button.onClick (fun _ -> SelectTargetAudioFile (Some "combo.wav", CombineAudioFiles) |> dispatch)
                        Button.isEnabled canCombineAudio
                    ]
                    // Create Preview Button
                    Button.create [
                        Button.content "Create Preview"
                        Button.fontSize 12.0
                        Button.margin (0.0, 1.0)
                        Button.onClick (fun _ -> SelectTargetAudioFile (Some "combo_preview.wav", CreatePreview) |> dispatch)
                        Button.isEnabled canCombineAudio
                        ToolTip.tip "Creates a preview audio file from randomly selected sections from randomly selected files (up to 4)."
                    ]
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
                        TextBox.text state.CombinationTitle
                        TextBox.verticalAlignment VerticalAlignment.Center
                        TextBox.onTextChanged (UpdateCombinationTitle >> dispatch)
                        TextBox.width 200.0
                        ToolTip.tip "Combined Title"
                    ]

                    // Options Panel
                    StackPanel.create [
                        StackPanel.verticalAlignment VerticalAlignment.Center
                        StackPanel.children [
                            // Coerce Phrases Checkbox
                            CheckBox.create [
                                CheckBox.content "Coerce to 100 Phrases"
                                CheckBox.isChecked state.CoercePhrases
                                CheckBox.onChecked (fun _ -> CoercePhrasesChanged true |> dispatch)
                                CheckBox.onUnchecked (fun _ -> CoercePhrasesChanged false |> dispatch)
                                ToolTip.tip "Combines phrases and sections so that the resulting arrangements have a max of 100 phrases and sections.\n\nWorks only when combining arrangements without DD levels."
                            ]
                            // One Phrase Per Track Checkbox
                            CheckBox.create [
                                CheckBox.content "One Phrase Per Track"
                                CheckBox.isChecked state.OnePhrasePerTrack
                                CheckBox.onChecked (fun _ -> OnePhrasePerTrackChanged true |> dispatch)
                                CheckBox.onUnchecked (fun _ -> OnePhrasePerTrackChanged false |> dispatch)
                                CheckBox.margin (0.0, 5.0, 0.0, 5.0)
                                ToolTip.tip "Condenses each instrumental arrangement on a track into one phrase/section."
                            ]
                            // Add Track Names to Lyrics Checkbox
                            CheckBox.create [
                                CheckBox.content "Add Track Names to Lyrics"
                                CheckBox.isChecked state.AddTrackNamesToLyrics
                                CheckBox.onChecked (fun _ -> AddTrackNamesChanged true |> dispatch)
                                CheckBox.onUnchecked (fun _ -> AddTrackNamesChanged false |> dispatch)
                            ]
                        ]
                    ]

                    // Combine Arrangements Button
                    Button.create [
                        Button.content "Combine Arrangements"
                        Button.onClick (fun _ -> dispatch SelectCombinationTargetFolder)
                        Button.fontSize 20.0
                        Button.isEnabled canCombineArrangements
                    ]
                ]
            ]
        ]
    ]
