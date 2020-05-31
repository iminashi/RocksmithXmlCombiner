namespace RSXmlCombiner.FuncUI

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

    let view state dispatch =
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
        ]
