namespace RSXmlCombiner.FuncUI

[<AutoOpen>]
module Types =
    open System
    open Rocksmith2014Xml

    type CommonTones = Map<string, string[]>

    type ArrangementType = 
        | Unknown =    0b000000000
        | Lead =       0b000000001
        | Rhythm =     0b000000010
        | Combo =      0b000000100
        | Bass =       0b000001000
        | Vocals =     0b000010000
        | JVocals =    0b000100000
        | ShowLights = 0b001000000

    let private instrumentalArrangement = ArrangementType.Lead ||| ArrangementType.Rhythm ||| ArrangementType.Combo ||| ArrangementType.Bass
    let private vocalsArrangement = ArrangementType.Vocals ||| ArrangementType.JVocals
    let private otherArrangement = vocalsArrangement ||| ArrangementType.ShowLights

    /// Tests if the arrangement type is lead, rhythm, bass or combo.
    let isInstrumental arrType = (arrType &&& instrumentalArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals or j-vocals.
    let isVocals arrType = (arrType &&& vocalsArrangement) <> ArrangementType.Unknown
    /// Tests if the arrangement type is vocals, j-vocals or show lights.
    let isOther arrType = (arrType &&& otherArrangement) <> ArrangementType.Unknown

    type ArrangementOrdering = Main | Alternative | Bonus

    type InstrumentalArrangementData = {
        Ordering : ArrangementOrdering
        BaseToneIndex : int
        ToneNames : string list
        ToneReplacements : Map<string, int> }

    type Arrangement = {
        Name : string
        FileName : string option
        ArrangementType : ArrangementType
        Data : InstrumentalArrangementData option }

    let createNamePrefix ordering = 
        match ordering with
        | ArrangementOrdering.Alternative -> "Alt. "
        | ArrangementOrdering.Bonus -> "Bonus "
        | _ -> ""

    let createInstrumental fileName (arrType : ArrangementType option) =
        let song = InstrumentalArrangement.Load(fileName)
        let arrangementType =
            match arrType with
            | Some t -> t
            | None ->
                if song.ArrangementProperties.PathLead = byte 1 then ArrangementType.Lead
                else if song.ArrangementProperties.PathRhythm = byte 1 then ArrangementType.Rhythm
                else if song.ArrangementProperties.PathBass = byte 1 then ArrangementType.Bass
                else ArrangementType.Unknown

        let toneNames =
            if isNull song.ToneChanges then
                []
            else
                [
                    if not (String.IsNullOrEmpty song.ToneA) then yield song.ToneA
                    if not (String.IsNullOrEmpty song.ToneB) then yield song.ToneB
                    if not (String.IsNullOrEmpty song.ToneC) then yield song.ToneC
                    if not (String.IsNullOrEmpty song.ToneD) then yield song.ToneD
                ]

        let ordering =
            if song.ArrangementProperties.BonusArrangement = byte 1 then ArrangementOrdering.Bonus
            else if song.ArrangementProperties.Represent = byte 0 then ArrangementOrdering.Alternative
            else ArrangementOrdering.Main

        let arrData = {
            Ordering = ordering
            BaseToneIndex = -1
            ToneNames = toneNames
            ToneReplacements = Map.empty }

        let name = (createNamePrefix ordering) + arrangementType.ToString()

        { FileName = Some fileName
          ArrangementType = arrangementType
          Name = name
          Data = Some arrData }

    type Track = {
        Title : string
        TrimAmount : int
        AudioFile : string option
        SongLength : int
        Arrangements : Arrangement list }

    let hasAudioFile track = track.AudioFile |> Option.isSome

    type Templates = Templates of Arrangement list

    let getTones fileName =
        let toneNames = InstrumentalArrangement.ReadToneNames(fileName)

        let baseTone = toneNames.[0] |> Option.ofObj
        let toneNamesList = 
            toneNames
            |> Seq.skip 1
            |> Seq.filter (String.IsNullOrEmpty >> not)
            |> Seq.toList

        baseTone, toneNamesList

    let arrTypeHumanized arrType =
        match arrType with
        | ArrangementType.ShowLights -> "Show Lights"
        | ArrangementType.JVocals -> "J-Vocals"
        | _ -> string arrType

    let private createArrName arr =
        match arr.Data with
        | Some data -> createNamePrefix data.Ordering + arr.ArrangementType.ToString()
        | None -> arrTypeHumanized arr.ArrangementType

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
            | None -> 0
        arr.ArrangementType, ordering
