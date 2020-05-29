namespace RSXmlCombiner.FuncUI

module Types =
    open System.Text.Json.Serialization
    open System.IO
    open System
    open Rocksmith2014Xml

    type ArrangementType = 
        | Unknown =    0b000000000
        | Lead =       0b000000001
        | Rhythm =     0b000000010
        | Combo =      0b000000100
        | Bass =       0b000001000
        | Vocals =     0b000010000
        | JVocals =    0b000100000
        | ShowLights = 0b001000000

    let instrumentalArrangement = ArrangementType.Lead ||| ArrangementType.Rhythm ||| ArrangementType.Combo ||| ArrangementType.Bass
    let vocalsArrangement = ArrangementType.Vocals ||| ArrangementType.JVocals

    let isInstrumental arrType =
        (arrType &&& instrumentalArrangement) <> ArrangementType.Unknown

    let isVocals arrType =
        (arrType &&& vocalsArrangement) <> ArrangementType.Unknown

    type ArrangementOrdering = Main | Alternative | Bonus

    type InstrumentalArrangementData = {
        Ordering : ArrangementOrdering
        BaseTone : string option
        ToneNames : string list
        ToneReplacements : Map<string, string> }

    type Arrangement = {
        Name : string
        FileName : string option
        ArrangementType : ArrangementType
        Data : InstrumentalArrangementData option }

    let createInstrumental fileName (baseTone : string option) =
        let song = RS2014Song.Load(fileName)
        let arrangementType =
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

        let arrData = {
            Ordering = ArrangementOrdering.Main
            BaseTone = Option.ofObj song.ToneBase |> Option.orElse baseTone
            ToneNames = toneNames
            ToneReplacements = Map[] }

        { FileName = Some fileName
          ArrangementType = arrangementType
          Name = arrangementType.ToString()
          Data = Some arrData }

    type Track = {
        Title : string
        TrimAmount : float32
        AudioFile : string option
        SongLength : float32
        Arrangements : Arrangement list }

    type CombinerProject = {
        Tracks : Track list
        CommonTones : Map<string, string[]>
        [<JsonIgnore>]
        Templates : Arrangement list /// Name and type of arrangements that must be found on every track.
        CombinationTitle : string
        CoercePhrases : bool
        AddTrackNamesToLyrics : bool }

    let emptyProject = {
        Tracks = []
        Templates = []
        CommonTones = Map.empty
        CombinationTitle = ""
        CoercePhrases = true
        AddTrackNamesToLyrics = true }

    let getTones fileName =
        let song = RS2014Song.Load(fileName)

        let bt = song.ToneBase |> Option.ofObj
        let toneNames =
            match song.ToneChanges with
            | tones when tones.Count > 0 ->
                [
                    if not (String.IsNullOrEmpty(song.ToneA)) then yield song.ToneA
                    if not (String.IsNullOrEmpty(song.ToneB)) then yield song.ToneB
                    if not (String.IsNullOrEmpty(song.ToneC)) then yield song.ToneC
                    if not (String.IsNullOrEmpty(song.ToneD)) then yield song.ToneD
                ]
            | _ -> []

        bt, toneNames

    let arrTypeHumanized arrType =
        match arrType with
        | ArrangementType.ShowLights -> "Show Lights"
        | ArrangementType.JVocals -> "J-Vocals"
        | _ -> string arrType