namespace RSXmlCombiner.FuncUI

module Project =
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization

    type Dto = {
        Tracks : Track list
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

    let save fileName (state : ProgramState) =
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonFSharpConverter())
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
        options.WriteIndented <- true

        let project = fromProgramState state
        let json = JsonSerializer.Serialize(project, options)
        File.WriteAllText(fileName, json)

    let load fileName =
        let options = JsonSerializerOptions()
        options.Converters.Add(JsonFSharpConverter())
        options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase

        let json = File.ReadAllText(fileName)
        try
            JsonSerializer.Deserialize<Dto>(json, options) |> Ok
        with
        | :? JsonException as e -> Error(sprintf "Opening project failed: %s" e.Message)