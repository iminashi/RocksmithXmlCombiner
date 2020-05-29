namespace RSXmlCombiner.FuncUI

module BottomControls =
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Input
    open Types
    open Avalonia.Layout
    open Avalonia.FuncUI.Types

    type State = { Tracks : Track list; CombinationTitle : string }

    type Msg = 
    | SelectTargetAudioFile
    | SelectCombinationTargetFolder
    | CombineAudioFiles of targetFile : string
    | CombineArrangements of targetFolder :string
    | UpdateCombinationTitle of newTitle : string
    | TracksUpdated of tracks : Track list

    let init = { Tracks = []; CombinationTitle = "" }

    let update msg state : State * Cmd<_> =
        match msg with
        | SelectTargetAudioFile -> 
            let targetFile = Dialogs.saveFileDialog "Select Target File" Dialogs.audioFileFilters (Some "combo.wav")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun f -> CombineAudioFiles f)

        | CombineAudioFiles -> state, Cmd.none
        | CombineArrangements -> state, Cmd.none

        | SelectCombinationTargetFolder ->
            let targetFolder = Dialogs.openFolderDialog "Select Target Folder"
            state, Cmd.OfAsync.perform (fun _ -> targetFolder) () (fun f -> CombineArrangements f)

        | UpdateCombinationTitle newTitle ->
            { state with CombinationTitle = newTitle }, Cmd.none

        | TracksUpdated tracks ->
            { state with Tracks = tracks }, Cmd.none

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
                    Grid.column 2
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.spacing 10.0
                    StackPanel.children [
                        // Combined Title Text Box
                        TextBox.create [
                            TextBox.watermark "Combined Title"
                            TextBox.text state.CombinationTitle
                            TextBox.onTextChanged (fun text -> dispatch (UpdateCombinationTitle text))
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
                                    //CheckBox.isChecked state.Project.CoercePhrases
                                    // TODO: Binding
                                    ToolTip.tip "Will combine phrases and sections so the resulting arrangements have a max of 100 phrases and sections."
                                ]
                                // Add Track Names to Lyrics Check Box
                                CheckBox.create [
                                    CheckBox.content "Add Track Names to Lyrics"
                                    //CheckBox.isChecked state.Project.AddTrackNamesToLyrics
                                    // TODO: Binding
                                    CheckBox.margin (0.0, 5.0, 0.0, 0.0) 
                                ]
                            ]
                        ]

                        // Combine Arrangements Button
                        Button.create [
                            Button.content "Combine Arrangements"
                            Button.onClick (fun _ -> dispatch SelectCombinationTargetFolder)
                            Button.fontSize 20.0
                            Button.isEnabled (state.Tracks.Length > 1)
                        ]
                    ]
                ]
            ]
        ]
