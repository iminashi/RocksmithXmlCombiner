module RSXmlCombiner.FuncUI.AudioCombiner

open System
open System.IO
open NAudio.Wave

let progress = Progress<float>()

/// Copies from the reader to the writer with the given amount in milliseconds trimmed from the start.
let private addTrimmed (reader : WaveStream) (writer : WaveFileWriter) (trim : int<ms>) =
    let bytesPerMillisecond = float reader.WaveFormat.AverageBytesPerSecond / 1000.0
    let startPos = int (float trim * bytesPerMillisecond)
    let trimStart = startPos - startPos % reader.WaveFormat.BlockAlign

    reader.Position <- int64 trimStart

    async {
        do! reader.CopyToAsync writer |> Async.AwaitTask
        do! writer.FlushAsync() |> Async.AwaitTask
    }

/// Combines the audio files of the given tracks into the target file.
let combineAudioFiles (tracks : Track list) (targetFile : string) =
    async {
        try
            use first = Audio.get16BitWaveStream tracks.Head.AudioFile
            use writer = new WaveFileWriter(targetFile, first.WaveFormat)
            do! addTrimmed first writer 0<ms>

            for i, t in tracks.Tail |> List.indexed do
                (progress :> IProgress<float>).Report(float(i + 1) / (float tracks.Length))

                use fileReader = Audio.get16BitWaveStream t.AudioFile
                if not <| fileReader.WaveFormat.Equals(writer.WaveFormat) then
                    let error =
                        let targetFormat = writer.WaveFormat
                        let currFormat = fileReader.WaveFormat
                        sprintf "Different wave format. Target format: %iHz, %i bits, %i channels. File %s: %iHz, %i bits, %i channels."
                            targetFormat.SampleRate targetFormat.BitsPerSample targetFormat.Channels
                            (t.AudioFile |> Option.get |> Path.GetFileName)
                            currFormat.SampleRate currFormat.BitsPerSample currFormat.Channels
                    failwith error
                do! addTrimmed fileReader writer t.TrimAmount

            return sprintf "Audio files combined as %s" targetFile
        with
        e -> return sprintf "Error: %s" e.Message
    }

/// Combines the audio files of the given tracks into the target file.
let combineWithResampling (tracks : Track list) (targetFile : string) =
    async {
        try
            let firstFile = tracks.Head.AudioFile |> Option.get
            let head = Audio.getSampleProviderWithRate 48000 firstFile

            let tail =
                tracks.Tail
                |> Seq.map (fun t ->
                    t.AudioFile
                    |> Option.get
                    |> Audio.getSampleProviderWithRate 48000 
                    |> Audio.trimStart t.TrimAmount)

            Audio.concatenate targetFile (seq { head; yield! tail })

            return sprintf "Audio files combined as %s" targetFile
        with
        e -> return sprintf "Error: %s" e.Message
    }

let private createSampleProviderWithRandomOffset take (fileName, audioLength) =
    let randomOffset =
        let rand = Random()
        let startOffset =
            if audioLength <= 50_000<ms> then rand.Next(0, int(audioLength - 15_000<ms>))
            else rand.Next(10_000, int (audioLength - 30_000<ms>))
        startOffset |> float |> TimeSpan.FromMilliseconds

    Audio.getSampleProviderWithRate 48000 fileName
    |> Audio.offset randomOffset (TimeSpan.FromMilliseconds(float take))

/// Creates a preview audio file from up to four randomly selected files.
let createPreview (tracks : Track list) (targetFile : string) =
    let rand = Random()
    let fadeBetweenSections = 400<ms>
    let numFiles = min 4 tracks.Length
    let sectionLength = LanguagePrimitives.Int64WithMeasure<ms> (int64 (28.0 / float numFiles * 1000.0))

    tracks
    |> Seq.choose (fun t -> t.AudioFile |> Option.map (fun f -> f, t.SongLength))
    |> Seq.sortBy (fun _ -> rand.Next())
    |> Seq.take numFiles
    |> Seq.map (createSampleProviderWithRandomOffset sectionLength)
    |> Seq.mapi (fun i s ->
        let fadeIn = if i = 0 then 2500<ms> else fadeBetweenSections
        let fadeOut = if i = numFiles - 1 then 3000<ms> else fadeBetweenSections
        Audio.fade fadeIn fadeOut sectionLength s)
    |> Audio.concatenate targetFile
