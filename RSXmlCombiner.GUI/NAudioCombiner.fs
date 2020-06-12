namespace RSXmlCombiner.FuncUI

module NAudioCombiner =
    open System
    open System.IO
    open NAudio.Wave
    open NAudio.Wave.SampleProviders

    /// Copies from the reader to the writer with the given amount in milliseconds trimmed from the start.
    let addTrimmed (reader : WaveFileReader) (writer : WaveFileWriter) (trim : int) =
        let bytesPerMillisecond = float reader.WaveFormat.AverageBytesPerSecond / 1000.0
        let startPos = int (float trim * bytesPerMillisecond)
        let trimStart = startPos - startPos % reader.WaveFormat.BlockAlign

        reader.Position <- int64 trimStart
        reader.CopyTo writer

        writer.Flush()

    /// Combines the audio files of the given tracks into the target file.
    let combineAudioFiles (tracks : Track list) (targetFile : string) =
        try
            use formatGetter = new WaveFileReader(tracks.Head.AudioFile |> Option.get)
            use writer = new WaveFileWriter(targetFile, formatGetter.WaveFormat)
            for t in tracks do
                use fileReader = new WaveFileReader(t.AudioFile |> Option.get)
                if not <| fileReader.WaveFormat.Equals(writer.WaveFormat) then
                    let error =
                        let targetFormat = writer.WaveFormat
                        let currFormat = fileReader.WaveFormat
                        sprintf "Different wave format. Target format: %iHz, %i bits, %i channels. File %s: %iHz, %i bits, %i channels."
                            targetFormat.SampleRate targetFormat.BitsPerSample targetFormat.Channels
                            (t.AudioFile |> Option.get |> Path.GetFileName)
                            currFormat.SampleRate currFormat.BitsPerSample currFormat.Channels
                    failwith error
                let trimAmount = if t = tracks.Head then 0 else t.TrimAmount
                addTrimmed fileReader writer trimAmount

            sprintf "Audio files combined as %s" targetFile
        with
        e -> sprintf "Error: %s" e.Message