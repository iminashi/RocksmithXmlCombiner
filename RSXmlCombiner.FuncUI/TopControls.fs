namespace RSXmlCombiner.FuncUI

module TopControls =
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Input
    open Types
    open Avalonia.Layout
    open Avalonia.FuncUI.Types

    type Msg =
        | SelectAddTrackFiles
        | AddTrack of fileNames : string[]
        | SelectOpenProjectFile
        | OpenProject of fileNames : string[]
        | SelectToolkitTemplate
        | ImportToolkitTemplate of fileNames : string[]
        | NewProject
        | SaveProject of fileName : string
        | SelectSaveProjectFile

    //let init = ()

    let private handleHotkeys dispatch (event : KeyEventArgs) =
        match event.KeyModifiers with
        | KeyModifiers.Control ->
            match event.Key with
            | Key.O -> dispatch SelectOpenProjectFile
            | Key.S -> dispatch SelectSaveProjectFile
            | Key.N -> dispatch NewProject
            | _ -> ()
        | _ -> ()

    let update (msg: Msg) state : unit * Cmd<_> =
        match msg with
        | SelectAddTrackFiles ->
            let selectFiles = Dialogs.openFileDialog "Select Arrangement File(s)" Dialogs.xmlFileFilter true
            state, Cmd.OfAsync.perform (fun _ -> selectFiles) () (fun files -> AddTrack files)

        | AddTrack -> (), Cmd.none
        | ImportToolkitTemplate -> (), Cmd.none
        | NewProject -> (), Cmd.none
        | OpenProject -> (), Cmd.none
        | SaveProject -> (), Cmd.none

        | SelectToolkitTemplate ->
            let files = Dialogs.openFileDialog "Select Toolkit Template" Dialogs.toolkitTemplateFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun files -> ImportToolkitTemplate files)

        | SelectOpenProjectFile ->
            let files = Dialogs.openFileDialog "Select Project File" Dialogs.projectFileFilter false
            state, Cmd.OfAsync.perform (fun _ -> files) () (fun files -> OpenProject files)

        | SelectSaveProjectFile ->
            let targetFile = Dialogs.saveFileDialog "Save Project As" Dialogs.projectFileFilter (Some "combo.rscproj")
            state, Cmd.OfAsync.perform (fun _ -> targetFile) () (fun file -> SaveProject file)

    let view state dispatch =
        // Top Panel
        Grid.create [
            DockPanel.dock Dock.Top
            Grid.margin (15.0, 0.0)
            Grid.classes [ "topcontrols" ]
            Grid.columnDefinitions "auto,*,auto"
            Grid.children [
                // Track Creation Buttons
                StackPanel.create [
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.spacing 15.0
                    StackPanel.children [
                        Button.create [
                            Button.content "Add Track..."
                            Button.onClick (fun _ -> dispatch SelectAddTrackFiles)
                        ]
                        Button.create [
                            Button.content "Import..."
                            Button.onClick (fun _ -> dispatch SelectToolkitTemplate)
                            ToolTip.tip "Imports a track from a Toolkit template file."
                         ]
                    ]
                ]
                //ComboBox.create [
                //    ComboBox.dataItems state.Project.Templates
                //]

                // Right Side Panel
                StackPanel.create [
                    Grid.column 2
                    StackPanel.orientation Orientation.Horizontal
                    StackPanel.spacing 15.0
                    StackPanel.children [
                        Button.create [
                            Button.content "New Project"
                            Button.onClick (fun _ -> dispatch NewProject)
                            Button.hotKey (KeyGesture.Parse "Ctrl+N") // TODO: Hook up hot keys
                        ]
                        Button.create [
                            Button.content "Open Project..."
                            Button.onClick (fun _ -> dispatch SelectOpenProjectFile)
                            Button.hotKey (KeyGesture.Parse "Ctrl+O")
                        ]
                        Button.create [
                            Button.content "Save Project..."
                            Button.onClick (fun _ -> dispatch SelectSaveProjectFile)
                            //Button.isEnabled (state.Project.Tracks.Length > 0)
                            Button.hotKey (KeyGesture.Parse "Ctrl+S")
                        ]
                    ]
                ]
            ]
        ]

