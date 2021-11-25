namespace RSXmlCombiner.FuncUI

open System

[<RequireQualifiedAccess>]
module Option =
    /// If the predicate evaluates to true, return Some x, else None.
    let create pred x = if pred x then Some x else None

    /// If the string is null or whitespace, returns None.
    let ofString s = if String.IsNullOrWhiteSpace(s) then None else Some s

[<RequireQualifiedAccess>]
module String =
    /// Returns true if the given string is not null or an empty string.
    let notEmpty = String.IsNullOrEmpty >> not

[<RequireQualifiedAccess>]
module List =
    /// Returns a new list with the map function applied to the item at the given index.
    let mapAt index map list =
        List.mapi (fun i x -> if i = index then map x else x) list
