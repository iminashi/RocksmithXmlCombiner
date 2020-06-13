namespace RSXmlCombiner.FuncUI

module Option =
    /// If the predicate evaluates to true, return Some x, else None.
    let create pred x = if pred x then Some x else None
