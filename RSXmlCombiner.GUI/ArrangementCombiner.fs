module RSXmlCombiner.FuncUI.ArrangementCombiner

open System
open System.IO
open Rocksmith2014Xml
open XmlCombiners
open ArrangementType

let progress = Progress<int>()

let private increaseProgress () = (progress :> IProgress<int>).Report(1)

/// Combines the show light arrangements if all tracks have one set.
let private combineShowLights tracks arrIndex targetFolder =
    if tracks |> List.forall (fun t -> t.Arrangements.[arrIndex].FileName |> Option.isSome) then
        let combiner = ShowLightsCombiner()
        for track in tracks do
            let next = ShowLights.Load(track.Arrangements.[arrIndex].FileName |> Option.get)
            combiner.AddNext(next, track.SongLength, track.TrimAmount)

        combiner.Save(Path.Combine(targetFolder, "Combined_Showlights_RS2.xml"))
        increaseProgress ()

/// Inserts the given title at the beginning of the given vocals arrangement.
let private addTitle (vocals : Vocals) (title : string) (startBeat : int) =
    let defaultDisplayTime = 3000

    let displayTime =
        // Make sure that the title does not overlap with existing lyrics
        if vocals.Count > 0 && vocals.[0].Time < startBeat + defaultDisplayTime then
            vocals.[0].Time - startBeat - 50
        else
            defaultDisplayTime

    // Don't add the title if it will be displayed for less than a quarter of a second
    if displayTime >= 250 then
        let words = title.Split(' ')
        let length = displayTime / words.Length
        for i = words.Length - 1 downto 0 do
            vocals.Insert(0, Vocal(startBeat + (length * i), length, words.[i]))

/// Combines the vocals arrangements if at least one track has one.
let private combineVocals (tracks : Track list) arrIndex targetFolder addTitles =
    // TODO: Always generate lyrics file if addTitles is true?
    if tracks |> List.exists (fun t -> t.Arrangements.[arrIndex].FileName |> Option.isSome) then
        let combiner = VocalsCombiner()
        for (trackIndex, track) in tracks |> Seq.indexed do
            let next = 
                match track.Arrangements.[arrIndex].FileName with
                | Some fn -> Vocals.Load(fn)
                | None -> Vocals()
            
            if addTitles then
                let title = sprintf "%i. %s+" (trackIndex + 1) track.Title
                addTitle next title track.TrimAmount

            combiner.AddNext(next, track.SongLength, track.TrimAmount)

        combiner.Save(Path.Combine(targetFolder, sprintf "Combined_%s_RS2.xml" tracks.Head.Arrangements.[arrIndex].Name))
        increaseProgress ()

let private replaceToneNames (song : InstrumentalArrangement) (toneReplacements : Map<string, int>) (commonTones : string array) =
    // Replace the tone names of the defined tones and the tone changes
    for kv in toneReplacements do
        let newToneName = commonTones.[kv.Value + 1] // First one is the base tone
        if song.ToneBase = kv.Key then song.ToneBase <- newToneName
        if song.ToneA = kv.Key then song.ToneA <- newToneName
        if song.ToneB = kv.Key then song.ToneB <- newToneName
        if song.ToneC = kv.Key then song.ToneC <- newToneName
        if song.ToneD = kv.Key then song.ToneD <- newToneName

        if not (song.ToneChanges |> isNull) then
            for tone in song.ToneChanges do
                if tone.Name = kv.Key then
                    tone.Name <- newToneName

    // Make sure that there are no duplicate names in the defined tones
    let uniqueTones = Set.ofSeq (seq {
        if String.notEmpty song.ToneA then yield song.ToneA
        if String.notEmpty song.ToneB then yield song.ToneB
        if String.notEmpty song.ToneC then yield song.ToneC
        if String.notEmpty song.ToneD then yield song.ToneD
    })
    
    song.ToneA <- null
    song.ToneB <- null
    song.ToneC <- null
    song.ToneD <- null
    
    let songType = song.GetType()

    // Set the properties using reflection
    let folder toneChar tName =
        let toneProp = songType.GetProperty(sprintf "Tone%c" toneChar)
        toneProp.SetValue(song, tName)
        toneChar + char 1

    uniqueTones |> Set.fold folder 'A' |> ignore

/// Updates the metadata of the given instrumental arrangement file to match the given arrangement.
let private updateArrangementMetadata arr (combined : InstrumentalArrangement) =
    combined.Arrangement <- arr.ArrangementType.ToString()

    let arrProps = combined.ArrangementProperties
    arrProps.PathBass <- 0uy
    arrProps.PathLead <- 0uy
    arrProps.PathRhythm <- 0uy
    arrProps.Represent <- 0uy
    arrProps.BonusArrangement <- 0uy

    match arr.ArrangementType with
    | ArrangementType.Lead -> arrProps.PathLead <- 1uy
    | ArrangementType.Rhythm | ArrangementType.Combo -> arrProps.PathRhythm <- 1uy
    | ArrangementType.Bass -> arrProps.PathBass <- 1uy
    | _ -> ()

    let data = arr.Data |> Option.get
    match data.Ordering with
    | ArrangementOrdering.Main -> arrProps.Represent <- 1uy
    | ArrangementOrdering.Bonus -> arrProps.BonusArrangement <- 1uy
    | _ -> ()

/// Combines the instrumental arrangements at the given index if all tracks have one set.
let private combineInstrumental (project : ProgramState) arrIndex targetFolder =
    let tracks = project.Tracks
    let commonTones = project.CommonTones |> Map.find tracks.Head.Arrangements.[arrIndex].Name

    if tracks |> List.forall (fun t -> t.Arrangements.[arrIndex].FileName |> Option.isSome) then
        let combiner = InstrumentalCombiner()

        for i = 0 to tracks.Length - 1 do
            let arr = tracks.[i].Arrangements.[arrIndex]
            let arrData = arr.Data |> Option.get
            let next = InstrumentalArrangement.Load(arr.FileName |> Option.get)

            if i = 0 then
                next.ToneBase <- commonTones.[0]
            else if arrData.BaseToneIndex <> -1 then
                next.ToneBase <- commonTones.[arrData.BaseToneIndex + 1] // First one is the base tone

            if not arrData.ToneNames.IsEmpty then
                replaceToneNames next arrData.ToneReplacements commonTones

            combiner.AddNext(next, tracks.[i].TrimAmount, (i = tracks.Length - 1))
            increaseProgress ()

        if String.notEmpty project.CombinationTitle then
            combiner.SetTitle(project.CombinationTitle)

        // Remove periods and replace spaces with underscores in the arrangement name
        let name = 
            tracks.Head.Arrangements.[arrIndex].Name
            |> String.filter (fun c -> c <> '.')
            |> String.map (fun c -> if c = ' ' then '_' else c)

        // The metadata might be wrong if, for example, a lead file was used as the first file of the combined rhythm arrangement
        updateArrangementMetadata tracks.Head.Arrangements.[arrIndex] combiner.CombinedArrangement

        combiner.Save(Path.Combine(targetFolder, sprintf "Combined_%s_RS2.xml" name), project.CoercePhrases)

let combineArrangement (project : ProgramState) arrIndex targetFolder =
    match project.Tracks.Head.Arrangements.[arrIndex].ArrangementType with
    | Instrumental _ -> combineInstrumental project arrIndex targetFolder
    | Vocals _ -> combineVocals project.Tracks arrIndex targetFolder project.AddTrackNamesToLyrics
    | ArrangementType.ShowLights -> combineShowLights project.Tracks arrIndex targetFolder
    | _ -> failwith "Unknown arrangement type."

/// Combines all the arrangements in the given project.
let combine (project : ProgramState) targetFolder  =
    let nArrangements = project.Tracks.Head.Arrangements.Length
    let tasks = [ for i in 0..nArrangements - 1 -> async { do combineArrangement project i targetFolder } ]
    Async.Parallel tasks |> Async.Ignore
