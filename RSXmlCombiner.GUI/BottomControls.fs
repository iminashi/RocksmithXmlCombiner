namespace RSXmlCombiner.FuncUI

module BottomControls =
    open Elmish
    open System
    open System.IO
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout

    type Msg = 
    | SelectTargetAudioFile
    | SelectCombinationTargetFolder
    | CombineAudioFiles of targetFile : string
    | CombineArrangements of targetFolder :string
    | UpdateCombinationTitle of newTitle : string
    | CoercePhrasesChanged of bool
    | AddTrackNamesChanged of bool

    let update msg state : ProgramState * Cmd<_> =
        match msg with
        | SelectTargetAudioFile ->
            let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
            let targetFile = Dialogs.saveFileDialog "Select Target File" Dialogs.audioFileFiltersSave (Some "combo.wav") initialDir
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () CombineAudioFiles

        | CombineAudioFiles targetFile ->
            if String.IsNullOrEmpty targetFile then
                // User canceled the dialog
                state, Cmd.none
            else
                let message = AudioCombiner.combineAudioFiles state.Tracks targetFile
                { state with StatusMessage = message }, Cmd.none

        | CombineArrangements targetFolder ->
            if String.IsNullOrEmpty(targetFolder) then
                // User canceled the dialog
                state, Cmd.none
            else
                ArrangementCombiner.combine state targetFolder
                { state with StatusMessage = "Arrangements combined." }, Cmd.none

        | SelectCombinationTargetFolder ->
            let initialDir = state.OpenProjectFile |> Option.map Path.GetDirectoryName
            let targetFolder = Dialogs.openFolderDialog "Select Target Folder" initialDir
            state, Cmd.OfAsync.perform (fun _ -> targetFolder) () CombineArrangements

        | UpdateCombinationTitle newTitle -> { state with CombinationTitle = newTitle }, Cmd.none
        | CoercePhrasesChanged value -> { state with CoercePhrases = value }, Cmd.none
        | AddTrackNamesChanged value -> { state with AddTrackNamesToLyrics = value }, Cmd.none

    let view state dispatch =
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
                            Button.onClick (fun _ -> dispatch SelectTargetAudioFile)
                            // Only enable the button if there is more than one track and every track has an audio file
                            Button.isEnabled (state.Tracks.Length > 1 && state.Tracks |> List.forall hasAudioFile)
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
                                    ToolTip.tip "Combines phrases and sections so that the resulting arrangements have a max of 100 phrases and sections."
                                ]
                                // Add Track Names to Lyrics Checkbox
                                CheckBox.create [
                                    CheckBox.content "Add Track Names to Lyrics"
                                    CheckBox.isChecked state.AddTrackNamesToLyrics
                                    CheckBox.onChecked (fun _ -> AddTrackNamesChanged true |> dispatch)
                                    CheckBox.onUnchecked (fun _ -> AddTrackNamesChanged false |> dispatch)
                                    CheckBox.margin (0.0, 5.0, 0.0, 0.0) 
                                ]
                            ]
                        ]

                        // Combine Arrangements Button
                        Button.create [
                            Button.content "Combine Arrangements"
                            Button.onClick (fun _ -> dispatch SelectCombinationTargetFolder)
                            Button.fontSize 20.0
                            // TODO: More comprehensive validation when arrangements can be combined?
                            Button.isEnabled (state.Tracks.Length > 1)
                        ]
                    ]
                ]
            ]
        ]
