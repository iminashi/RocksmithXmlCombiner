module RSXmlCombiner.FuncUI.Audio

open NAudio.Wave
open NAudio.Wave.SampleProviders
open NAudio.Vorbis
open System

let private getWaveStream (fileName : string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then new AudioFileReader(fileName) :> WaveStream
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> WaveStream
    else failwith "The audio file must be a wav or ogg file."

let getSampleProvider (fileName : string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then new AudioFileReader(fileName) :> ISampleProvider
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> ISampleProvider
    else failwith "The audio file must be a wav or ogg file."

let getSampleRate (fileName : string) =
    use reader = getWaveStream fileName
    reader.WaveFormat.SampleRate

let getLength (fileName : string) =
    use reader = getWaveStream fileName
    int64 reader.TotalTime.TotalMilliseconds
        
let resampleIfNeeded targetRate (reader : ISampleProvider) =
    if reader.WaveFormat.SampleRate <> targetRate then
        WdlResamplingSampleProvider(reader, targetRate) :> ISampleProvider
    else
        reader

let getSampleProviderWithRate sampleRate = getSampleProvider >> resampleIfNeeded sampleRate

let trimStart amount (file : ISampleProvider) =
    file.Skip(TimeSpan.FromMilliseconds(float amount))

let concatenate targetFile (files : ISampleProvider seq) =
    let combined = ConcatenatingSampleProvider(files |> Array.ofSeq)
    WaveFileWriter.CreateWaveFile16(targetFile, combined)

let offset skip take (sampleProvider : ISampleProvider) =
    OffsetSampleProvider(sampleProvider, SkipOver = skip, Take = take)

let get16BitWaveStream (fileName : string option) =
    let reader =
        match fileName with
        | Some fn -> getWaveStream fn
        | None -> failwith "No audio file set!"

    if reader.WaveFormat.BitsPerSample = 32 then
        new Wave32To16Stream(reader) :> WaveStream
    else
        reader
