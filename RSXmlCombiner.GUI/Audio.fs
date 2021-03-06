﻿module RSXmlCombiner.FuncUI.Audio

open NAudio.Wave
open NAudio.Wave.SampleProviders
open NAudio.Vorbis
open System

/// Returns a wave stream for wave and vorbis files.
let private getWaveStream (fileName: string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then new WaveFileReader(fileName) :> WaveStream
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> WaveStream
    else invalidOp "The audio file must be a wav or ogg file."

/// Returns a sample provider for wave and vorbis files.
let getSampleProvider (fileName: string)  =
    if fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) then (new WaveFileReader(fileName)).ToSampleProvider()
    elif fileName.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) then new VorbisWaveReader(fileName) :> ISampleProvider
    else invalidOp "The audio file must be a wav or ogg file."

/// Returns the sample rate of the given wave or vorbis file.
let getSampleRate (fileName: string) =
    use reader = getWaveStream fileName
    reader.WaveFormat.SampleRate

/// Returns the total length in milliseconds of the given wave or vorbis file.
let getLength (fileName: string) =
    use reader = getWaveStream fileName
    int (Math.Round(reader.TotalTime.TotalMilliseconds, MidpointRounding.AwayFromZero)) * 1<ms>
       
/// If the sample rate of the sample provider is different than the given sample rate, it will be resampled.
let private resampleIfNeeded targetRate (reader: ISampleProvider) =
    if reader.WaveFormat.SampleRate <> targetRate then
        WdlResamplingSampleProvider(reader, targetRate) :> ISampleProvider
    else
        reader

/// Returns a sample provider for the given wave or vorbis files, resampled to the given sample rate if necessary.
let getSampleProviderWithRate sampleRate = getSampleProvider >> resampleIfNeeded sampleRate

/// Trims the given amount of time in milliseconds from the given sample provider.
let trimStart (amount: int<ms>) (file: ISampleProvider) =
    amount |> float |> TimeSpan.FromMilliseconds |> file.Skip

/// Concatenates the sample providers into a 16-bit wave file with the given name.
let concatenate targetFile (files: ISampleProvider seq) =
    let combined = ConcatenatingSampleProvider files
    WaveFileWriter.CreateWaveFile16(targetFile, combined)

/// Offsets the sample provider by the given amounts.
let offset skip take (sampleProvider: ISampleProvider) =
    OffsetSampleProvider(sampleProvider, SkipOver = skip, Take = take)

/// Adds a fade-in and fade-out to the sample provider.
let fade fadeIn fadeOut audioLength (sampleProvider: ISampleProvider) =
    AudioFader(sampleProvider, fadeIn, fadeOut, audioLength) :> ISampleProvider

