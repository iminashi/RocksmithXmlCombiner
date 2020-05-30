namespace RSXmlCombiner.FuncUI

module CommonToneEditor =
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Types
    open Avalonia.Layout
    open Avalonia.FuncUI.Types

    type State = CombinerProject

    type Msg =
        | UpdateToneName of title:string * index:int * newName:string

    let update (msg: Msg) (state: State): State * Cmd<_> =
        match msg with
        | UpdateToneName (title, index, newName) ->
            let names = state.CommonTones |> Map.find title
            let newTones = state.CommonTones |> Map.add title (names |> Array.mapi (fun i name -> if i = index then newName else name))

            { state with CommonTones = newTones }, Cmd.none

    let private tonesTemplate title (tones : string[]) dispatch =
        let leftSide = [| "Base"; "Tone A"; "Tone B"; "Tone C"; "Tone D" |]

        Grid.create [
            Grid.columnDefinitions "60,150"
            Grid.rowDefinitions "*,*,*,*,*,*"
            Grid.children [
                yield TextBlock.create [
                    TextBlock.text title
                    TextBlock.fontSize 16.0
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    Grid.columnSpan 2
                ]
                for i = 0 to tones.Length - 1 do
                    yield TextBlock.create [
                        Grid.row (i + 1)
                        TextBlock.margin 2.0
                        TextBlock.text leftSide.[i]
                        TextBlock.verticalAlignment VerticalAlignment.Center
                    ]
                    yield TextBox.create [
                        Grid.column 1
                        Grid.row (i + 1)
                        TextBox.margin 2.0
                        TextBox.text (tones.[i])
                        // TODO: Enabled
                        TextBox.onTextChanged ((fun text -> dispatch (UpdateToneName(title, i, text))), SubPatchOptions.OnChangeOf title)
                    ]
            ]
        ]

    let view (state: State) (dispatch) =
        StackPanel.create [
            StackPanel.spacing 10.0
            StackPanel.margin 10.0
            StackPanel.horizontalAlignment HorizontalAlignment.Center
            StackPanel.verticalAlignment VerticalAlignment.Top
            StackPanel.children [
                WrapPanel.create [
                    WrapPanel.orientation Orientation.Horizontal
                    WrapPanel.children (
                        state.CommonTones
                        |> Map.toList
                        |> List.map (fun (title, tones) -> tonesTemplate title tones dispatch :> IView)
                    )
                ]
            ]
        ]
