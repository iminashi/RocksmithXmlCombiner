module RSXmlCombiner.FuncUI.Dialogs

open System.Collections.Generic
open Avalonia
open Avalonia.Controls

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

let openFolderDialog title directory =
    let dialog = OpenFolderDialog(Title = title)
    directory |> Option.iter (fun dir -> dialog.Directory <- dir)

    async {
        let! result = dialog.ShowAsync(window) |> Async.AwaitTask
        return Option.create String.notEmpty result
    }

let saveFileDialog title filters initialFileName directory =
    let dialog = SaveFileDialog(Title = title, Filters = filters)
    initialFileName |> Option.iter (fun fn -> dialog.InitialFileName <- fn)
    directory |> Option.iter (fun dir -> dialog.Directory <- dir)

    async {
        let! result = dialog.ShowAsync(window) |> Async.AwaitTask
        return Option.create String.notEmpty result
    }

let openFileDialog title filters directory =
    let dialog = OpenFileDialog(Title = title, Filters = filters, AllowMultiple = false)
    directory |> Option.iter (fun dir -> dialog.Directory <- dir)

    async {
        match! dialog.ShowAsync(window) |> Async.AwaitTask with
        | [| file |] -> return Some file
        | _ -> return None
    }

let openFileDialogMulti title filters directory =
    let dialog = OpenFileDialog(Title = title, Filters = filters, AllowMultiple = true)
    directory |> Option.iter (fun dir -> dialog.Directory <- dir)

    async {
        match! dialog.ShowAsync(window) |> Async.AwaitTask with
        | null | [||] -> return None
        | arr -> return Some arr
    }
