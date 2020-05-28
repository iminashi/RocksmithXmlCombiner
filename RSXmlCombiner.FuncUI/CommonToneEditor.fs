namespace RSXmlCombiner.FuncUI

module CommonToneEditor =
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Types
    open Avalonia.Layout
    open Avalonia.FuncUI.Types

    type State = { CommonTones : Map<string, string[]> }

    type Msg =
        | OkClick
        | TemplatesUpdated of Arrangement list

    let private updateCommonTones =
            Seq.filter (fun t -> t.ArrangementType |> Types.isInstrumental )
            >> Seq.map (fun t -> t.Name, Array.create 5 "" )
            >> Map.ofSeq

    let init : State * Cmd<Msg> = { CommonTones = Map.empty }, Cmd.none

    let update (msg: Msg) (state: State): State * Cmd<_> =
        match msg with
        | OkClick ->
            state, Cmd.ofMsg OkClick

        | TemplatesUpdated templates ->
            { state with CommonTones = updateCommonTones templates }, Cmd.none

    let private tonesTemplate title (tones : string[])  dispatch =
        let leftSide = [ "Base"; "Tone A"; "Tone B"; "Tone C"; "Tone D" ]

        Grid.create [
            Grid.columnDefinitions "60,150"
            Grid.rowDefinitions "*,*,*,*,*,*"
            Grid.children [
                yield TextBlock.create [
                    TextBlock.text title
                    TextBlock.fontSize 16.0
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    Grid.columnSpan 2
                    Grid.row 0
                ]
                for i = 0 to tones.Length - 1 do
                    yield TextBlock.create [
                        TextBlock.margin 2.0
                        TextBlock.text leftSide.[i]
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        Grid.row (i + 1)
                    ]
                    yield TextBox.create [
                        Grid.column 1
                        Grid.row (i + 1)
                        TextBox.margin 2.0
                        TextBox.text (tones.[i])
                        // TODO: Enabled
                        // TODO: Binding
                    ]
            ]
        ]

    let view (state: State) (dispatch) =
        StackPanel.create [
            StackPanel.spacing 10.0
            StackPanel.margin 10.0
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.verticalAlignment VerticalAlignment.Center
            StackPanel.children [
                WrapPanel.create [
                    WrapPanel.orientation Orientation.Horizontal
                    WrapPanel.children (state.CommonTones |> Map.toList |> List.map (fun (title, tones) -> tonesTemplate title tones dispatch :> IView))
                ]
                Button.create [
                    //Button.onClick (fun _ -> dispatch OkClick)
                    Button.content "OK"
                ]
            ]
        ]
