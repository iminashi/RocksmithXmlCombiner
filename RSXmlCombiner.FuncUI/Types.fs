namespace RSXmlCombiner.FuncUI

module Types =
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

    let InstrumentalArrangement = ArrangementType.Lead ||| ArrangementType.Rhythm ||| ArrangementType.Combo ||| ArrangementType.Bass
    let VocalsArrangement = ArrangementType.Vocals ||| ArrangementType.JVocals

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
