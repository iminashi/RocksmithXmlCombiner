module RSXmlCombiner.FuncUI.Audio

open NAudio.Wave
open NAudio.Wave.SampleProviders
open NAudio.Vorbis
open System

let private (|Extension|_|) extension (fileName: string) =
    if fileName.EndsWith($".%s{extension}", StringComparison.OrdinalIgnoreCase) then
        Some()
    else
        None

type AudioReader(stream: WaveStream, provider: ISampleProvider) =
    new(input: WaveFileReader) =
        new AudioReader(input :> WaveStream, input.ToSampleProvider())

    new(input: VorbisWaveReader) =
        new AudioReader(input :> WaveStream, input :> ISampleProvider)

    member _.Stream = stream
    member _.SampleProvider = provider

    member _.Position = stream.Position
    member _.Length = stream.Length

    /// Returns an audio reader for the given filename.
    static member Create(fileName: string) =
        match fileName with
        | Extension "wav" ->
            new AudioReader(new WaveFileReader(fileName))
        | Extension "ogg" ->
            new AudioReader(new VorbisWaveReader(fileName))
        | _ ->
            raise <| NotSupportedException "Only vorbis and wave files are supported."

    interface IDisposable with
        member _.Dispose() = stream.Dispose()

/// Returns the sample rate of the given wave or vorbis file.
let getSampleRate (fileName: string) =
    use reader = AudioReader.Create fileName
    reader.Stream.WaveFormat.SampleRate

/// Returns the total length in milliseconds of the given wave or vorbis file.
let getLength (fileName: string) =
    use reader = AudioReader.Create fileName
    int (Math.Round(reader.Stream.TotalTime.TotalMilliseconds, MidpointRounding.AwayFromZero)) * 1<ms>

/// If the sample rate of the sample provider is different than the given sample rate, it will be resampled.
let resampleIfNeeded targetRate (reader: ISampleProvider) =
    if reader.WaveFormat.SampleRate <> targetRate then
        WdlResamplingSampleProvider(reader, targetRate) :> ISampleProvider
    else
        reader

/// Trims the given amount of time in milliseconds from the given sample provider.
let trimStart (amount: int<ms>) (file: ISampleProvider) =
    amount |> float |> TimeSpan.FromMilliseconds |> file.Skip

/// Concatenates the sample providers into a 16-bit wave file with the given name.
let concatenate targetFile (files: ISampleProvider seq) =
    let combined = ConcatenatingSampleProvider files
    WaveFileWriter.CreateWaveFile16(targetFile, combined)

/// Offsets the sample provider by the given amounts.
let offset skip take sampleProvider =
    OffsetSampleProvider(sampleProvider, SkipOver = skip, Take = take)

/// Adds a fade-in and fade-out to the sample provider.
let fade fadeIn fadeOut audioLength sampleProvider =
    AudioFader(sampleProvider, fadeIn, fadeOut, audioLength) :> ISampleProvider
