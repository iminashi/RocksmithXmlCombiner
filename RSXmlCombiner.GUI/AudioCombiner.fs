module RSXmlCombiner.FuncUI.AudioCombiner

open System

let progress = Progress<float>()

let [<Literal>] private TargetSampleRate = 48000

/// Combines the audio files of the given tracks into the target file.
let combineWithResampling (tracks: Track list) (targetFile: string) =
    try
        tracks
        |> Seq.mapi (fun i track ->
            let sampler =
                track.AudioFile
                |> Option.get
                |> Audio.getSampleProviderWithRate TargetSampleRate
            if i = 0 then sampler else sampler |> Audio.trimStart track.TrimAmount)
        |> Audio.concatenate targetFile
  
        $"Audio files combined as {targetFile}"
    with e ->
        $"Error: {e.Message}"

let private createSampleProviderWithRandomOffset take (fileName, audioLength) =
    let randomOffset =
        let rand = Random()
        let startOffset =
            if audioLength <= 50_000<ms> then rand.Next(0, int(audioLength - 15_000<ms>))
            else rand.Next(10_000, int (audioLength - 30_000<ms>))
        startOffset |> float |> TimeSpan.FromMilliseconds

    Audio.getSampleProviderWithRate TargetSampleRate fileName
    |> Audio.offset randomOffset (TimeSpan.FromMilliseconds(float take))

/// Creates a preview audio file from up to four randomly selected files.
let createPreview (tracks: Track list) (targetFile: string) =
    let rand = Random()
    let fadeBetweenSections = 400<ms>
    let numFiles = min 4 tracks.Length
    let sectionLength = LanguagePrimitives.Int64WithMeasure<ms> (int64 (28.0 / float numFiles * 1000.0))

    tracks
    |> Seq.choose (fun track -> track.AudioFile |> Option.map (fun file -> file, track.SongLength))
    |> Seq.sortBy (fun _ -> rand.Next())
    |> Seq.take numFiles
    |> Seq.map (createSampleProviderWithRandomOffset sectionLength)
    |> Seq.mapi (fun i s ->
        let fadeIn = if i = 0 then 2500<ms> else fadeBetweenSections
        let fadeOut = if i = numFiles - 1 then 3000<ms> else fadeBetweenSections
        Audio.fade fadeIn fadeOut sectionLength s)
    |> Audio.concatenate targetFile
