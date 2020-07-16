module RSXmlCombiner.FuncUI.Dialogs

open System.Collections.Generic
open Avalonia
open Avalonia.Controls
open Avalonia.Threading
open System.Threading.Tasks

let private window =
    (Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow

let private createFilters name (extensions : string seq) =
    let filter = FileDialogFilter(Extensions = List(extensions), Name = name)
    List(seq { filter })

let audioFileFiltersOpen = createFilters "Audio Files" (seq { "wav"; "ogg" })
let audioFileFiltersSave = createFilters "Wave Files" (seq { "wav" })
let xmlFileFilter = createFilters "Rocksmith Arrangement Files" (seq { "xml" })
let projectFileFilter = createFilters "Project Files" (seq { "rscproj" })
let toolkitTemplateFilter = createFilters "Toolkit Templates" (seq { "dlc.xml" })

/// Shows an open folder dialog.
let openFolderDialog title directory dispatch = 
    Dispatcher.UIThread.InvokeAsync(
        fun () ->
            OpenFolderDialog(
                Title = title,
                Directory = Option.toObj directory)
               .ShowAsync(window)
               .ContinueWith(fun (t: Task<string>) -> 
                   match t.Result with
                   | null | "" -> ()
                   | file -> file |> dispatch)
        ) |> ignore

/// Shows a save file dialog.
let saveFileDialog title filters initialFileName directory dispatch = 
    Dispatcher.UIThread.InvokeAsync(
        fun () ->
            SaveFileDialog(
                Title = title,
                Filters = filters,
                InitialFileName = Option.toObj initialFileName,
                Directory = Option.toObj directory)
               .ShowAsync(window)
               .ContinueWith(fun (t: Task<string>) -> 
                   match t.Result with
                   | null | "" -> ()
                   | file -> file |> dispatch)
        ) |> ignore

let private showOpenFileDialog t f d m =
    OpenFileDialog(Title = t, AllowMultiple = m, Filters = f, Directory = Option.toObj d)
        .ShowAsync(window)

/// Shows an open file dialog for selecting a single file.
let openFileDialog title filters directory dispatch = 
    Dispatcher.UIThread.InvokeAsync(
        fun () ->
            (showOpenFileDialog title filters directory false)
               .ContinueWith(fun (t: Task<string[]>) -> 
                   match t.Result with
                   | [| file |] -> file |> dispatch
                   | _ -> ())
        ) |> ignore

/// Shows an open file dialog that allows selecting multiple files.
let openMultiFileDialog title filters directory dispatch = 
    Dispatcher.UIThread.InvokeAsync(
        fun () ->
            (showOpenFileDialog title filters directory true)
               .ContinueWith(fun (t: Task<string[]>) -> 
                   match t.Result with
                   | null | [||] -> ()
                   | files -> files |> dispatch)
        ) |> ignore
