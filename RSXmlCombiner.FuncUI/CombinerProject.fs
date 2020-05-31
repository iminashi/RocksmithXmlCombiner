namespace RSXmlCombiner.FuncUI

open System.Text.Json.Serialization

type CombinerProject = {
    Tracks : Track list
    CommonTones : CommonTones
    [<JsonIgnore>]
    /// Name and type of arrangements that must be found on every track.
    Templates : Templates
    CombinationTitle : string
    CoercePhrases : bool
    AddTrackNamesToLyrics : bool }

module CombinerProject =
    let empty = {
        Tracks = []
        Templates = Templates []
        CommonTones = Map.empty
        CombinationTitle = ""
        CoercePhrases = true
        AddTrackNamesToLyrics = true }

    let private updateTemplates (Templates currentTemplates) (arrangements : Arrangement list) =
        let newTemplates =
            arrangements
            |> List.filter (fun arr -> currentTemplates |> List.exists (fun temp -> arr.Name = temp.Name) |> not)
            |> List.map createTemplate
        Templates (currentTemplates @ newTemplates)

    let addMissingArrangements (Templates templates) (arrs : Arrangement list) =
        arrs 
        |> List.append
            (templates
            |> List.except (arrs |> Seq.map createTemplate))

    let private updateTrack templates track =
        let newArrangements = 
            addMissingArrangements templates track.Arrangements
            |> List.sortBy (fun a -> a.ArrangementType)

        { track with Arrangements = newArrangements }

    let private updateTracks (tracks : Track list) (templates : Templates) =
        tracks
        |> List.map (updateTrack templates)

    let private updateCommonTones commonTones (Templates templates) =
            let newCommonTones = 
                templates
                |> Seq.filter (fun t -> t.ArrangementType |> Types.isInstrumental )
                |> Seq.map (fun t -> t.Name, Array.create 5 "")
                |> Map.ofSeq

            // Preserve the current tone names
            commonTones
            |> Map.fold (fun commonTones name toneNames -> commonTones |> Map.add name toneNames) newCommonTones

    /// Adds a new track to the end of the track list of the project.
    let addTrack newTrack project =
        // Add any new arrangements in the track to the templates
        let templates = updateTemplates project.Templates newTrack.Arrangements

        // Update the common tone map from the new templates
        let commonTones = updateCommonTones project.CommonTones templates

        // Add any new templates to the existing tracks
        let tracks = updateTracks project.Tracks templates
        
        { project with Tracks = tracks @ [ newTrack ]; Templates = templates; CommonTones = commonTones }
