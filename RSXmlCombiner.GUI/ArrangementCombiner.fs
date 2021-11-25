module RSXmlCombiner.FuncUI.ArrangementCombiner

open System
open System.IO
open Rocksmith2014.XML
open XmlCombiners
open ArrangementType

let progress = Progress<int>()

let private increaseProgress () = (progress :> IProgress<int>).Report(1)

/// Combines the show light arrangements if all tracks have one set.
let private combineShowLights tracks arrIndex targetFolder =
    if tracks |> List.forall (fun t -> t.Arrangements[arrIndex].FileName.IsSome) then
        let combiner = ShowLightsCombiner()
        for track in tracks do
            let next = ShowLights.Load(track.Arrangements[arrIndex].FileName |> Option.get)
            combiner.AddNext(next, int track.SongLength, int track.TrimAmount)

        combiner.Save(Path.Combine(targetFolder, "Combined_Showlights_RS2.xml"))
        increaseProgress ()

/// Inserts the given title at the beginning of the given vocals arrangement.
let private addTitle (vocals: ResizeArray<Vocal>) (title: string) (startBeat: int<ms>) =
    let defaultDisplayTime = 3000<ms>

    let displayTime =
        let firstVocalsTime =
            if vocals.Count > 0 then
                Some(vocals[0].Time |> LanguagePrimitives.Int32WithMeasure<ms>)
            else
                None

        // Make sure that the title does not overlap with existing lyrics
        match firstVocalsTime with
        | Some time when time < startBeat + defaultDisplayTime ->
            time - startBeat - 50<ms>
        | _ ->
            defaultDisplayTime

    // Don't add the title if it will be displayed for less than a quarter of a second
    if displayTime >= 250<ms> then
        let words = title.Split(' ')
        let length = displayTime / words.Length
        for i = words.Length - 1 downto 0 do
            vocals.Insert(0, Vocal(int (startBeat + (length * i)), int length, words[i]))

/// Combines the vocals arrangements if at least one track has one, or addTitles is true.
let private combineVocals (tracks: Track list) arrIndex targetFolder addTitles =
    if addTitles || tracks |> List.exists (fun t -> t.Arrangements[arrIndex].FileName |> Option.isSome) then
        let combiner = VocalsCombiner()
        tracks
        |> Seq.indexed
        |> Seq.iter (fun (trackIndex, track) ->
            let next =
                track.Arrangements[arrIndex].FileName
                |> Option.map Vocals.Load
                |> Option.defaultWith (fun () -> ResizeArray())
            
            if addTitles then
                let title = sprintf "%i. %s+" (trackIndex + 1) track.Title
                addTitle next title track.TrimAmount

            combiner.AddNext(next, int track.SongLength, int track.TrimAmount))

        let targetFile = sprintf "Combined_%s_RS2.xml" tracks.Head.Arrangements[arrIndex].Name
        combiner.Save(Path.Combine(targetFolder, targetFile))
        increaseProgress ()

let private replaceToneNames (song: InstrumentalArrangement)
                             (toneReplacements: Map<string, int>)
                             (commonTones: string array) =
    let tones = song.Tones

    // Replace the tone names of the defined tones and the tone changes
    for kv in toneReplacements do
        // First one is the base tone
        let newToneName = commonTones[kv.Value + 1] 
        if tones.BaseToneName = kv.Key then tones.BaseToneName <- newToneName

        for i = 0 to tones.Names.Length - 1 do
            if tones.Names[i] = kv.Key then tones.Names[i] <- newToneName

        if not <| isNull tones.Changes then
            for tone in tones.Changes do
                if tone.Name = kv.Key then
                    tone.Name <- newToneName

    // Make sure that there are no duplicate names in the defined tones
    let uniqueTones =
        tones.Names
        |> Seq.filter String.notEmpty
        |> Set.ofSeq
        |> Set.toArray
    
    for i = 0 to tones.Names.Length - 1 do
        if i < uniqueTones.Length then
            tones.Names[i] <- uniqueTones[i]
        else
            tones.Names[i] <- null

/// Updates the metadata of the given instrumental arrangement file to match the given arrangement.
let private updateArrangementMetadata arr (combined: InstrumentalArrangement) =
    combined.MetaData.Arrangement <- arr.ArrangementType.ToString()

    let arrProps = combined.MetaData.ArrangementProperties
    arrProps.PathBass <- false
    arrProps.PathLead <- false
    arrProps.PathRhythm <- false
    arrProps.Represent <- false
    arrProps.BonusArrangement <- false

    match arr.ArrangementType with
    | ArrangementType.Lead ->
        arrProps.PathLead <- true
    | ArrangementType.Rhythm | ArrangementType.Combo ->
        arrProps.PathRhythm <- true
    | ArrangementType.Bass ->
        arrProps.PathBass <- true
    | _ ->
        ()

    match arr.Data with
    | Some { Ordering = ArrangementOrdering.Main } ->
        arrProps.Represent <- true
    | Some { Ordering = ArrangementOrdering.Bonus } ->
        arrProps.BonusArrangement <- true
    | _ ->
        ()

/// Combines the instrumental arrangements at the given index if all tracks have one set.
let private combineInstrumental (project: ProgramState) arrIndex targetFolder =
    let tracks = project.Tracks
    let commonTones =
        project.CommonTones
        |> Map.find tracks.Head.Arrangements[arrIndex].Name

    if tracks |> List.forall (fun t -> t.Arrangements[arrIndex].FileName.IsSome) then
        let combiner = InstrumentalCombiner()

        for i = 0 to tracks.Length - 1 do
            let arr = tracks[i].Arrangements[arrIndex]
            let arrData = arr.Data |> Option.get
            let next = InstrumentalArrangement.Load(arr.FileName |> Option.get)

            if i = 0 then
                next.Tones.BaseToneName <- commonTones[0]
            else if arrData.BaseToneIndex <> -1 then
                // First one is the base tone
                next.Tones.BaseToneName <- commonTones[arrData.BaseToneIndex + 1]

            if not arrData.ToneNames.IsEmpty then
                replaceToneNames next arrData.ToneReplacements commonTones

            let isLast = (i = tracks.Length - 1)
            combiner.AddNext(
                next,
                int tracks[i].SongLength,
                int tracks[i].TrimAmount,
                project.OnePhrasePerTrack,
                isLast)

            increaseProgress ()

        if String.notEmpty project.CombinationTitle then
            combiner.SetTitle(project.CombinationTitle)

        // Remove periods and replace spaces with underscores in the arrangement name
        let name = 
            tracks.Head.Arrangements[arrIndex].Name
            |> String.filter (fun c -> c <> '.')
            |> String.map (fun c -> if c = ' ' then '_' else c)

        // The metadata might be wrong if, for example,
        // a lead file was used as the first file of the combined rhythm arrangement
        updateArrangementMetadata tracks.Head.Arrangements[arrIndex] combiner.CombinedArrangement

        combiner.Save(Path.Combine(targetFolder, sprintf "Combined_%s_RS2.xml" name), project.CoercePhrases)

let private combineArrangement (project: ProgramState) arrIndex targetFolder =
    match project.Tracks.Head.Arrangements[arrIndex].ArrangementType with
    | Instrumental _ ->
        combineInstrumental project arrIndex targetFolder
    | Vocals _ ->
        combineVocals project.Tracks arrIndex targetFolder project.AddTrackNamesToLyrics
    | ArrangementType.ShowLights ->
        combineShowLights project.Tracks arrIndex targetFolder
    | _ ->
        failwith "Unknown arrangement type."

/// Combines all the arrangements in the given project.
let combine (project: ProgramState) targetFolder =
    let nArrangements = project.Tracks.Head.Arrangements.Length

    [ for i in 0 .. nArrangements - 1 -> async { combineArrangement project i targetFolder } ]
    |> Async.Parallel
    |> Async.Ignore
