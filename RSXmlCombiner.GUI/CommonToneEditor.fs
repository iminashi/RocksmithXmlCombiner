module RSXmlCombiner.FuncUI.CommonToneEditor

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

let private tonesTemplate (state : ProgramState) arrName (tones : string[]) dispatch =
    let labels = [| "Base"; "Tone A"; "Tone B"; "Tone C"; "Tone D" |]

    StackPanel.create [
        StackPanel.children [
            yield Grid.create [
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
                            TextBox.onLostFocus ((fun arg -> UpdateToneName(arrName, i, (arg.Source :?> TextBox).Text) |> dispatch), SubPatchOptions.OnChangeOf arrName)
                        ]
                ]
            ]
            let toneList =
                state.Tracks
                |> Seq.choose (fun t ->
                    t.Arrangements
                    |> List.tryFind (fun a -> a.Name = arrName)
                    |> Option.bind (fun a -> a.Data)
                    |> Option.map (fun d -> d.ToneNames))
                |> Seq.concat
                |> Seq.distinct
                |> List.ofSeq
            
            if toneList.Length > 0 then
                yield StackPanel.create [
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.fontSize 12.0
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text "Tones in files:"
                        ]
                        ComboBox.create [
                            ComboBox.height 30.0
                            ComboBox.dataItems toneList
                        ]
                    ]
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
                    |> Seq.map (fun (arrName, tones) -> tonesTemplate state arrName tones dispatch :> IView)
                    |> List.ofSeq
                )
            ]
        ]
    ]
