namespace RSXmlCombiner.FuncUI

module Shell =
    open System
    open Elmish
    open Avalonia
    open Avalonia.Controls
    open Avalonia.Input
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI

    type State =
        { trackListState : TrackList.State
          commonTonesState : CommonToneEditor.State
          bottomControlsState : BottomControls.State
          StatusMessage : string }

    type Msg =
        | TrackListMsg of TrackList.Msg
        | CommonTonesMsg of CommonToneEditor.Msg
        | TopControlsMsg of TopControls.Msg
        | BottomControlsMsg of BottomControls.Msg
        | UpdateCommonTones

    let init : State * Cmd<Msg> =
        let trackListState, _ = TrackList.init
        let commonTonesState, _ = CommonToneEditor.init
        let bottomControlsState = BottomControls.init

        { trackListState = trackListState
          commonTonesState = commonTonesState
          bottomControlsState = bottomControlsState
          StatusMessage = "" }, Cmd.none

    let update (msg: Msg) (state: State): State * Cmd<_> =
        match msg with
        | TrackListMsg trlMsg ->
            match trlMsg with
            | TrackList.Msg.ProjectArrangementsChanged(templates, commonTones) ->
                let tonesState, cmd = CommonToneEditor.update (CommonToneEditor.Msg.TemplatesUpdated(templates, commonTones)) state.commonTonesState
                let newBcState, _ = BottomControls.update (BottomControls.Msg.TracksUpdated(state.trackListState.Project.Tracks)) state.bottomControlsState

                { state with commonTonesState = tonesState; bottomControlsState = newBcState }, Cmd.map CommonTonesMsg cmd
            | _ ->
                let trackListState, cmd = TrackList.update trlMsg state.trackListState
                let newBcState, _ = BottomControls.update (BottomControls.Msg.TracksUpdated(trackListState.Project.Tracks)) state.bottomControlsState

                { state with trackListState = trackListState; bottomControlsState = newBcState }, Cmd.map TrackListMsg cmd

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

        | BottomControlsMsg msg ->
            match msg with 
            | BottomControls.Msg.CombineAudioFiles targetFile ->
                if String.IsNullOrEmpty targetFile then
                    // User canceled the dialog
                    state, Cmd.none
                else
                    let message = AudioCombiner.combineAudioFiles state.trackListState.Project.Tracks targetFile
                    { state with StatusMessage = message }, Cmd.none
            
            | BottomControls.Msg.CombineArrangements targetFolder ->
                if String.IsNullOrEmpty(targetFolder) then
                    // User canceled the dialog
                    state, Cmd.none
                else
                    ArrangementCombiner.combineArrangements state.trackListState.Project targetFolder
                    { state with StatusMessage = "Arrangements combined." }, Cmd.none

            | _ ->
                let bcState, cmd = BottomControls.update msg state.bottomControlsState

                { state with bottomControlsState = bcState }, Cmd.map BottomControlsMsg cmd

    let view (state: State) (dispatch) =
        TabControl.create [
            TabControl.tabStripPlacement Dock.Top
            TabControl.viewItems [
                TabItem.create [
                    TabItem.header "Project"
                    TabItem.onIsSelectedChanged (fun selected -> if selected then dispatch UpdateCommonTones)
                    TabItem.content (
                        DockPanel.create [
                            DockPanel.children [
                                TopControls.view () (TopControlsMsg >> dispatch)

                                // Status Bar with Message
                                Border.create [
                                    Border.classes [ "statusbar" ]
                                    Border.minHeight 25.0
                                    Border.background "Black"
                                    Border.dock Dock.Bottom
                                    Border.padding 5.0
                                    Border.child (TextBlock.create [ TextBlock.text state.StatusMessage ])
                                ]

                                BottomControls.view state.bottomControlsState (BottomControlsMsg >> dispatch)

                                TrackList.view state.trackListState (TrackListMsg >> dispatch)
                            ]
                        ]
                    )
                ]
                TabItem.create [
                    TabItem.header "Common Tones"
                    TabItem.content (CommonToneEditor.view state.commonTonesState (CommonTonesMsg >> dispatch))
                ]
            ]
        ]

