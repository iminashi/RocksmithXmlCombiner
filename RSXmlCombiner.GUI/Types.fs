[<AutoOpen>]
module RSXmlCombiner.FuncUI.Types

open Rocksmith2014.XML

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

type Msg =
    | ToneReplacementClosed
    | SetReplacementTone of trackIndex : int * arrIndex : int * toneName : string * replacementIndex : int
    | ProjectViewActiveChanged of bool
    | CombineAudioProgressChanged of float
    | CombineArrangementsProgressChanged of int

    // Top controls
    | SelectAddTrackFiles
    | AddTrack of arrangementFiles : string[] option
    | SelectOpenProjectFile
    | OpenProject of projectFile : string option
    | SelectToolkitTemplate
    | ImportToolkitTemplate of templateFile : string option
    | NewProject
    | SaveProject of fileName : string option
    | SelectSaveProjectFile
    | AddTemplate of arrType : ArrangementType * ordering : ArrangementOrdering option

    // Bottom controls
    | SelectCombinationTargetFolder
    | CombineAudioFiles of targetFile : string option
    | CombineArrangements of targetFolder :string option
    | UpdateCombinationTitle of newTitle : string
    | CoercePhrasesChanged of bool
    | OnePhrasePerTrackChanged of bool
    | AddTrackNamesChanged of bool
    | CombineAudioCompleted of message : string
    | CombineArrangementsCompleted of unit
    | CreatePreview of targetFile : string option
    | SelectTargetAudioFile of defaultFileName : string option * cmd : (string option -> Msg)

    // Track list
    | RemoveTrack of trackIndex : int
    | ChangeAudioFile of trackIndex : int
    | ChangeAudioFileResult of trackIndex : int * newFile : string option
    | SelectArrangementFile of trackIndex : int * arrIndex : int
    | ChangeArrangementFile of trackIndex : int * arrIndex : int * newFile : string option
    | ArrangementBaseToneChanged of trackIndex : int * arrIndex : int * toneIndex : int
    | RemoveArrangementFile of trackIndex : int * arrIndex : int
    | ShowReplacementToneEditor of trackIndex : int * arrIndex : int
    | TrimAmountChanged of trackIndex : int * trimAmunt : double
    | RemoveTemplate of name : string

    // Common tone editor
    | UpdateToneName of arrName:string * index:int * newName:string
    | SelectedToneFromFileChanged of arrName:string * selectedTone:string
    | AddSelectedToneFromFile of arrName:string

/// Creates a name prefix based on the given arrangement ordering.
let createNamePrefix = function
    | ArrangementOrdering.Alternative -> "Alt. "
    | ArrangementOrdering.Bonus -> "Bonus "
    | _ -> ""

/// Creates an instrumental arrangement from the given file.
let createInstrumental fileName (arrType : ArrangementType option) =
    let song = InstrumentalArrangement.Load(fileName)
    let arrangementType =
        arrType |> Option.defaultWith (fun () -> ArrangementType.fromArrangement song)

    let toneNames =
        if isNull song.Tones.Changes then
            []
        else
            song.Tones.Names |> Seq.filter String.notEmpty |> Seq.toList

    let ordering =
        if song.MetaData.ArrangementProperties.BonusArrangement then ArrangementOrdering.Bonus
        else if song.MetaData.ArrangementProperties.Represent then ArrangementOrdering.Main
        else ArrangementOrdering.Alternative

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

/// Returns true if the track has an audio file set.
let hasAudioFile track = track.AudioFile |> Option.isSome

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
