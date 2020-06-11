namespace RSXmlCombiner.FuncUI

module AudioCombiner =
    open System
    open System.IO
    open System.Diagnostics
    open System.Runtime.InteropServices
    open System.Text
    open Rocksmith2014Xml

    [<DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError=true)>]
    extern uint GetShortPathName(
       [<MarshalAs(UnmanagedType.LPTStr)>]
       string lpszLongPath,
       [<MarshalAs(UnmanagedType.LPTStr)>]
       StringBuilder lpszShortPath,
       uint cchBuffer)

    /// On Windows, gets the short 8.3 path name for the given path. 
    let getShortPath path =
        if not <| RuntimeInformation.IsOSPlatform OSPlatform.Windows then
            path
        else
            let path = @"\\?\" + path
            let bufferSize = GetShortPathName(path, null, 0u)
            if bufferSize = 0u then
                failwith ("Could not get short path for path: " + path)

            let shortNameBuffer = new StringBuilder(int bufferSize)
            let size = GetShortPathName(path, shortNameBuffer, bufferSize)

            if size = 0u then
                failwith ("Could not get short path for path: " + path)

            shortNameBuffer.ToString();

    /// Creates the command line arguments for SoX.
    let private createArguments (tracks : Track list) targetFile =
        // The file needs to exist for GetShortPathName to work
        if not <| File.Exists targetFile then
            File.Create(targetFile).Dispose()

        let first = getShortPath(tracks.Head.AudioFile |> Option.get)
        let target = getShortPath targetFile

        // Initial arguments: "--multi−threaded --buffer 131072 -S "|sox \"firstfile.wav\" -p rate 44100"
        let arguments = StringBuilder(sprintf "--multi−threaded --buffer 131072 -S \"|sox \\\"%s\\\" -p rate 44100\"" first)
        
        for t in tracks.Tail do
            // Format example: "|sox \"foo.wav\" -p rate 44100 trim 5.500"
            let file = t.AudioFile |> Option.get |> getShortPath
            let trim = t.TrimAmount |> Utils.TimeCodeToString
            let next = sprintf " \"|sox \\\"%s\\\" -p rate 44100 trim %s\"" file trim
            arguments.Append(next) |> ignore

        arguments.Append(sprintf " \"%s\"" target).ToString()

    /// Uses SoX to combine the audio files of the given tracks into the target file.
    let private combine (tracks : Track list) targetFile =
        let appPath = AppDomain.CurrentDomain.BaseDirectory;
        let arguments = createArguments tracks targetFile
        let startInfo = 
            ProcessStartInfo(
                FileName = Path.Combine (appPath, "tools", "sox", "sox"),
                Arguments = arguments
                //RedirectStandardError = true
            )
        use soxProcess = new Process(StartInfo = startInfo)
        soxProcess.Start() |> ignore
        soxProcess.WaitForExit()
        //File.WriteAllLines(Path.Combine(appPath, "log.txt"), seq { "Arguments:\n"; arguments; "\nOutput:\n"; soxProcess.StandardError.ReadToEnd() })
        soxProcess.ExitCode
        
    /// Combines the audio files of the given tracks into the target file.
    let combineAudioFiles (tracks : Track list) targetFile =
        try
            let exitCode = combine tracks targetFile
            match exitCode with
            | 0 -> sprintf "Audio files combined as %s" targetFile
            | _ -> sprintf "Error: SoX exited with code %i" exitCode
        with
        | e -> sprintf "Error: %s" e.Message
