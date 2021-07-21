module RSXmlCombiner.FuncUI.Project

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization

type Dto() =
   member val Tracks : Track list = List.empty with get, set
   member val CommonTones : CommonTones = Map.empty with get, set
   member val CombinationTitle : string = String.Empty with get, set
   member val CoercePhrases : bool = true with get, set
   member val OnePhrasePerTrack : bool = false with get, set
   member val AddTrackNamesToLyrics : bool = true with get, set

let private fromProgramState (state: ProgramState) =
    Dto(Tracks = state.Tracks,
        CommonTones = state.CommonTones,
        CombinationTitle = state.CombinationTitle,
        CoercePhrases = state.CoercePhrases,
        OnePhrasePerTrack = state.OnePhrasePerTrack,
        AddTrackNamesToLyrics = state.AddTrackNamesToLyrics)

/// Saves a project with the given filename.
let save fileName (state: ProgramState) = async {
    let options = JsonSerializerOptions(WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    options.Converters.Add(JsonFSharpConverter())

    let project = fromProgramState state
    use file = File.Create fileName
    do! JsonSerializer.SerializeAsync(file, project, options) |> Async.AwaitTask }

/// Loads a project from a file.
let load fileName =
    let options = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
    options.Converters.Add(JsonFSharpConverter())

    let json = File.ReadAllText(fileName)
    try
        JsonSerializer.Deserialize<Dto>(json, options) |> Ok
    with
    | :? JsonException as e -> Error($"Opening project failed: {e.Message}")

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
      SelectedFileTones = Map.empty }
