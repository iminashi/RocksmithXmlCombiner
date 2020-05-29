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
        | TopControlsMsg of TopControls.Msg
        | UpdateCommonTones

    let init : State * Cmd<Msg> =
        let trackListState, _ = TrackList.init
        let commonTonesState, _ = CommonToneEditor.init
        { trackListState = trackListState; commonTonesState = commonTonesState }, Cmd.none

    let update (msg: Msg) (state: State): State * Cmd<_> =
        match msg with
        | TrackListMsg trlMsg ->
            match trlMsg with
            | TrackList.Msg.ProjectArrangementsChanged(templates, commonTones) ->
                let tonesState, cmd = CommonToneEditor.update (CommonToneEditor.Msg.TemplatesUpdated(templates, commonTones)) state.commonTonesState

                { state with commonTonesState = tonesState }, Cmd.map CommonTonesMsg cmd
            | _ ->
                let trackListState, cmd = TrackList.update trlMsg state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd

        | CommonTonesMsg tonesMsg ->
            let tonesState, cmd = CommonToneEditor.update tonesMsg state.commonTonesState

            { state with commonTonesState = tonesState }, Cmd.map CommonTonesMsg cmd

        | UpdateCommonTones ->
            let newTrackListState = 
                { state.trackListState with Project = {state.trackListState.Project with CommonTones = state.commonTonesState.CommonTones } }

            { state with trackListState = newTrackListState }, Cmd.none

        | TopControlsMsg msg ->
            match msg with
            | TopControls.Msg.AddTrack fileNames ->
                let trackListState, cmd = TrackList.update (TrackList.Msg.AddTrack(fileNames)) state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd
            | TopControls.Msg.OpenProject fileNames ->
                let trackListState, cmd = TrackList.update (TrackList.Msg.OpenProject(fileNames)) state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd
            | TopControls.Msg.ImportToolkitTemplate fileNames ->
                let trackListState, cmd = TrackList.update (TrackList.Msg.ImportToolkitTemplate(fileNames)) state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd
            | TopControls.Msg.NewProject ->
                let trackListState, cmd = TrackList.update (TrackList.Msg.NewProject) state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd
            | TopControls.Msg.SaveProject fileName ->
                let trackListState, cmd = TrackList.update (TrackList.Msg.SaveProject(fileName)) state.trackListState

                { state with trackListState = trackListState }, Cmd.map TrackListMsg cmd
            | _ ->
                let _, cmd = TopControls.update msg ()

                state, Cmd.map TopControlsMsg cmd

    let view (state: State) (dispatch) =
        DockPanel.create
            [ DockPanel.children
                [ TabControl.create
                    [ TabControl.tabStripPlacement Dock.Top
                      TabControl.viewItems
                          [ TabItem.create
                                [ TabItem.header "Project"
                                  TabItem.onIsSelectedChanged (fun selected -> if selected then dispatch UpdateCommonTones)
                                  TabItem.content (
                                    DockPanel.create [
                                        DockPanel.children ([
                                            TopControls.view () (TopControlsMsg >> dispatch)
                                        ] @ TrackList.view state.trackListState (TrackListMsg >> dispatch))
                                    ])
                                ]
                            TabItem.create
                                [ TabItem.header "Common Tones"
                                  TabItem.content (CommonToneEditor.view state.commonTonesState (CommonTonesMsg >> dispatch)) ] ] ] ] ]
