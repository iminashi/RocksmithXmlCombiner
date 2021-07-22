[<AutoOpen>]
module RSXmlCombiner.FuncUI.Types

open Rocksmith2014.XML
open System

[<Measure>] type ms

/// A map matching an arrangement name into an array of tone names.
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

/// Creates a name prefix based on the given arrangement ordering.
let createNamePrefix = function
    | ArrangementOrdering.Alternative ->
        "Alt. "
    | ArrangementOrdering.Bonus ->
        "Bonus "
    | _ ->
        String.Empty

/// Creates an instrumental arrangement from the given file.
let createInstrumental fileName (arrType: ArrangementType option) =
    let song = InstrumentalArrangement.Load(fileName)
    let arrangementType =
        arrType
        |> Option.defaultWith (fun () -> ArrangementType.fromArrangement song)

    let toneNames =
        if isNull song.Tones.Changes then
            List.empty
        else
            song.Tones.Names
            |> Seq.filter String.notEmpty
            |> Seq.toList

    let ordering =
        if song.MetaData.ArrangementProperties.BonusArrangement then
            ArrangementOrdering.Bonus
        elif song.MetaData.ArrangementProperties.Represent then
            ArrangementOrdering.Main
        else
            ArrangementOrdering.Alternative

    let arrData =
      { Ordering = ordering
        BaseToneIndex = -1
        ToneNames = toneNames
        ToneReplacements = Map.empty }

    let name = $"{createNamePrefix ordering}{arrangementType}"

    { FileName = Some fileName
      ArrangementType = arrangementType
      Name = name
      Data = Some arrData }

let createOther name fileName arrType =
    { Name = name
      FileName = Some fileName
      ArrangementType = arrType
      Data = None }

type Track = 
  { Title : string
    TrimAmount : int<ms>
    AudioFile : string option
    SongLength : int<ms>
    Arrangements : Arrangement list }

/// Returns true if the track has an audio file set.
let hasAudioFile track = track.AudioFile.IsSome

/// Reads the tone information from the given file.
let getTones fileName =
    let tones = InstrumentalArrangement.ReadToneNames(fileName)

    let baseTone = tones.BaseToneName |> Option.ofObj
    let toneNamesList =
        tones.Names
        |> Seq.filter String.notEmpty
        |> Seq.toList

    baseTone, toneNamesList

let private createArrName arr =
    match arr.Data with
    | Some data ->
        createNamePrefix data.Ordering + arr.ArrangementType.ToString()
    | None ->
        ArrangementType.humanize arr.ArrangementType

/// Creates a template (no file name or arrangement data) from the given arrangement.
let createTemplate arr =
    { Name = createArrName arr
      ArrangementType = arr.ArrangementType
      FileName = None
      Data = None }

let updateSingleArrangement tracks trackIndex arrIndex newArr =
    let changeArrangement arrList =
        arrList
        |> List.mapi (fun i arr -> if i = arrIndex then newArr else arr)

    tracks
    |> List.mapi (fun i t ->
        if i = trackIndex then
            { t with Arrangements = changeArrangement t.Arrangements }
        else
            t)

let arrangementSort (arr: Arrangement) =
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
