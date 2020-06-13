namespace RSXmlCombiner.FuncUI

open Avalonia.Layout
open Avalonia.FuncUI.Types

module Shell =
    open System
    open Elmish
    open Avalonia
    open Avalonia.Controls
    open Avalonia.Input
    open Avalonia.Media
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI

    type Msg =
        | TrackListMsg of TrackList.Msg
        | CommonTonesMsg of CommonToneEditor.Msg
        | TopControlsMsg of TopControls.Msg
        | BottomControlsMsg of BottomControls.Msg
        | ToneReplacementClosed
        | SetReplacementTone of trackIndex : int * arrIndex : int * toneName : string * replacementIndex : int
        | ProjectViewActiveChanged of bool
        | CombineAudioProgressChanged of float

    let init () = ProgramState.init, Cmd.none

    let update shellMsg state : ProgramState * Cmd<_> =
        match shellMsg with
        | TrackListMsg msg ->
            let trackListState, cmd = TrackList.update msg state
            trackListState, Cmd.map TrackListMsg cmd

        | CommonTonesMsg msg ->
            let tonesState, cmd = CommonToneEditor.update msg state
            tonesState, Cmd.map CommonTonesMsg cmd

        | TopControlsMsg msg ->
            let topCtrlState, cmd = TopControls.update msg state
            topCtrlState, Cmd.map TopControlsMsg cmd

        | BottomControlsMsg msg ->
            let bcState, cmd = BottomControls.update msg state
            bcState, Cmd.map BottomControlsMsg cmd

        | ToneReplacementClosed -> { state with ReplacementToneEditor = None }, Cmd.none

        | SetReplacementTone (trackIndex, arrIndex, toneName, replacementIndex) ->
            let arr = state.Tracks.[trackIndex].Arrangements.[arrIndex]
            let data = arr.Data |> Option.get
            let updatedReplacements = data.ToneReplacements |> Map.add toneName replacementIndex
            let updatedData = { data with ToneReplacements = updatedReplacements }
            let updatedArr = { arr with Data = Some updatedData }
            let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex updatedArr

            { state with Tracks = updatedTracks }, Cmd.none

        | ProjectViewActiveChanged active -> { state with ProjectViewActive = active }, Cmd.none

        | CombineAudioProgressChanged progress -> { state with AudioCombinerProgress = Some(progress) }, Cmd.none

    let private replacementToneView state trackIndex arrIndex dispatch =
        let arrangement = state.Tracks.[trackIndex].Arrangements.[arrIndex]
        let data = arrangement.Data |> Option.get
        let replacementToneNames = ProgramState.getReplacementToneNames arrangement.Name state.CommonTones

        DockPanel.create [
            DockPanel.background "#77000000"
            DockPanel.children [
                Border.create [
                    Border.padding 20.0
                    Border.cornerRadius 5.0
                    Border.horizontalAlignment HorizontalAlignment.Center
                    Border.verticalAlignment VerticalAlignment.Center
                    Border.background "#444444"
                    Border.child (
                        Grid.create [
                            Grid.columnDefinitions "150, 150"
                            Grid.rowDefinitions (Seq.replicate (data.ToneNames.Length + 3) "*" |> String.concat ",")
                            Grid.children [
                                yield TextBlock.create [
                                    Grid.columnSpan 2
                                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                                    TextBlock.fontSize 18.0
                                    TextBlock.text (sprintf "%s - %s" state.Tracks.[trackIndex].Title arrangement.Name)
                                ]
                                yield TextBlock.create [
                                    Grid.row 1
                                    TextBlock.text "Tone Name"    
                                    TextBlock.fontSize 16.0
                                ]
                                yield TextBlock.create [
                                    Grid.row 1
                                    Grid.column 1
                                    TextBlock.text "Replace With"    
                                    TextBlock.fontSize 16.0
                                ]
                                for (i, tone) in data.ToneNames |> List.indexed do
                                    yield TextBlock.create [
                                            Grid.row (i + 2)
                                            TextBlock.margin 2.0
                                            TextBlock.text tone
                                            TextBlock.verticalAlignment VerticalAlignment.Center
                                          ]
                                    yield ComboBox.create [
                                            Grid.row (i + 2)
                                            Grid.column 1
                                            ComboBox.margin 2.0
                                            ComboBox.height 30.0
                                            ComboBox.dataItems replacementToneNames
                                            ComboBox.selectedIndex (
                                                match data.ToneReplacements |> Map.tryFind tone with
                                                | Some index -> index
                                                | None -> -1)
                                            ComboBox.onSelectedIndexChanged (fun item -> SetReplacementTone(trackIndex, arrIndex, tone, item) |> dispatch)
                                          ]
                                yield Button.create [
                                    Grid.row (data.ToneNames.Length + 2)
                                    Grid.columnSpan 2
                                    Button.content "OK"
                                    Button.isDefault true
                                    Button.fontSize 15.0
                                    Button.horizontalAlignment HorizontalAlignment.Center
                                    Button.width 120.0
                                    Button.margin 5.0
                                    Button.onClick (fun _ -> ToneReplacementClosed |> dispatch)
                                ]
                            ]
                        ]
                    )
                ]
            ]
        ]

    let view state dispatch =
        TabControl.create [
            TabControl.tabStripPlacement Dock.Top
            TabControl.viewItems [
                TabItem.create [
                    TabItem.header "Tracks"
                    TabItem.onIsSelectedChanged (fun selected -> ProjectViewActiveChanged(selected) |> dispatch)
                    TabItem.content (
                        Grid.create [
                            Grid.children [
                                yield DockPanel.create [
                                    DockPanel.children [
                                        TopControls.view state (TopControlsMsg >> dispatch)

                                        // Status Bar with Message
                                        Border.create [
                                            Border.classes [ "statusbar" ]
                                            Border.dock Dock.Bottom
                                            Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                                        ]

                                        ProgressBar.create [
                                              Border.dock Dock.Bottom
                                              ProgressBar.background "#181818"
                                              ProgressBar.height 2.0
                                              ProgressBar.minHeight 2.0
                                              ProgressBar.value (state.AudioCombinerProgress |> Option.defaultValue 0.0)
                                              ProgressBar.maximum 1.0
                                          ]

                                        BottomControls.view state (BottomControlsMsg >> dispatch)

                                        TrackList.view state (TrackListMsg >> dispatch)
                                    ]
                                ]
                                match state.ReplacementToneEditor with
                                | Some (trackIndex, arrIndex) ->
                                    yield replacementToneView state trackIndex arrIndex dispatch :> IView
                                | None -> ()
                            ]
                        ]
                    )
                ]
                TabItem.create [
                    TabItem.header "Common Tones"
                    TabItem.content (CommonToneEditor.view state (CommonTonesMsg >> dispatch))
                ]
            ]
        ] :> IView
