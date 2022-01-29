namespace RSXmlCombiner.FuncUI

open System

type ReplacementToneEditor =
    { TrackIndex: int
      ArrangementIndex: int }

type ProgramState =
    { Tracks: Track list
      CommonTones: CommonTones
      /// Names and types of arrangements that must be found on every track.
      Templates: Templates
      CombinationTitle: string
      CoercePhrases: bool
      OnePhrasePerTrack: bool
      AddTrackNamesToLyrics: bool
      StatusMessage: string
      ReplacementToneEditor: ReplacementToneEditor option
      ProjectViewActive: bool
      AudioCombinerProgress: float option
      ArrangementCombinerProgress: (int * int) option
      OpenProjectFile: string option
      EditingTitleTrackIndex: int
      SelectedFileTones: Map<string, string> }

module ProgramState =
    let init =
        { Tracks = []
          Templates = Templates []
          CommonTones = Map.empty
          CombinationTitle = ""
          CoercePhrases = true
          OnePhrasePerTrack = false
          AddTrackNamesToLyrics = true
          StatusMessage = ""
          ReplacementToneEditor = None
          ProjectViewActive = true
          AudioCombinerProgress = None
          ArrangementCombinerProgress = None
          OpenProjectFile = None
          EditingTitleTrackIndex = -1
          SelectedFileTones = Map.empty }

    let private updateTemplates (arrangements: Arrangement list) (Templates currentTemplates) =
        let newTemplates =
            arrangements
            |> List.filter (fun arr ->
                currentTemplates
                |> List.exists (fun temp -> arr.Name = temp.Name)
                |> not)
            |> List.map createTemplate

        Templates (currentTemplates @ newTemplates)

    let addMissingArrangements (Templates templates) (arrangements: Arrangement list) =
        let missing = 
            templates
            |> List.filter (fun temp ->
                arrangements
                |> List.exists (fun arr -> arr.Name = temp.Name)
                |> not)

        arrangements @ missing

    let private updateTrack templates track =
        let newArrangements = 
            addMissingArrangements templates track.Arrangements
            |> List.sortBy arrangementSort

        { track with Arrangements = newArrangements }

    let updateTracks templates = List.map (updateTrack templates)

    let private updateCommonTones (Templates templates) (commonTones: CommonTones): CommonTones =
        let newCommonTones = 
            templates
            |> Seq.filter (fun t -> t.ArrangementType |> ArrangementType.isInstrumental)
            |> Seq.map (fun t -> t.Name, Array.create 5 "")
            |> Map.ofSeq

        // Preserve the current tone names
        (newCommonTones, commonTones)
        ||> Map.fold (fun state arrName toneNames -> state |> Map.add arrName toneNames)

    /// Adds a new track to the end of the track list of the project.
    let addTrack project newTrack =
        // Add any new arrangements in the track to the templates
        let templates = project.Templates |> updateTemplates newTrack.Arrangements

        // Update the common tone map from the new templates
        let commonTones = project.CommonTones |> updateCommonTones templates

        // Add any new templates to the existing tracks
        let tracks = project.Tracks |> updateTracks templates
        
        { project with
            Tracks = tracks @ [ newTrack ]
            Templates = templates
            CommonTones = commonTones }

    let getReplacementToneNames arrName (commonTones: CommonTones) =
        match Map.tryFind arrName commonTones with
        | Some names ->
            match names |> Array.tryFindIndex String.IsNullOrEmpty with
            // Exclude the first one, which is the base tone for the combined arrangement
            | Some firstEmptyIndex ->
                names[1..(firstEmptyIndex - 1)]
            | None ->
                names[1..]
        | None ->
            Array.empty
