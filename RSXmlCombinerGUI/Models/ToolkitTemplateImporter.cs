
using Rocksmith2014Xml;

using RSXmlCombinerGUI.Extensions;
using RSXmlCombinerGUI.ViewModels;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace RSXmlCombinerGUI.Models
{
    public sealed class ToolkitTemplateImporter
    {
        private TrackListViewModel TrackListViewModel { get; }

        private static readonly XNamespace ad = "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage";
        private static readonly XNamespace d4p1 = "http://schemas.datacontract.org/2004/07/RocksmithToolkitLib.DLCPackage.AggregateGraph";

        public ToolkitTemplateImporter(TrackListViewModel trackListViewModel)
        {
            TrackListViewModel = trackListViewModel;
        }

        public TrackViewModel? Import(string templateFile)
        {
            try
            {
                string templatePath = Path.GetDirectoryName(templateFile)!;
                var xdoc = XElement.Load(templateFile);

                var arrangements = xdoc.Element(ad + "Arrangements").Elements();
                string title = xdoc.Element(ad + "SongInfo").Element(ad + "SongDisplayName").Value;

                // If there is no ArrangementName tag, assume that it is an old template file
                if (arrangements.First().Element(ad + "ArrangementName") == null)
                {
                    return ImportOld(arrangements, templatePath, title);
                }

                var foundArrangements = new Dictionary<ArrangementType, (string fileName, string? baseTone)>();
                string? instArrFile = null;
                ArrangementType instArrType = ArrangementType.Unknown;

                foreach (var itemNode in arrangements)
                {
                    string arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value);
                    var arrType = Enum.Parse<ArrangementType>(itemNode.Element(ad + "ArrangementName").Value);

                    // Only include primary arrangements (represent = true)
                    if (itemNode.Element(ad + "Represent").Value == "true")
                    {
                        var baseTone = itemNode.Element(ad + "ToneBase").Value;

                        if (instArrFile is null)
                        {
                            instArrFile = arrFn;
                            instArrType = arrType;
                        }

                        foundArrangements.Add(arrType, (arrFn, baseTone));
                    }
                    else if (itemNode.Element(ad + "ArrangementName").Value == "Vocals"
                          || itemNode.Element(ad + "ArrangementName").Value == "ShowLights")
                    {
                        // TODO: JVocals
                        foundArrangements.Add(arrType, (arrFn, null));
                    }
                }

                return CreateViewModel(instArrFile, title, foundArrangements);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        private TrackViewModel? ImportOld(IEnumerable<XElement> arrangements, string templatePath, string title)
        {
            try
            {
                var foundArrangements = new Dictionary<ArrangementType, (string fileName, string? baseTone)>();
                string? instArrFile = null;
                ArrangementType instArrType = ArrangementType.Unknown;

                foreach (var itemNode in arrangements)
                {
                    bool bonusArr = itemNode.Element(ad + "BonusArr").Value == "true";
                    ArrangementType arrType = ArrangementType.ShowLights;

                    // Try to read the arrangement type of an instrumental arrangement from the route mask
                    if (Enum.TryParse(itemNode.Element(ad + "RouteMask").Value, out ArrangementType type))
                    {
                        arrType = type;
                    }
                    else
                    {
                        // Try to read the arrangement type from the pluralized arrangement type value
                        if (Enum.TryParse(itemNode.Element(ad + "ArrangementType").Value + "s", out ArrangementType type2))
                            arrType = type2;
                        else
                            continue;
                    }

                    if (bonusArr || foundArrangements.ContainsKey(arrType))
                        continue;

                    string arrFn = Path.Combine(templatePath, itemNode.Element(ad + "SongXml").Element(d4p1 + "File").Value);
                    var baseTone = itemNode.Element(ad + "ToneBase").Value;

                    if (instArrFile is null && arrType.IsInstrumental())
                    {
                        instArrFile = arrFn;
                        instArrType = arrType;
                    }

                    foundArrangements.Add(arrType, (arrFn, baseTone));
                }

                return CreateViewModel(instArrFile, title, foundArrangements);
            }
            catch (NullReferenceException)
            {
                return null;
            }
        }

        private TrackViewModel? CreateViewModel(
            string? instArrFile,
            string title,
            Dictionary<ArrangementType, (string fileName, string? baseTone)> foundArrangements)
        {
            if (instArrFile != null)
            {
                var instArr = RS2014Song.Load(instArrFile);
                var vm = new TrackViewModel(title, instArr.StartBeat, instArr.SongLength, TrackListViewModel);

                foreach (var kv in foundArrangements.OrderBy(kv => kv.Key))
                {
                    vm.AddNewArrangement(kv.Key, kv.Value.fileName, kv.Value.baseTone);
                }

                return vm;
            }

            return null;
        }
    }
}
