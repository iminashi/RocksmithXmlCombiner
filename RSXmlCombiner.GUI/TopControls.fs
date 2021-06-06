module RSXmlCombiner.FuncUI.TopControls

open Avalonia.Layout
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open ArrangementType

/// Creates the menu items for adding arrangements.
let private addArrangementMenuItems state dispatch : IView list =
    let (Templates templates) = state.Templates
    let notIncluded arrType =
        templates
        |> List.exists (fun t -> t.ArrangementType = arrType)
        |> not

    let createMenuItem arrType =
        MenuItem.create [
            MenuItem.header (humanize arrType)
            MenuItem.isEnabled (notIncluded arrType)
            MenuItem.onClick (fun _ -> AddTemplate(arrType, None) |> dispatch)
        ]

    [ createMenuItem ArrangementType.JVocals
      createMenuItem ArrangementType.ShowLights ]

let view state dispatch =
    // Top Panel
    Grid.create [
        DockPanel.dock Dock.Top
        Grid.margin (15.0, 0.0)
        Grid.classes [ "topcontrols" ]
        Grid.columnDefinitions "auto,*,auto"
        Grid.rowDefinitions "*,*"
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
                        Button.onClick (fun _ -> dispatch SelectImportProject)
                        ToolTip.tip "Imports a track from a Toolkit template or a DLC Builder project."
                     ]
                ]
            ]

            Menu.create [
                Grid.row 1
                Menu.isEnabled (not state.Tracks.IsEmpty)
                Menu.margin (15.0, 0.0, 0.0, 0.0)
                Menu.viewItems [
                    MenuItem.create [
                        MenuItem.header "Add Arrangement"
                        MenuItem.viewItems (addArrangementMenuItems state dispatch)
                    ]
                ]
            ]

            // Right Side Panel
            StackPanel.create [
                Grid.column 2
                StackPanel.orientation Orientation.Horizontal
                StackPanel.spacing 15.0
                StackPanel.children [
                    Button.create [
                        Button.content "New Project"
                        Button.onClick (fun _ -> dispatch NewProject) 
                    ]
                    Button.create [
                        Button.content "Open Project..."
                        Button.onClick (fun _ -> dispatch SelectOpenProjectFile)
                    ]
                    Button.create [
                        Button.content "Save Project..."
                        Button.onClick (fun _ -> dispatch SelectSaveProjectFile)
                        Button.isEnabled (not state.Tracks.IsEmpty)
                    ]
                ]
            ]
        ]
    ]
