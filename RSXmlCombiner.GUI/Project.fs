module RSXmlCombiner.FuncUI.Project

open System.IO
open System.Text.Json
open System.Text.Json.Serialization

type Dto =
   { Tracks : Track list
     CommonTones : CommonTones
     CombinationTitle : string
     CoercePhrases : bool
     AddTrackNamesToLyrics : bool }

let private fromProgramState (state : ProgramState) =
   { Tracks = state.Tracks
     CommonTones = state.CommonTones
     CombinationTitle = state.CombinationTitle
     CoercePhrases = state.CoercePhrases
     AddTrackNamesToLyrics = state.AddTrackNamesToLyrics }

/// Saves a project with the given filename.
let save fileName (state : ProgramState) =
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    options.WriteIndented <- true

    let project = fromProgramState state
    let json = JsonSerializer.Serialize(project, options)
    File.WriteAllText(fileName, json)

/// Loads a project from a file.
let load fileName =
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

    let json = File.ReadAllText(fileName)
    try
        JsonSerializer.Deserialize<Dto>(json, options) |> Ok
    with
    | :? JsonException as e -> Error(sprintf "Opening project failed: %s" e.Message)

/// Converts a project DTO into a program state record.
let toProgramState templates fileName statusMessage dto =
    { Tracks = dto.Tracks
      CommonTones = dto.CommonTones
      CombinationTitle = dto.CombinationTitle
      AddTrackNamesToLyrics = dto.AddTrackNamesToLyrics
      CoercePhrases = dto.CoercePhrases
      OnePhrasePerTrack = true
      Templates = templates
      StatusMessage = statusMessage
      ReplacementToneEditor = None
      ProjectViewActive = true
      AudioCombinerProgress = None
      ArrangementCombinerProgress = None
      OpenProjectFile = Some fileName
      SelectedFileTones = Map.empty }
