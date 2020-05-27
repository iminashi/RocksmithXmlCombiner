namespace RSXmlCombiner.FuncUI

module ToolkitImporter =
    open System.Collections.Generic
    open System.IO
    open System.Linq
    open System.Xml.Linq
    open Types

    let private ad = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage"
    let private d4p1 = XNamespace.Get "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage.AggregateGraph"

    let private importOld (arrangements : IEnumerable<XElement>) templatePath title =
        let mutable foundArrangements : Map<ArrangementType, string * string option> = Map.empty

        for itemNode in arrangements do
            let bonusArr = itemNode.Element(ad + "BonusArr").Value = "true"
            let mutable arrType = ArrangementType.Unknown

            // Try to read the arrangement type from route mask or the pluralized arrangement type value
            if ArrangementType.TryParse(itemNode.Element(ad + "RouteMask").Value, &arrType) || ArrangementType.TryParse(itemNode.Element(ad + "ArrangementType").Value + "s", &arrType) then
                if not (bonusArr || foundArrangements.ContainsKey(arrType)) then
                    let arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value)
                    let baseTone = itemNode.Element(ad + "ToneBase").Value;
                
                    foundArrangements <- foundArrangements.Add(arrType, (arrFn, Some baseTone));

        foundArrangements
            
    let import (fileName : string) =
        let templatePath = Path.GetDirectoryName(fileName)
        let xdoc = XElement.Load(fileName)
        let arrangements = xdoc.Element(ad + "Arrangements").Elements();
        let title = xdoc.Element(ad + "SongInfo").Element(ad + "SongDisplayName").Value;
    
        // If there is no ArrangementName tag, assume that it is an old template file
        if arrangements.First().Element(ad + "ArrangementName") = null then
            importOld arrangements templatePath title
        else
            // ArrangementType -> file name * base tone name
            let mutable foundArrangements : Map<ArrangementType, string * string option> = Map.empty

            for itemNode in arrangements do
                let arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value);
                let arrType = ArrangementType.Parse(itemNode.Element(ad + "ArrangementName").Value);
    
                // Only include primary arrangements (represent = true)
                if itemNode.Element(ad + "Represent").Value = "true" then
                    let baseTone = itemNode.Element(ad + "ToneBase").Value;
    
                    foundArrangements <- foundArrangements.Add(arrType, (arrFn, Some baseTone));
                else if itemNode.Element(ad + "ArrangementName").Value = "Vocals" || itemNode.Element(ad + "ArrangementName").Value = "ShowLights" then
                    // TODO: JVocals
                    foundArrangements <- foundArrangements.Add(arrType, (arrFn, None));
    
            foundArrangements