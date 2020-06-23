module RSXmlCombiner.FuncUI.Audio

open NAudio.Wave
open NAudio.Wave.SampleProviders
open NAudio.Vorbis
open System

/// Returns a wave stream for wave and vorbis files.
let private getWaveStream (fileName : string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then new AudioFileReader(fileName) :> WaveStream
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> WaveStream
    else failwith "The audio file must be a wav or ogg file."

/// Returns a sample provider for wave and vorbis files.
let getSampleProvider (fileName : string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then new AudioFileReader(fileName) :> ISampleProvider
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> ISampleProvider
    else failwith "The audio file must be a wav or ogg file."

/// Returns the sample rate of the given wave or vorbis file.
let getSampleRate (fileName : string) =
    use reader = getWaveStream fileName
    reader.WaveFormat.SampleRate

/// Returns the total length in milliseconds of the given wave or vorbis file.
let getLength (fileName : string) =
    use reader = getWaveStream fileName
    int64 reader.TotalTime.TotalMilliseconds
       
/// If the sample rate of the sample provider is different than the given sample rate, it will be resampled.
let private resampleIfNeeded targetRate (reader : ISampleProvider) =
    if reader.WaveFormat.SampleRate <> targetRate then
        WdlResamplingSampleProvider(reader, targetRate) :> ISampleProvider
    else
        reader

/// Returns a sample provider for the given wave or vorbis files, resampled to the given sample rate if necessary.
let getSampleProviderWithRate sampleRate = getSampleProvider >> resampleIfNeeded sampleRate

/// Trims the given amount of time in milliseconds from the given sample provider.
let trimStart amount (file : ISampleProvider) =
    amount |> float |> TimeSpan.FromMilliseconds |> file.Skip

/// Concatenates the sample providers into a 16-bit wave file with the given name.
let concatenate targetFile (files : ISampleProvider seq) =
    let combined = ConcatenatingSampleProvider(files |> Array.ofSeq)
    WaveFileWriter.CreateWaveFile16(targetFile, combined)

/// Offsets the sample provider by the given amounts.
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
