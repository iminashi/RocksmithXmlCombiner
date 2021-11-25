module RSXmlCombiner.FuncUI.Help

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

let helpView =
    StackPanel.create [
        StackPanel.classes [ "help" ]
        StackPanel.spacing 5.0
        StackPanel.margin (20.0, 0.0)
        StackPanel.children [
            TextBlock.create [
                TextBlock.classes [ "h1" ]
                TextBlock.text "Basic Use"
            ]
            TextBlock.create [ TextBlock.text "1. Add tracks by manually selecting the XML arrangements or by importing a Toolkit template." ]
            TextBlock.create [ TextBlock.text "2. In the Common Tones tab, enter the tone names you want to use for each arrangement." ]
            TextBlock.create [ TextBlock.text "3. For each of the arrangements, either set the base tone using the dropdown, or set the replacement tones with the Tones button." ]

            TextBlock.create [
                TextBlock.classes [ "h1" ]
                TextBlock.text "Note"
            ]
            TextBlock.create [ TextBlock.text "● For instrumental and show light arrangements, a file must be set on each track for a combined arrangement to be generated." ]
            TextBlock.create [ TextBlock.text "● A combined lyrics arrangement will be generated even if a file is not set on some of the tracks." ]
            TextBlock.create [ TextBlock.text "● The \"Coerce to 100 Phrases\" option only works when combining files that do not have DD levels." ]

            TextBlock.create [
                TextBlock.classes [ "h1" ]
                TextBlock.text "Tips"
            ]
            TextBlock.create [ TextBlock.text "● The base tone in the common tones tab is the tone the combined arrangement will start with. Usually it is one of the four tones, but it can be a different, fifth tone also." ]
            TextBlock.create [ TextBlock.text "● Use the context menu on an arrangement to remove the arrangement file, or to remove that arrangement type from all of the tracks." ]
            TextBlock.create [ TextBlock.text "● The color of the \"Tones\" button indicates whether the replacement tones are properly set or not." ]
            TextBlock.create [ TextBlock.text "● The trim amount is set automatically from the first beat on the beat map. If the first beat is set correctly, you do not need to touch the trim amount. You may lower the value if you want to increase the silence between the tracks." ]
        ]
    ] :> IView
