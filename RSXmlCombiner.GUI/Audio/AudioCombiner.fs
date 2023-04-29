module RSXmlCombiner.FuncUI.AudioCombiner

open System

let progress = Progress<float>()

let [<Literal>] private TargetSampleRate = 48000

let private rand = Random()

type AudioCombinerData =
    { AudioReader: Audio.AudioReader
      TrimAmount: int<ms> }

type PreviewCreationData =
    { AudioReader: Audio.AudioReader
      SongLength: int<ms> }

/// Combines the audio files of the given tracks into the target file.
let combineWithResampling (targetFile: string) (data: AudioCombinerData list) =
    try
        data
        |> Seq.mapi (fun i data ->
            let sampler =
                Audio.resampleIfNeeded TargetSampleRate data.AudioReader.SampleProvider

            match i with
            | 0 -> sampler
            | _ -> sampler |> Audio.trimStart data.TrimAmount)
        |> Audio.concatenate targetFile
    finally
        data
        |> List.iter (fun x -> (x.AudioReader :> IDisposable).Dispose())

let private randomizeStartOffset audioLength =
    let startTime, endTime =
        if audioLength <= 50_000<ms> then
            0, int (audioLength - 15_000<ms>)
        else
            10_000, int (audioLength - 30_000<ms>)

    rand.Next(startTime, endTime)
    |> float
    |> TimeSpan.FromMilliseconds

let private createSampleProviderWithRandomOffset take data =
    let randomStartOffset = randomizeStartOffset data.SongLength

    data.AudioReader.SampleProvider
    |> Audio.resampleIfNeeded TargetSampleRate
    |> Audio.offset randomStartOffset (TimeSpan.FromMilliseconds(float take))

/// Creates a preview audio file from up to four randomly selected files.
let createPreview (targetFile: string) (data: PreviewCreationData list) =
    try
        let fadeBetweenSections = 400<ms>
        let numFiles = min 4 data.Length
        let sectionLength = LanguagePrimitives.Int64WithMeasure<ms>(int64 (28.0 / float numFiles * 1000.0))

        data
        |> Seq.sortBy (fun _ -> rand.Next())
        |> Seq.take numFiles
        |> Seq.mapi (fun i data ->
            let fadeIn = if i = 0 then 2500<ms> else fadeBetweenSections
            let fadeOut = if i = numFiles - 1 then 3000<ms> else fadeBetweenSections

            createSampleProviderWithRandomOffset sectionLength data
            |> Audio.fade fadeIn fadeOut sectionLength)
        |> Audio.concatenate targetFile
    finally
        data
        |> List.iter (fun x -> (x.AudioReader :> IDisposable).Dispose())
