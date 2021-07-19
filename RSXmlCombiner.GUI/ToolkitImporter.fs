module RSXmlCombiner.FuncUI.ToolkitImporter

open System.IO
open System.Linq
open System.Xml.Linq

let private ad = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage"
let private d4p1 = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage.AggregateGraph"

let private importOld (arrangements: XElement seq) templatePath =
    (Map.empty, arrangements)
    ||> Seq.fold (fun map itemNode ->
        let isBonusArr = itemNode.Element(ad + "BonusArr").Value = "true"

        // Try to read the arrangement type from the name element
        match ArrangementType.TryParse(itemNode.Element(ad + "Name").Value) with
        | true, arrType when not (isBonusArr || map.ContainsKey arrType) ->
            let arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value)

            map.Add(arrType, arrFn)
        | _ ->
            map)

/// Imports the main arrangements from a Toolkit template.
/// Returns them in a map: arrangement type to filename.
let import (fileName: string) =
    let templateDirectory = Path.GetDirectoryName fileName
    let xdoc = XElement.Load fileName
    let arrangements = xdoc.Element(ad + "Arrangements").Elements()
    let title = xdoc.Element(ad + "SongInfo").Element(ad + "SongDisplayName").Value
    let audioFile = Path.Combine(templateDirectory, xdoc.Element(ad + "OggPath").Value)

    let arrangementMap =
        // If there is no ArrangementName tag, assume that it is an old template file
        if isNull <| arrangements.First().Element(ad + "ArrangementName") then
            importOld arrangements templateDirectory
        else
            // Map ArrangementType to filename
            (Map.empty, arrangements)
            ||> Seq.fold (fun map itemNode ->
                let songXml = itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value
                let arrXmlPath = Path.Combine(templateDirectory, songXml)
                let arrType = ArrangementType.Parse(itemNode.Element(ad + "ArrangementName").Value)
                let isMainArr = itemNode.Element(ad + "Represent").Value = "true"

                // Only include primary arrangements (represent = true), any vocals and show lights
                if isMainArr || ArrangementType.isOther arrType then
                    map.Add(arrType, arrXmlPath)
                else
                    map)

    arrangementMap, title, audioFile
