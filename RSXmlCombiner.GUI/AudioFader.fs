﻿namespace RSXmlCombiner.FuncUI

open NAudio.Wave

type AudioFader(source: ISampleProvider, fadeInLength: int, fadeOutLength : int, audioLength : int64) =
    let mutable fadeInSamplePosition = 0
    let mutable fadeOutSamplePosition = 0
    let mutable totalSamplesRead = 0L

    let fadeInSampleCount = (fadeInLength * source.WaveFormat.SampleRate) / 1000
    let fadeOutSampleCount = (fadeOutLength * source.WaveFormat.SampleRate) / 1000
    let fadeOutStartSample = ((audioLength - int64 fadeOutLength) * int64 source.WaveFormat.SampleRate * int64 source.WaveFormat.Channels) / 1000L

    interface ISampleProvider with
        member this.Read(buffer:float32[], offset:int, count:int) = 
            let sourceSamplesRead = source.Read(buffer, offset, count)
            totalSamplesRead <- totalSamplesRead + int64 sourceSamplesRead

            if fadeInSamplePosition <= fadeInSampleCount then
                let mutable sample = 0
                while sample < sourceSamplesRead && fadeInSamplePosition <= fadeInSampleCount do
                    let multiplier = float32 fadeInSamplePosition / float32 fadeInSampleCount
                    for _ch = 1 to source.WaveFormat.Channels do
                        buffer.[offset + sample] <- buffer.[offset + sample] * multiplier;
                        sample <- sample + 1
                    fadeInSamplePosition <- fadeInSamplePosition + 1

            if totalSamplesRead >= fadeOutStartSample then
                let mutable sample : int =
                    if totalSamplesRead - int64 sourceSamplesRead < fadeOutStartSample then
                        int (fadeOutStartSample - (totalSamplesRead - int64 sourceSamplesRead))
                    else
                        0
                sample <- sample - (sample % source.WaveFormat.Channels)
                while sample < sourceSamplesRead do
                    let multiplier = 1.0f - (float32 fadeOutSamplePosition / float32 fadeOutSampleCount)
                    for _ch = 1 to source.WaveFormat.Channels do
                        buffer.[offset + sample] <- buffer.[offset + sample] * multiplier;
                        sample <- sample + 1
                    fadeOutSamplePosition <- fadeOutSamplePosition + 1

            sourceSamplesRead

        member _.WaveFormat = source.WaveFormat