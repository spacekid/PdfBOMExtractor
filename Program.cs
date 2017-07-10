using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PdfTableExtractor
{
    class Program
    {
        private static Regex indicatorRegex = new Regex(@"^([A-Z]*?)(\d*?)(?=\s+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex componentIdRegex = new Regex(@"(IC|R|C|D|Q|TR|SW|P|PT)(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex componentValueRegex = new Regex(@"(\d+\.\d+|\d+)[R|K|M]\d*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex multipleComponentsRegex = new Regex(@"[a-zA-Z]+\d+(-[a-zA-Z]*?\d+)+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex multiCompExtractionRegex = new Regex(@"(([a-zA-Z]*?)(\d+))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static Regex resistorRegex = new Regex(@"(?<=\W)((\d+\.\d+|\d+)[R|K|M]\d*)(?![A|B|C])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex capacitorRegex = new Regex(@"(\d+\.\d+|\d+)([p|pf|n|nf|u|uf])\d{1}|(\d+\.\d+|\d+)([p|pf|n|nf|u|uf])(?!\d)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex jedecRegex = new Regex(@"[1|2|3]N\d+[A-Z]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex proElectronRegex = new Regex(@"[A|B|C|R][A|B|C|D|E|F|G|H|L|N|P|Q|R|S|T|U|W|X|Y|Z](\d{3}|[A-Z]\d{2})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex potentioMeterRegex = new Regex(@"(?<=\W)[A|B|C]?((\d+\.\d+|\d+)[R|K|M]\d*)[A|B|C]?(?!\sTRIM)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex generalRegex = new Regex(@"(?<=IC\d+\s+).*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static List<Regex> semiConductorRegexList = new List<Regex>() { jedecRegex, proElectronRegex };
        private static List<Regex> potentioMeterRegexList = new List<Regex>() { potentioMeterRegex };

        private static Dictionary<enumComponentType, List<Regex>> componentValueRegexDict = new Dictionary<enumComponentType, List<Regex>>
        {
            { enumComponentType.RESISTOR, new List<Regex>(){ resistorRegex } },
            { enumComponentType.CAPACITOR, new List<Regex>(){ capacitorRegex } },
            { enumComponentType.DIODE, semiConductorRegexList },
            { enumComponentType.TRANSISTOR, semiConductorRegexList },
            { enumComponentType.INTEGRATED_CIRCUIT, new List<Regex>(){ generalRegex } },
            { enumComponentType.SWITCH, new List<Regex>(){ generalRegex } },
            { enumComponentType.POTENTIOMETER, potentioMeterRegexList }
        };

        private static Dictionary<enumComponentType, List<string>> abbreviationComponentTypeList =
            new Dictionary<enumComponentType, List<string>>()
            {
                { enumComponentType.RESISTOR, new List<string>() { "R" } },
                { enumComponentType.CAPACITOR, new List<string>() { "C" } },
                { enumComponentType.DIODE, new List<string>() { "D" } },
                { enumComponentType.TRANSISTOR, new List<string>() { "Q", "TR" } },
                { enumComponentType.INTEGRATED_CIRCUIT, new List<string>() { "IC" } },
                { enumComponentType.POTENTIOMETER, new List<string>() { "P", "PT" } },
                { enumComponentType.SWITCH, new List<string>() { "S", "SW" } },
                { enumComponentType.UNKNOWN, new List<string>() { string.Empty } }
            };

    static void Main(string[] args)
        {
            string pdfPath = @"G:\Martijn\Music\Guitar Pedals\Gristleiser.pdf";
            //string pdfPath = @"G:\Martijn\Music\Guitar Pedals\Repeater-V3.pdf";

            //get the text from the pdf
            Dictionary<int, string[]> pdfTextDict = normalizeComponents(ExtractTextFromPdf(pdfPath));

            findBillOfMaterials(pdfTextDict);
            return;
        }

        private static Dictionary<int, string[]> normalizeComponents(Dictionary<int, string[]> dictionary)
        {
            Dictionary<int, string[]> normalizedDict = new Dictionary<int, string[]>();
            
            for (int index = 0; index < dictionary.Count; index++)
            {
                var dictItem = dictionary.ElementAt(index);

                string[] pageLines = dictItem.Value;

                List<string> normalizedPageLines = new List<string>();

                //normalize pagelines
                for (int lineIndex=0;lineIndex<pageLines.Length;lineIndex++)
                {
                    string pageLine = pageLines[lineIndex];

                    //check for two or more components on the same line
                    Match multiMatch = multipleComponentsRegex.Match(pageLine);
                    if (multiMatch.Success)
                    {
                        //get the multi match string
                        string multiMatchValue = multiMatch.Value;

                        //get the components
                        MatchCollection multiCompList = multiCompExtractionRegex.Matches(multiMatchValue);
                        if (multiCompList != null && multiCompList.Count > 0)
                        {
                            string componentValue = pageLine.Replace(multiMatchValue, string.Empty);

                            string componentType = string.Empty;
                            foreach(Match multiCompMatch in multiCompList)
                            {
                                if (string.IsNullOrEmpty(componentType))
                                {
                                    if (multiCompMatch.Groups.Count == 4)
                                    {
                                        componentType = multiCompMatch.Groups[2].Value;
                                    }
                                }

                                string compIndicator = string.Empty;
                                if (!string.IsNullOrEmpty(multiCompMatch.Groups[2].Value))
                                {
                                    compIndicator = multiCompMatch.Value;
                                }
                                else
                                {
                                    compIndicator = string.Format("{0}{1}",
                                        componentType,
                                        multiCompMatch.Groups[3].Value);
                                }

                                string newPageLine = string.Format("{0}{1}", compIndicator, componentValue);

                                normalizedPageLines.Add(newPageLine);
                            }                            
                        }
                    }
                    else
                    {
                        normalizedPageLines.Add(pageLine);
                    }
                }

                normalizedDict.Add(index, normalizedPageLines.ToArray());
            }
            return normalizedDict;
        }

        private static void findBillOfMaterials(Dictionary<int, string[]> pdfTextDict)
        {
            List<Component> componentList = new List<Component>();

            foreach (int page in pdfTextDict.Keys)
            {
                string[] pageLines = pdfTextDict[page];

                for(int lineIndex=0;lineIndex<pageLines.Length;lineIndex++)
                {
                    string pageLine = pageLines[lineIndex];

                    //potential BOM Found
                    //description: loose indicator match that checks "[character][number(s)][whitespace]"
                    Match indicatorMatch = indicatorRegex.Match(pageLine);
                    if (indicatorMatch.Success)
                    {
                        //get the matched component indicator
                        string componentIndicator = indicatorMatch.Value;

                        enumComponentType componentType = enumComponentType.UNKNOWN;

                        int componentSeqNum = -1;

                        if (indicatorMatch.Groups.Count == 3)
                        {
                            componentType = matchComponentType(indicatorMatch.Groups[1].Value);

                            if (!Int32.TryParse(indicatorMatch.Groups[2].Value, out componentSeqNum))
                            {
                                componentSeqNum = -1;
                            }
                        }

                        Component component = matchComponentTypeByValue(pageLine, 
                            componentIndicator, componentType, componentSeqNum);

                        if (component != null && component.Type != enumComponentType.UNKNOWN)
                        {
                            Console.WriteLine("id=" + component.ID + ", type=" + component.Type + ",value=" + component.Value);
                            componentList.Add(component);
                        }
                    }
                }
            }
        }

        private static enumComponentType matchComponentType(string value)
        {
            foreach(var key in abbreviationComponentTypeList.Keys)
            {
                List<string> abbreviationList = abbreviationComponentTypeList[key];
                if (abbreviationList != null)
                {
                    foreach(var abbreviation in abbreviationList)
                    {
                        if (abbreviation.Equals(value, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return key;
                        }
                    }
                }
            }
            return enumComponentType.UNKNOWN;
        }

        private static Component matchComponentTypeByValue(string pageLine, 
            string componentIndicator, enumComponentType componentType, int componentSeqNum)
        {
            Component component = null;

            if (!componentType.Equals(enumComponentType.UNKNOWN))
            {
                List<Regex> compTypeRegexList = componentValueRegexDict[componentType];

                component = createComponentFromMatch(pageLine, 
                    componentIndicator, componentType, componentSeqNum,
                    compTypeRegexList);
            }
            else
            {
                foreach (var key in componentValueRegexDict.Keys)
                {
                    List<Regex> list = componentValueRegexDict[key];

                    component = createComponentFromMatch(pageLine,
                        componentIndicator, enumComponentType.UNKNOWN, -1, list);
                    if (component != null && !component.Type.Equals(enumComponentType.UNKNOWN))
                    {
                        break;
                    }
                }
            }
            return component;
        }

        private static Component createComponentFromMatch(string pageLine, 
            string componentIndicator, enumComponentType componentType, int componentSeqNum, 
            List<Regex> list)
        {
            Component component = null;
            foreach (Regex item in list)
            {
                Match itemMatch = item.Match(pageLine);
                if (itemMatch.Success)
                {
                    component = new Component
                    {
                        ID = componentIndicator,
                        Type = componentType,
                        SequenceNumber = componentSeqNum,
                        Value = itemMatch.Value
                    };
                    break;
                }
            }
            if (component == null)
            {
                component = new Component
                {
                    ID = componentIndicator,
                    Type = componentType,
                    Value = pageLine
                };
            }
            return component;
        }

        public static Dictionary<int, string[]> ExtractTextFromPdf(string path)
        {
            Dictionary<int, string[]> pdfTextDict = new Dictionary<int, string[]>();

            using (PdfReader reader = new PdfReader(path))
            {
                StringBuilder text = new StringBuilder();
                

                for (int i = 1; i <= reader.NumberOfPages; i++)
                {
                    string page = PdfTextExtractor.GetTextFromPage(reader, i, new SimpleTextExtractionStrategy());

                    pdfTextDict.Add(i, page.Split('\n'));
                }
            }
            return pdfTextDict;
        }
    }
}
