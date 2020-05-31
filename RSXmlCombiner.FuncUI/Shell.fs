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
        | SetReplacementTone of trackIndex : int * arrIndex : int * toneName : string * replacementName : string

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

        | SetReplacementTone (trackIndex, arrIndex, toneName, replacementName) ->
            let arr = state.Tracks.[trackIndex].Arrangements.[arrIndex]
            let data = arr.Data |> Option.get
            let updatedReplacements = data.ToneReplacements |> Map.add toneName replacementName
            let updatedData = { data with ToneReplacements = updatedReplacements }
            let updatedArr = { arr with Data = Some updatedData }
            let updatedTracks = updateSingleArrangement state.Tracks trackIndex arrIndex updatedArr

            { state with Tracks = updatedTracks }, Cmd.none

    let private replacementToneView state trackIndex arrIndex dispatch =
        let arrangement = state.Tracks.[trackIndex].Arrangements.[arrIndex]
        let data = arrangement.Data |> Option.get
        let commonTones =
            state.CommonTones
            |> Map.find arrangement.Name
            |> Seq.skip 1 // Skip the base tone name
            |> Seq.filter (fun t -> not (String.IsNullOrEmpty(t)))

        StackPanel.create [
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.children [
                Grid.create [
                    Grid.columnDefinitions "150, 150"
                    Grid.rowDefinitions (Seq.replicate data.ToneNames.Length "*" |> String.concat ",")
                    Grid.children [
                        for (i, tone) in data.ToneNames |> List.indexed do
                            yield TextBlock.create [
                                    Grid.row i
                                    TextBlock.margin 2.0
                                    TextBlock.text tone
                                    TextBlock.verticalAlignment VerticalAlignment.Center
                                  ]
                            yield ComboBox.create [
                                    yield Grid.row i
                                    yield Grid.column 1
                                    yield ComboBox.margin 2.0
                                    yield ComboBox.height 30.0
                                    yield ComboBox.dataItems commonTones
                                    match data.ToneReplacements |> Map.tryFind tone with
                                    | Some replacement ->
                                        yield ComboBox.selectedItem replacement
                                    | None -> ()
                                    yield ComboBox.onSelectedItemChanged (fun item -> SetReplacementTone(trackIndex, arrIndex, tone, (string item)) |> dispatch)
                                  ]
                    ]
                ]
                Button.create [
                    Button.content "OK"
                    Button.fontSize 15.0
                    Button.horizontalAlignment HorizontalAlignment.Center
                    Button.width 120.0
                    Button.onClick (fun _ -> ToneReplacementClosed |> dispatch)
                ]
            ]
        ]

    let view state dispatch =
        match state.ReplacementToneEditor with
        | Some (trackIndex, arrIndex) ->
            replacementToneView state trackIndex arrIndex dispatch :> IView
        | None ->
            TabControl.create [
                TabControl.tabStripPlacement Dock.Top
                TabControl.viewItems [
                    TabItem.create [
                        TabItem.header "Tracks"
                        TabItem.content (
                            DockPanel.create [
                                DockPanel.children [
                                    TopControls.view state (TopControlsMsg >> dispatch)

                                    // Status Bar with Message
                                    Border.create [
                                        Border.classes [ "statusbar" ]
                                        Border.minHeight 28.0
                                        Border.background "#222222"
                                        Border.dock Dock.Bottom
                                        Border.padding 5.0
                                        Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                                    ]

                                    BottomControls.view state (BottomControlsMsg >> dispatch)

                                    TrackList.view state (TrackListMsg >> dispatch)
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
