namespace RSXmlCombiner.FuncUI

module CommonToneEditor =
    open System
    open Elmish
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout
    open Avalonia.FuncUI.Types

    type Msg =
        | UpdateToneName of arrName:string * index:int * newName:string

    let update (msg: Msg) state : ProgramState * Cmd<_> =
        match msg with
        | UpdateToneName (arrName, index, newName) ->
            let names = state.CommonTones |> Map.find arrName
            let oldName = names.[index]
            if oldName = newName then
                state, Cmd.none
            else 
                let newTones = 
                    state.CommonTones
                    |> Map.add arrName (names |> Array.mapi (fun i name -> if i = index then newName else name))

                { state with CommonTones = newTones }, Cmd.none

    let private tonesTemplate arrName (tones : string[]) dispatch =
        let labels = [| "Base"; "Tone A"; "Tone B"; "Tone C"; "Tone D" |]

        Grid.create [
            Grid.columnDefinitions "60,150"
            Grid.rowDefinitions "*,*,*,*,*,*"
            Grid.children [
                yield TextBlock.create [
                    TextBlock.text arrName
                    TextBlock.fontSize 16.0
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    Grid.columnSpan 2
                ]
                for i = 0 to tones.Length - 1 do
                    yield TextBlock.create [
                        Grid.row (i + 1)
                        TextBlock.margin (8.0, 2.0, 0.0, 2.0)
                        TextBlock.text labels.[i]
                        TextBlock.verticalAlignment VerticalAlignment.Center
                    ]
                    yield TextBox.create [
                        Grid.column 1
                        Grid.row (i + 1)
                        TextBox.margin 2.0
                        TextBox.text tones.[i]
                        //TextBox.isEnabled (i = 0 || not <| String.IsNullOrEmpty(tones.[i - 1]))
                        TextBox.onLostFocus ((fun arg -> UpdateToneName(arrName, i, (arg.Source :?> TextBox).Text) |> dispatch), SubPatchOptions.OnChangeOf arrName)
                    ]
            ]
        ]

    let view state dispatch =
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
                        |> Map.toSeq
                        |> Seq.map (fun (arrName, tones) -> tonesTemplate arrName tones dispatch :> IView)
                        |> List.ofSeq
                    )
                ]
            ]
        ]
