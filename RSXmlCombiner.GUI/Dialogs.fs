module RSXmlCombiner.FuncUI.Dialogs

open Avalonia
open Avalonia.Controls
open Avalonia.Threading

let private window =
    (Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow

let private createFilters name (extensions: string seq) =
    let filter = FileDialogFilter(Extensions = ResizeArray(extensions), Name = name)
    ResizeArray(seq { filter })

let audioFileFiltersOpen = createFilters "Audio Files" (seq { "wav"; "ogg" })
let audioFileFiltersSave = createFilters "Wave Files" (seq { "wav" })
let xmlFileFilter = createFilters "Rocksmith Arrangement Files" (seq { "xml" })
let projectFileFilter = createFilters "Project Files" (seq { "rscproj" })
let toolkitTemplateFilter = createFilters "Toolkit Templates" (seq { "dlc.xml" })

/// Shows an open folder dialog.
let openFolderDialog title directory = async {
    let! result =
        Dispatcher.UIThread.InvokeAsync<string>(fun () ->
            let dialog = OpenFolderDialog(Title = title, Directory = Option.toObj directory)
            dialog.ShowAsync window)
        |> Async.AwaitTask

    return Option.ofString result }

/// Shows a save file dialog.
let saveFileDialog title filters initialFileName directory = async {
    let! result =
        Dispatcher.UIThread.InvokeAsync<string>(fun () ->
            let dialog =
                SaveFileDialog(
                    Title = title,
                    Filters = filters,
                    InitialFileName = Option.toObj initialFileName,
                    Directory = Option.toObj directory)
            dialog.ShowAsync window)
        |> Async.AwaitTask

    return Option.ofString result }

let private createOpenFileDialog t f d m =
    OpenFileDialog(Title = t, Filters = f, Directory = Option.toObj d, AllowMultiple = m)

/// Shows an open file dialog for selecting a single file.
let openFileDialog title filters directory = async {
    let! result =
        Dispatcher.UIThread.InvokeAsync<string[]>(fun () ->
            let dialog = createOpenFileDialog title filters directory false
            dialog.ShowAsync window)
        |> Async.AwaitTask
    match result with
    | [| file |] -> return Some file
    | _ -> return None }

/// Shows an open file dialog that allows selecting multiple files.
let openMultiFileDialog title filters directory = async {
    let! result =
        Dispatcher.UIThread.InvokeAsync<string[]>(fun () ->
            let dialog = createOpenFileDialog title filters directory true
            dialog.ShowAsync window)
        |> Async.AwaitTask
    match result with
    | null | [||] -> return None
    | arr -> return Some arr }
