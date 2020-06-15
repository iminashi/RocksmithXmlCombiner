module RSXmlCombiner.FuncUI.AudioCombiner

open System
open System.IO
open NAudio.Wave
open NAudio.Vorbis
open NAudio.Wave.SampleProviders

let progress = Progress<float>()

/// Copies from the reader to the writer with the given amount in milliseconds trimmed from the start.
let private addTrimmed (reader : WaveStream) (writer : WaveFileWriter) (trim : int) =
    let bytesPerMillisecond = float reader.WaveFormat.AverageBytesPerSecond / 1000.0
    let startPos = int (float trim * bytesPerMillisecond)
    let trimStart = startPos - startPos % reader.WaveFormat.BlockAlign

    reader.Position <- int64 trimStart

    async {
        do! reader.CopyToAsync writer |> Async.AwaitTask
        do! writer.FlushAsync() |> Async.AwaitTask
    }

let private getReader (fileName : string option) =
    let reader =
        match fileName with
        | Some fn when fn.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) -> new WaveFileReader(fn) :> WaveStream
        | Some fn when fn.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) -> new VorbisWaveReader(fn) :> WaveStream
        | Some _ -> failwith "The audio file must be a wav or ogg file!"
        | None -> failwith "No audio file set!"
    if reader.WaveFormat.BitsPerSample = 32 then
        new Wave32To16Stream(reader) :> WaveStream
    else
        reader

/// Combines the audio files of the given tracks into the target file.
let combineAudioFiles (tracks : Track list) (targetFile : string) =
    async {
        try
            use first = getReader tracks.Head.AudioFile
            use writer = new WaveFileWriter(targetFile, first.WaveFormat)
            do! addTrimmed first writer 0

            for i, t in tracks.Tail |> List.indexed do
                (progress :> IProgress<float>).Report(float(i + 1) / (float tracks.Length))

                use fileReader = getReader t.AudioFile
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
