namespace RSXmlCombiner.FuncUI

open System

module Option =
    /// If the predicate evaluates to true, return Some x, else None.
    let create pred x = if pred x then Some x else None

module String =
    /// Returns true if the given string is not null or an empty string.
    let notEmpty = String.IsNullOrEmpty >> not

module Array =
    /// Returns a new array with the element at the given index changed to the new one.
    let updateAt index newElem = Array.mapi (fun i old -> if i = index then newElem else old)