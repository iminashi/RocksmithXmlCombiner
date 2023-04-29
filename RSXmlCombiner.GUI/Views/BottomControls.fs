module RSXmlCombiner.FuncUI.BottomControls

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout

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
                                ToolTip.tip "Combines phrases and sections so that the resulting arrangements have a max of 100 phrases and sections. \
                                             \n\nWorks only when combining arrangements without DD levels."
                            ]
                            // One Phrase Per Track Checkbox
                            CheckBox.create [
                                CheckBox.content "One Phrase Per Track"
                                CheckBox.isChecked state.OnePhrasePerTrack
                                CheckBox.onChecked (fun _ -> OnePhrasePerTrackChanged true |> dispatch)
                                CheckBox.onUnchecked (fun _ -> OnePhrasePerTrackChanged false |> dispatch)
                                ToolTip.tip "Condenses each instrumental arrangement on a track into one phrase/section. \
                                            \nMay cause issues with the camera zoom in the game."
                                CheckBox.margin (0.0, 5.0, 0.0, 5.0)
                            ]
                            // Generate minimal DD levels
                            CheckBox.create [
                                CheckBox.content "Generate Dummy DD"
                                CheckBox.isChecked state.GenerateDummyDD
                                CheckBox.onChecked (fun _ -> GenerateDummyDDChanged true |> dispatch)
                                CheckBox.onUnchecked (fun _ -> GenerateDummyDDChanged false |> dispatch)
                                ToolTip.tip "Generates two difficulty levels for all phrases: one empty and one with all the notes. \
                                             \nWorks only when combining arrangements without DD levels."
                                CheckBox.margin (0.0, 0.0, 0.0, 5.0)
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
