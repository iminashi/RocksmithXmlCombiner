module RSXmlCombiner.FuncUI.XmlUtils

open System.Xml

let getRootElementName (path: string) =
    use reader = XmlReader.Create(path)
    reader.MoveToContent() |> ignore
    reader.LocalName

let validateRootName expectedName (path: string) =
    getRootElementName path = expectedName
