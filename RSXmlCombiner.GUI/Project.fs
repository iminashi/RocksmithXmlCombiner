module RSXmlCombiner.FuncUI.Project

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Rocksmith2014.XML

type Dto() =
    member val Tracks: Track list = List.empty with get, set
    member val CommonTones: CommonTones = Map.empty with get, set
    member val CombinationTitle: string = String.Empty with get, set
    member val CoercePhrases: bool = true with get, set
    member val OnePhrasePerTrack: bool = false with get, set
    member val AddTrackNamesToLyrics: bool = true with get, set

let private fromProgramState (state: ProgramState) =
    Dto(
        Tracks = state.Tracks,
        CommonTones = state.CommonTones,
        CombinationTitle = state.CombinationTitle,
        CoercePhrases = state.CoercePhrases,
        OnePhrasePerTrack = state.OnePhrasePerTrack,
        AddTrackNamesToLyrics = state.AddTrackNamesToLyrics
    )

/// Saves a project with the given filename.
let save fileName (state: ProgramState) = async {
    let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    options.Converters.Add(JsonFSharpConverter())

    let project = fromProgramState state
    use file = File.Create(fileName)
    do! JsonSerializer.SerializeAsync(file, project, options) |> Async.AwaitTask }

/// Loads a project from a file.
let load fileName = async {
    let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    options.Converters.Add(JsonFSharpConverter())

    use file = File.Open(fileName, FileMode.Open)
    return! JsonSerializer.DeserializeAsync<Dto>(file, options).AsTask() |> Async.AwaitTask }

/// Converts a project DTO into a program state record.
let toProgramState templates fileName statusMessage (dto: Dto) =
    { Tracks = dto.Tracks
      CommonTones = dto.CommonTones
      CombinationTitle = dto.CombinationTitle
      AddTrackNamesToLyrics = dto.AddTrackNamesToLyrics
      CoercePhrases = dto.CoercePhrases
      OnePhrasePerTrack = dto.OnePhrasePerTrack
      Templates = templates
      StatusMessage = statusMessage
      ReplacementToneEditor = None
      ProjectViewActive = true
      AudioCombinerProgress = None
      ArrangementCombinerProgress = None
      OpenProjectFile = Some fileName
      EditingTitleTrackIndex = -1
      SelectedFileTones = Map.empty }

/// Updates the arrangements in the project with tone names read from the XML files.
let updateToneNames (project: Dto) =
    let tracks =
        project.Tracks
        |> List.map (fun track ->
            let arrangements =
                track.Arrangements
                |> List.map (fun arr ->
                    match arr.FileName, arr.Data with
                    | Some fn, Some data when File.Exists(fn) ->
                        let toneInfo = InstrumentalArrangement.ReadToneNames(fn)

                        let newData =
                            let toneNames =
                                if isNull toneInfo.Changes then
                                    List.empty
                                else
                                    toneInfo.Names
                                    |> Seq.filter String.notEmpty
                                    |> Seq.toList

                            let toneReplacements =
                                (data.ToneReplacements, toneNames)
                                ||> List.fold (fun map replacement ->
                                    if map.ContainsKey replacement then
                                        map
                                    else
                                        map.Add(replacement, -1))

                            { data with ToneNames = toneNames; ToneReplacements = toneReplacements }

                        { arr with Data = Some newData }
                    | _ ->
                        arr)
            { track with Arrangements = arrangements })

    project.Tracks <- tracks

/// Returns the names of audio files and arrangement files that do not exist anymore.
let getMissingFiles (project: Dto) =
    (([], []), project.Tracks)
    ||> List.fold (fun state track ->
        let missingAudioFiles =
            match track.AudioFile with
            | Some file when not <| File.Exists(file) ->
                file :: (fst state)
            | _ ->
                fst state

        let missingArrangementFiles =
            ([], track.Arrangements)
            ||> List.fold (fun missing arr -> 
                match arr.FileName with
                | Some file when not <| File.Exists(file) ->
                    file :: missing
                | _ ->
                    missing)

        missingAudioFiles, (snd state) @ missingArrangementFiles)
