namespace RSXmlCombiner.FuncUI

module ToolkitImporter =
    open System.Collections.Generic
    open System.IO
    open System.Linq
    open System.Xml.Linq

    let private ad = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage"
    let private d4p1 = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage.AggregateGraph"

    let private importOld (arrangements : IEnumerable<XElement>) templatePath =
        let folder (state :  Map<ArrangementType, string>) (itemNode : XElement) =
            let bonusArr = itemNode.Element(ad + "BonusArr").Value = "true"

            // Try to read the arrangement type from the name
            let success, arrType = ArrangementType.TryParse(itemNode.Element(ad + "Name").Value)
            
            match success, arrType with
            | true, t when not (bonusArr || state.ContainsKey t) ->
                let arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value)
                
                state.Add(t, arrFn);
            | _ -> state

        arrangements |> Seq.fold folder Map.empty
    
    /// Imports the main arrangements from a Toolkit template.
    /// Returns them in a map: arrangement type to file name.
    let import (fileName : string) =
        let templatePath = Path.GetDirectoryName(fileName)
        let xdoc = XElement.Load(fileName)
        let arrangements = xdoc.Element(ad + "Arrangements").Elements()
        let title = xdoc.Element(ad + "SongInfo").Element(ad + "SongDisplayName").Value
        let audioFile = Path.Combine(templatePath, xdoc.Element(ad + "OggPath").Value)
    
        // If there is no ArrangementName tag, assume that it is an old template file
        if isNull <| arrangements.First().Element(ad + "ArrangementName") then
            importOld arrangements templatePath, title, audioFile
        else
            // Map ArrangementType to file name
            let folder (state : Map<ArrangementType, string>) (itemNode : XElement) =
                let arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value)
                let arrType = ArrangementType.Parse(itemNode.Element(ad + "ArrangementName").Value)
                let isMainArr = itemNode.Element(ad + "Represent").Value = "true"
                
                // Only include primary arrangements (represent = true), any vocals and show lights
                if isMainArr || isVocals arrType || arrType = ArrangementType.ShowLights then
                    state.Add(arrType, arrFn)
                else
                    state

            arrangements |> Seq.fold folder Map.empty, title, audioFile
