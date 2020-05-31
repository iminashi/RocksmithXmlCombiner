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

    type State =
        { Project : CombinerProject
          StatusMessage : string }

    type Msg =
        | TrackListMsg of TrackList.Msg
        | CommonTonesMsg of CommonToneEditor.Msg
        | TopControlsMsg of TopControls.Msg
        | BottomControlsMsg of BottomControls.Msg

    let init () = { Project = emptyProject; StatusMessage = "" }, Cmd.none

    let update (shellMsg: Msg) (state: State): State * Cmd<_> =
        match shellMsg with
        | TrackListMsg msg ->
            match msg with
            | TrackList.Msg.StatusMessage message ->
                { state with StatusMessage = message }, Cmd.none
            | _ ->
                let trackListState, cmd = TrackList.update msg state.Project
                { state with Project = trackListState }, Cmd.map TrackListMsg cmd

        | CommonTonesMsg msg ->
            let tonesState, cmd = CommonToneEditor.update msg state.Project
            { state with Project = tonesState }, Cmd.map CommonTonesMsg cmd

        | TopControlsMsg msg ->
            let topCtrlState, cmd = TopControls.update msg state.Project
            { state with Project = topCtrlState }, Cmd.map TopControlsMsg cmd

        | BottomControlsMsg msg ->
            match msg with 
            | BottomControls.Msg.StatusMessage message ->
                { state with StatusMessage = message }, Cmd.none

            | _ ->
                let bcState, cmd = BottomControls.update msg state.Project
                { state with Project = bcState }, Cmd.map BottomControlsMsg cmd

    let view (state: State) (dispatch) =
        TabControl.create [
            TabControl.tabStripPlacement Dock.Top
            TabControl.viewItems [
                TabItem.create [
                    TabItem.header "Tracks"
                    TabItem.content (
                        DockPanel.create [
                            DockPanel.children [
                                TopControls.view state.Project (TopControlsMsg >> dispatch)

                                // Status Bar with Message
                                Border.create [
                                    Border.classes [ "statusbar" ]
                                    Border.minHeight 28.0
                                    Border.background "#222222"
                                    Border.dock Dock.Bottom
                                    Border.padding 5.0
                                    Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                                ]

                                BottomControls.view state.Project (BottomControlsMsg >> dispatch)

                                TrackList.view state.Project (TrackListMsg >> dispatch)
                            ]
                        ]
                    )
                ]
                TabItem.create [
                    TabItem.header "Common Tones"
                    TabItem.content (CommonToneEditor.view state.Project (CommonTonesMsg >> dispatch))
                ]
            ]
        ]
