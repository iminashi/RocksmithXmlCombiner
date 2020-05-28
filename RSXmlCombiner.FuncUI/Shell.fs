namespace RSXmlCombiner.FuncUI

module Shell =
    open Elmish
    open Avalonia
    open Avalonia.Controls
    open Avalonia.Input
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI

    type State =
        { trackListState : TrackList.State
          commonTonesState : CommonToneEditor.State }

    type Msg =
        | TrackListMsg of TrackList.Msg
        | CommonTonesMsg of CommonToneEditor.Msg

    let init : State * Cmd<Msg> =
        let trackListState, _ = TrackList.init
        let commonTonesState, _ = CommonToneEditor.init
        { trackListState = trackListState; commonTonesState = commonTonesState }, Cmd.none

    let update (msg: Msg) (state: State): State * Cmd<_> =
        match msg with
        | TrackListMsg trlMsg ->
            match trlMsg with
            | TrackList.Msg.ProjectArrangementsChanged ->
                let tonesState, cmd = CommonToneEditor.update (CommonToneEditor.Msg.TemplatesUpdated(state.trackListState.Project.Templates)) state.commonTonesState
                //let newProject = {state.trackListState.Project with CommonTones = newCommonTones }
                //let newTonesState = { state.commonTonesState with Project = newProject }

                { state with commonTonesState = tonesState }, Cmd.map CommonTonesMsg cmd
            | _ ->
                let trackListState, cmd = TrackList.update trlMsg state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd

        | CommonTonesMsg tonesMsg ->
            let tonesState, cmd = CommonToneEditor.update tonesMsg state.commonTonesState

            { state with commonTonesState = tonesState }, Cmd.map CommonTonesMsg cmd

    let view (state: State) (dispatch) =
        DockPanel.create
            [ DockPanel.children
                [ TabControl.create
                    [ TabControl.tabStripPlacement Dock.Top
                      TabControl.viewItems
                          [ TabItem.create
                                [ TabItem.header "Project"
                                  TabItem.content (TrackList.view state.trackListState (TrackListMsg >> dispatch)) ]
                            TabItem.create
                                [ TabItem.header "Common Tones"
                                  TabItem.content (CommonToneEditor.view state.commonTonesState (CommonTonesMsg >> dispatch)) ] ] ] ] ]
