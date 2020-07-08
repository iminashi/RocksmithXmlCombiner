module RSXmlCombiner.FuncUI.CommonToneEditor

open Elmish
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.FuncUI.Types
open System

type Msg =
    | UpdateToneName of arrName:string * index:int * newName:string
    | SelectedToneFromFileChanged of arrName:string * selectedTone:string
    | AddSelectedToneFromFile of arrName:string

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
                |> Map.add arrName (names |> Array.updateAt index newName)

            { state with CommonTones = newTones }, Cmd.none

    | SelectedToneFromFileChanged (arrName, selectedTone) ->
        let st = state.SelectedFileTones |> Map.add arrName selectedTone
        { state with SelectedFileTones = st }, Cmd.none

    | AddSelectedToneFromFile (arrName) ->
        let tones = state.CommonTones.[arrName]
        // Find an empty index that is not the base tone
        let availableIndex = tones.[1..] |> Array.tryFindIndex String.IsNullOrEmpty
        let selectedTone = state.SelectedFileTones |> Map.tryFind arrName

        match availableIndex, selectedTone with
        | Some i, Some newTone ->
            let updatedTones =
                let i = i + 1
                if i = 1 && tones.[0] |> String.IsNullOrEmpty then
                    // If the base tone and tone A are empty, use this name for them both
                    tones |> Array.mapi (fun j t -> if j = 0 || j = i then newTone else t)
                else
                    tones |> Array.updateAt i newTone

            let updatedCommonTones = state.CommonTones |> Map.add arrName updatedTones
            { state with CommonTones = updatedCommonTones }, Cmd.none
        | _ ->
            state, Cmd.none

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
                let canAdd =
                    tones.[1..] |> Array.exists String.IsNullOrEmpty
                    &&
                    state.SelectedFileTones
                    |> Map.tryFind arrName
                    |> Option.map String.notEmpty
                    |> Option.defaultValue false

                yield StackPanel.create [
                    StackPanel.children [
                        TextBlock.create [
                            TextBlock.fontSize 12.0
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text "Tone Names in Files:"
                        ]
                        ComboBox.create [
                            ComboBox.height 30.0
                            ComboBox.margin 2.0
                            ComboBox.dataItems toneList
                            ComboBox.selectedItem (state.SelectedFileTones |> Map.tryFind arrName |> Option.toObj)
                            ComboBox.onSelectedItemChanged (fun item -> SelectedToneFromFileChanged(arrName, item |> string) |> dispatch)
                        ]
                        Button.create [
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.content "Add"
                            Button.isEnabled canAdd
                            Button.onClick (fun _ -> AddSelectedToneFromFile arrName |> dispatch)
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
