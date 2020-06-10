namespace RSXmlCombiner.FuncUI

module Dialogs =
    open System.Collections.Generic
    open Avalonia
    open Avalonia.Controls

    let private createFilters name (extensions : string seq) =
        let filter = FileDialogFilter()
        filter.Extensions <- List(extensions)
        filter.Name <- name
        List(seq { filter })

    let audioFileFilters = createFilters "Audio Files" (seq { "wav"; "ogg" })
    let xmlFileFilter = createFilters "Rocksmith Arrangement Files" (seq { "xml" })
    let projectFileFilter = createFilters "Project Files" (seq { "rscproj" })
    let toolkitTemplateFilter = createFilters "Toolkit Templates" (seq { "dlc.xml" })

    let openFolderDialog title =
        let dialog = OpenFolderDialog()
        dialog.Title <- title
        let window = (Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow
        dialog.ShowAsync(window) |> Async.AwaitTask

    let saveFileDialog title filters initialFileName =
        let dialog = SaveFileDialog()
        dialog.Title <- title
        dialog.Filters <- filters
        initialFileName |> Option.iter (fun fn -> dialog.InitialFileName <- fn)

        let window = (Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow
        dialog.ShowAsync(window) |> Async.AwaitTask

    let openFileDialog title filters allowMultiple directory =
        let dialog = OpenFileDialog()
        dialog.Title <- title
        dialog.Filters <- filters
        dialog.AllowMultiple <- allowMultiple
        directory |> Option.iter (fun dir -> dialog.Directory <- dir)

        let window = (Application.Current.ApplicationLifetime :?> ApplicationLifetimes.ClassicDesktopStyleApplicationLifetime).MainWindow
        dialog.ShowAsync(window) |> Async.AwaitTask
