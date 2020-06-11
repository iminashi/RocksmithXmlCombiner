namespace RSXmlCombiner.FuncUI

open System

type ProgramState = {
    Tracks : Track list
    CommonTones : CommonTones
    /// Names and types of arrangements that must be found on every track.
    Templates : Templates
    CombinationTitle : string
    CoercePhrases : bool
    AddTrackNamesToLyrics : bool
    StatusMessage : string
    ReplacementToneEditor : (int * int) option
    ProjectViewActive : bool
    OpenProjectFile : string option }

module ProgramState =
    let init = {
        Tracks = []
        Templates = Templates []
        CommonTones = Map.empty
        CombinationTitle = ""
        CoercePhrases = true
        AddTrackNamesToLyrics = true
        StatusMessage = ""
        ReplacementToneEditor = None
        ProjectViewActive = true
        OpenProjectFile = None }

    let private updateTemplates (arrangements : Arrangement list) (Templates currentTemplates) =
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

    let private updateTracks (templates : Templates) = List.map (updateTrack templates)

    let private updateCommonTones (Templates templates) commonTones =
            let newCommonTones = 
                templates
                |> Seq.filter (fun t -> t.ArrangementType |> Types.isInstrumental )
                |> Seq.map (fun t -> t.Name, Array.create 5 "")
                |> Map.ofSeq

            // Preserve the current tone names
            commonTones
            |> Map.fold (fun commonTones arrName toneNames -> commonTones |> Map.add arrName toneNames) newCommonTones

    /// Adds a new track to the end of the track list of the project.
    let addTrack project newTrack =
        // Add any new arrangements in the track to the templates
        let templates = project.Templates |> updateTemplates newTrack.Arrangements

        // Update the common tone map from the new templates
        let commonTones = project.CommonTones |> updateCommonTones templates

        // Add any new templates to the existing tracks
        let tracks = project.Tracks |> updateTracks templates
        
        { project with Tracks = tracks @ [ newTrack ]; Templates = templates; CommonTones = commonTones }

    let getReplacementToneNames arrName commonTones =
        match Map.tryFind arrName commonTones with
        | Some names ->
            match names |> Array.tryFindIndex String.IsNullOrEmpty with
            // Exclude the first one which is the base tone for the combined arrangement
            | Some firstEmptyIndex -> names.[1..(firstEmptyIndex - 1)]
            | None -> names.[1..]
        | None -> [||]
