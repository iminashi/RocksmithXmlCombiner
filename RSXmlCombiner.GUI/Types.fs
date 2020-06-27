namespace RSXmlCombiner.FuncUI

[<AutoOpen>]
module Types =
    open System
    open Rocksmith2014Xml

    [<Measure>] type ms

    type CommonTones = Map<string, string[]>

    type ArrangementOrdering = Main | Alternative | Bonus

    type InstrumentalArrangementData = 
      { Ordering : ArrangementOrdering
        BaseToneIndex : int
        ToneNames : string list
        ToneReplacements : Map<string, int> }

    type Arrangement = 
      { Name : string
        FileName : string option
        ArrangementType : ArrangementType
        Data : InstrumentalArrangementData option }

    type Templates = Templates of Arrangement list

    let createNamePrefix = function
        | ArrangementOrdering.Alternative -> "Alt. "
        | ArrangementOrdering.Bonus -> "Bonus "
        | _ -> ""

    let createInstrumental fileName (arrType : ArrangementType option) =
        let song = InstrumentalArrangement.Load(fileName)
        let arrangementType =
            arrType |> Option.defaultWith (fun () -> ArrangementType.fromArrProperties song.ArrangementProperties)

        let toneNames =
            if isNull song.ToneChanges then
                []
            else
                [
                    if String.notEmpty song.ToneA then yield song.ToneA
                    if String.notEmpty song.ToneB then yield song.ToneB
                    if String.notEmpty song.ToneC then yield song.ToneC
                    if String.notEmpty song.ToneD then yield song.ToneD
                ]

        let ordering =
            if song.ArrangementProperties.BonusArrangement = 1uy then ArrangementOrdering.Bonus
            else if song.ArrangementProperties.Represent = 0uy then ArrangementOrdering.Alternative
            else ArrangementOrdering.Main

        let arrData =
          { Ordering = ordering
            BaseToneIndex = -1
            ToneNames = toneNames
            ToneReplacements = Map.empty }

        let name = (createNamePrefix ordering) + arrangementType.ToString()

        { FileName = Some fileName
          ArrangementType = arrangementType
          Name = name
          Data = Some arrData }

    type Track = 
      { Title : string
        TrimAmount : int<ms>
        AudioFile : string option
        SongLength : int<ms>
        Arrangements : Arrangement list }

    let hasAudioFile track = track.AudioFile |> Option.isSome

    let getTones fileName =
        let toneNames = InstrumentalArrangement.ReadToneNames(fileName)

        let baseTone = toneNames.[0] |> Option.ofObj
        let toneNamesList = 
            toneNames
            |> Seq.skip 1
            |> Seq.filter String.notEmpty
            |> Seq.toList

        baseTone, toneNamesList

    let private createArrName arr =
        match arr.Data with
        | Some data -> createNamePrefix data.Ordering + arr.ArrangementType.ToString()
        | None -> ArrangementType.humanize arr.ArrangementType

    /// Creates a template (no file name or arrangement data) from the given arrangement.
    let createTemplate arr =
        { Name = createArrName arr; ArrangementType = arr.ArrangementType; FileName = None; Data = None }

    let updateSingleArrangement tracks trackIndex arrIndex newArr =
        let changeArrangement arrList =
            arrList
            |> List.mapi (fun i arr -> if i = arrIndex then newArr else arr)

        tracks
        |> List.mapi (fun i t -> if i = trackIndex then { t with Arrangements = changeArrangement t.Arrangements } else t)

    let arrangementSort (arr : Arrangement) =
        let ordering =
            match arr.Data with
            | Some data ->
                match data.Ordering with
                | Main -> 0
                | Alternative -> 1
                | Bonus -> 2
            | None ->
                if arr.Name.StartsWith("Alt.") then 1
                elif arr.Name.StartsWith("Bonus") then 2
                else 0
        arr.ArrangementType, ordering
