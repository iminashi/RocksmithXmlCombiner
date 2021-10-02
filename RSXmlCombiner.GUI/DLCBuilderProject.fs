module RSXmlCombiner.FuncUI.DLCBuilderProject

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

type AudioFile = { Path: string; Volume: float }

type SortableString = { Value: string; SortValue: string }

type ArrangementName =
    | Lead = 0
    | Combo = 1
    | Rhythm = 2
    | Bass = 3

type RouteMask =
    | None = 0
    | Lead = 1
    | Rhythm = 2
    | Any = 3
    | Bass = 4

type ArrangementPriority =
    | Main = 0
    | Alternative = 1
    | Bonus = 2

type Instrumental =
    { XML: string
      Name: ArrangementName
      RouteMask: RouteMask
      Priority: ArrangementPriority
      ScrollSpeed: float
      BassPicked: bool
      Tuning: int16 array
      TuningPitch: float
      BaseTone: string
      Tones: string list
      CustomAudio: AudioFile option
      MasterID: int
      PersistentID: Guid }

type Vocals =
    { XML: string
      Japanese: bool
      CustomFont: string option
      MasterID: int
      PersistentID: Guid }

type Showlights =
    { XML: string }

type Arrangement =
    | Instrumental of Instrumental
    | Vocals of Vocals
    | Showlights of Showlights

type DLCProject =
    { Version: string
      DLCKey: string
      ArtistName: SortableString
      JapaneseArtistName: string option
      JapaneseTitle: string option
      Title: SortableString
      AlbumName: SortableString
      Year: int
      AlbumArtFile: string
      AudioFile: AudioFile
      AudioPreviewFile: AudioFile
      Arrangements: Arrangement list
      (*AudioPreviewStartTime: float option
      PitchShift: int16 option
      Tones: Tone list*) }

let private loadProject (fileName: string) = async {
    let options = JsonSerializerOptions(IgnoreNullValues = true)
    options.Converters.Add(JsonFSharpConverter())
    use file = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan ||| FileOptions.Asynchronous)
    return! JsonSerializer.DeserializeAsync<DLCProject>(file, options).AsTask() |> Async.AwaitTask }

let private toAbsolutePath (directory: string) (path: string) =
    if Path.IsPathFullyQualified(path) then
        path
    else
        Path.Combine(directory, path)

let private convertArrangementType (inst: Instrumental) =
    match inst.RouteMask with
    | RouteMask.Lead ->
        ArrangementType.Lead
    | RouteMask.Rhythm ->
        ArrangementType.Rhythm
    | RouteMask.Bass ->
        ArrangementType.Bass
    | _ ->
        ArrangementType.Lead

let private tryGetTypeAndXmlFile = function
    | Instrumental ({ Priority = ArrangementPriority.Main } as inst) ->
        Some(convertArrangementType inst, inst.XML)
    | Showlights sl ->
        Some(ArrangementType.ShowLights, sl.XML)
    | Vocals v ->
        let arrType = if v.Japanese then ArrangementType.JVocals else ArrangementType.Vocals
        Some(arrType, v.XML)
    | _ ->
        None

/// Imports arrangements from a DLC Builder project.
let import (fileName: string) = async {
    let projectDirectory = Path.GetDirectoryName(fileName)
    let! project = loadProject fileName
    let audioFile = toAbsolutePath projectDirectory project.AudioFile.Path
    let title = project.Title.Value

    let arrangementMap =
        (Map.empty, project.Arrangements)
        ||> List.fold (fun map arr ->
            (map, tryGetTypeAndXmlFile arr)
            ||> Option.fold (fun map (arrType, xml) ->
                map.Add(arrType, toAbsolutePath projectDirectory xml)))

    return arrangementMap, title, audioFile }
