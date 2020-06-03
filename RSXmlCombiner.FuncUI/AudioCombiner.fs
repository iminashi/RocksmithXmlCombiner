namespace RSXmlCombiner.FuncUI

module AudioCombiner =
    open System.Text
    open System
    open System.IO
    open System.Diagnostics
    open Rocksmith2014Xml

    /// Creates the command line arguments for sox.
    let private createArguments (tracks : Track list) targetFile =
        let arguments = StringBuilder(sprintf "--multi−threaded --buffer 131072 -S \"%s\"" (tracks.Head.AudioFile |> Option.get))
        
        for t in tracks.[1..] do
            // Format example: "|sox foo.wav -p trim 5.500"
            let next = sprintf " \"|sox \\\"%s\\\" -p trim %s\"" (t.AudioFile |> Option.get) (Utils.TimeCodeToString(t.TrimAmount))
            arguments.Append(next) |> ignore

        arguments.Append(sprintf " \"%s\"" targetFile).ToString()

    /// Uses sox to combine the audio files of the given tracks into the target file.
    let private combine (tracks : Track list) targetFile =
        let arguments = createArguments tracks targetFile
        let soxProcess = new Process()
        let appPath = AppDomain.CurrentDomain.BaseDirectory;
        soxProcess.StartInfo.FileName <- Path.Combine (appPath, "tools", "sox", "sox")
        soxProcess.StartInfo.Arguments <- arguments
        soxProcess.Start() |> ignore
        soxProcess.WaitForExit()
        soxProcess.ExitCode
        
    /// Combines the audio files of the given tracks into the target file.
    let combineAudioFiles (tracks : Track list) targetFile =
        let exitCode = combine tracks targetFile
        match exitCode with
        | 0 -> sprintf "Audio files combined as %s" targetFile
        | _ -> sprintf "Error: sox exited with code %i" exitCode
