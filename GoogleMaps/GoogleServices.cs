using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net;
using System.Runtime.InteropServices;

namespace TiledMaps
{
    public static class GoogleServices
    {
        static void WriteString(BinaryWriter writer, string s)
        {
            byte[] buff = UTF8Encoding.Default.GetBytes(s);
            short len = (short)(buff.Length);
            WriteShort(writer, len);
            writer.Write(buff);
        }

        static void WriteShort(BinaryWriter writer, short s)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter otherWriter = new BinaryWriter(stream);
                otherWriter.Write(s);
                byte[] buff = stream.GetBuffer();
                List<byte> thing = new List<byte>(buff);
                thing.RemoveRange(sizeof(short), thing.Count - sizeof(short));
                thing.Reverse();
                writer.Write(thing.ToArray());
            }
        }

        static void WriteInt(BinaryWriter writer, int i)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BinaryWriter otherWriter = new BinaryWriter(stream);
                otherWriter.Write(i);
                byte[] buff = stream.GetBuffer();
                List<byte> thing = new List<byte>(buff);
                thing.RemoveRange(sizeof(int), thing.Count - sizeof(int));
                thing.Reverse();
                writer.Write(thing.ToArray());
            }
        }

        static int ReadInt(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            int ret = (int)bytes[0] << 24 | (int)bytes[1] << 16 | (int)bytes[2] << 8 | (int)bytes[3];
            return ret;
        }

        const int SPI_GETOEMINFO = 258;
        [DllImport("coredll.dll", SetLastError = true)]
        static extern int SystemParametersInfo(int uiAction, int uiParam, StringBuilder pvParam, int fWinIni);
        static string Model
        {
            get
            {
                try
                {
                    StringBuilder OEMInfo = new StringBuilder(100);
                    int details = SystemParametersInfo(SPI_GETOEMINFO, OEMInfo.Capacity, OEMInfo, 0);
                    return OEMInfo.ToString();
                }
                catch (Exception)
                {
                    return string.Empty;
                }
            }
        }

        public static Geocode GetGeocode(int cellID, int locationAreaCode)
        {
            HttpWebRequest req = HttpWebRequest.Create("http://www.google.com/glm/mmap") as HttpWebRequest;
            MemoryStream s = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(s, UTF8Encoding.Default);

            WriteShort(writer, (short)21);
            writer.Write((long)0);
            WriteString(writer, "fr");
            writer.Flush();
            byte[] buff = s.GetBuffer();
            WriteString(writer, "something");
            WriteString(writer, "1.3.1");
            WriteString(writer, "Web");
            writer.Write((byte)27);

            writer.Write(0);
            writer.Write(0);
            WriteInt(writer, 3);
            WriteString(writer, "");
            WriteInt(writer, cellID);  // CELL-ID
            WriteInt(writer, locationAreaCode);     // LAC
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Flush();
            byte[] shit = s.GetBuffer();

            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = s.Length;
            Stream reqStream = req.GetRequestStream();
            reqStream.Write(shit, 0, (int)s.Length);
            reqStream.Close();
            using (HttpWebResponse resp = req.GetResponse() as HttpWebResponse)
            {
                using (BinaryReader reader = new BinaryReader(resp.GetResponseStream()))
                {
                    reader.ReadBytes(7);
                    double lat = (double)ReadInt(reader) / 1000000;
                    double lon = (double)ReadInt(reader) / 1000000;
                    return new Geocode(lat, lon);
                }
            }
        }

        static List<Geocode> DecodeGeocodes(byte[] bytes)
        {
            int index = 0;
            List<Geocode> array = new List<Geocode>();
            int lat = 0;
            int lon = 0;

            while (index < bytes.Length)
            {
                char b;
                int shift = 0;
                int result = 0;
                do
                {
                    // unescape java backslashes for decoding
                    if ((char)bytes[index] == '\\')
                    {
                        System.Diagnostics.Debug.Assert(index + 1 < bytes.Length);
                        index++;
                    }
                    b = (char)(bytes[index++] - 63);
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                }
                while (b >= 0x20);
                int dlat = ((result % 2 == 1) ? ~(result >> 1) : (result >> 1));
                lat += dlat;

                shift = 0;
                result = 0;
                do
                {
                    // unescape java backslashes for decoding
                    if ((char)bytes[index] == '\\')
                    {
                        System.Diagnostics.Debug.Assert(index + 1 < bytes.Length);
                        index++;
                    }
                    b = (char)(bytes[index++] - 63);
                    result |= (b & 0x1f) << shift;
                    shift += 5;
                }
                while (b >= 0x20);
                int dlng = ((result % 2 == 1) ? ~(result >> 1) : (result >> 1));
                lon += dlng;

                array.Add(new Geocode(lat * 0.00001, lon * 0.00001));
            }
            return array;
        }

        static int FindFirstOccurence(MemoryStream memory, string searchString, int start)
        {
            memory.Seek(start, SeekOrigin.Begin);
            StreamReader reader = new StreamReader(memory);

            const int maxSearch = 1 << 14;
            int offset = start;
            int found;
            while (offset < reader.BaseStream.Length)
            {
                int maxRead = offset + maxSearch < (int)reader.BaseStream.Length ? maxSearch : (int)reader.BaseStream.Length - offset;
                char[] buffer = new char[maxRead];
                int read = reader.Read(buffer, 0, maxRead);
                StringBuilder builder = new StringBuilder();
                builder.Append(buffer);
                string search = builder.ToString();
                if ((found = search.IndexOf(searchString)) != -1)
                {
                    return found + offset + searchString.Length;
                }
                offset += read;
            }
            return -1;
        }

        static List<int> DecodeLevels(byte[] bytes)
        {
            List<int> levels = new List<int>();
            for (int i = 0; i < bytes.Length; i++)
            {
                int level = bytes[i] - 63;
                levels.Add(level);
            }

            return levels;
        }

        static List<int> DecodeStepsGeocodes(char[] chars)
        {
            List<int> steps = new List<int>();
            //Regex regex = new Regex(@"{polyline:0,ppt:([\d]+)}");
            Regex regex = new Regex(@"ppt:([\d]+)");
            StringBuilder builder = new StringBuilder();
            builder.Append(chars);
            Match m = regex.Match(builder.ToString());
            while (m.Success)
            {
                steps.Add(Int32.Parse(m.Groups[1].Value));
                m = m.NextMatch();
            }
            return steps;
        }

        static string RemoveNonTextFormatting(string input)
        {
            StringBuilder builder = new StringBuilder(input);
            builder.Replace(@"\x26#160;", " ");
            builder.Replace(@"<wbr />", string.Empty);
            return builder.ToString();
        }

        static string RemoveAllFormatting(string input)
        {
            StringBuilder builder = new StringBuilder(RemoveNonTextFormatting(input));
            builder.Replace("<b>", string.Empty);
            builder.Replace("</b>", string.Empty);
            return builder.ToString();
        }

        static List<Segment> DecodeSteps(char[] chars)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(chars);

            // need to get the string blob into an unescaped format from the java string it is stored in
            // it is going to be turned into an xml blob
            builder.Replace(@"\x3c", "<").Replace(@"\x3e", ">").Replace(@"\""", @"""");

            string blob = builder.ToString();

            // Google seems to not like putting quotes around attributes
            Regex xmlCleaner = new Regex("=([^\"])(.*?)([^\"])([ >])");
            blob = xmlCleaner.Replace(blob, (m) =>
                {
                    return string.Format("=\"{0}{1}{2}\"{3}", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value);
                }
            );

            // search for the table that contains the direction segments
            // see SampleDecodedSegments.xml for an example of what this looks like
            Regex regex = new Regex(@"<table class=""ddr_steps"" id=""ddr_steps_0"">(.*?)</table>");
            Match match = regex.Match(blob);
            if (!match.Success)
                throw new Exception("Could not find Directions Steps blob.");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(match.ToString());

            List<Segment> ret = new List<Segment>();
            XmlNodeList list = doc.GetElementsByTagName("tr");
            // parse all the rows, which contain the segments
            foreach (XmlNode node in list)
            {
                Segment segment = new Segment();
                foreach (XmlNode child in node.ChildNodes)
                {
                    XmlAttribute attr = child.Attributes["class"];
                    if (attr == null)
                        continue;
                    if (attr.Value == "dirsegtext")
                    {
                        List<XmlNode> notes = new List<XmlNode>();
                        List<XmlNode> delete = new List<XmlNode>();
                        foreach (XmlNode gchild in child.ChildNodes)
                        {
                            bool handled = false;
                            if (gchild.Name == "b")
                            {
                                // bolded text
                                segment.RoadName = gchild.InnerXml;
                                continue;
                            }
                            else if (gchild is XmlText)
                            {
                                continue;
                            }
                            else if (gchild.Name == "div")
                            {
                                // look for driving notes
                                if (gchild.Attributes != null)
                                {
                                    XmlAttribute gattr = gchild.Attributes["class"];
                                    if (gattr != null)
                                    {
                                        if (gattr.Value.IndexOf("dirsegnote") != -1)
                                        {
                                            notes.Add(gchild);
                                            handled = true;
                                        }
                                    }
                                }
                            }

                            if (!handled)
                            {
                                System.Diagnostics.Debug.WriteLine("Unknown direction element " + gchild.InnerText);
                            }

                            // before getting the inner direction text
                            // we remove all notes and other extra crap we did not recognize
                            delete.Add(gchild);
                        }
                        if (notes.Count != 0)
                        {
                            segment.Notes = new string[notes.Count];
                            for (int i = 0; i < notes.Count; i++)
                            {
                                segment.Notes[i] = notes[i].InnerText;
                            }
                        }
                        foreach (XmlNode gchild in delete)
                        {
                            child.RemoveChild(gchild);
                        }
                        segment.FormattedText = RemoveNonTextFormatting(child.InnerXml);
                        segment.Text = RemoveAllFormatting(child.InnerText);
                    }
                    else if (attr.Value == "sdist")
                    {
                        foreach (XmlNode gchild in child.ChildNodes)
                        {
                            if (gchild.Attributes == null)
                                continue;
                            XmlAttribute gattr = gchild.Attributes["id"];
                            if (gattr == null)
                                continue;
                            if (gattr.Value == "sxdist")
                                segment.Distance = RemoveAllFormatting(gchild.InnerText);
                            else if (gattr.Value == "sxtime")
                                segment.Time = RemoveAllFormatting(gchild.InnerText);
                        }
                    }
                }
                ret.Add(segment);
            }
            return ret;
        }

        public static T GetDirections<T>(string startLoc, string endLoc) where T: Directions, new()
        {
            string uri = string.Format("http://maps.google.com/maps?f=d&hl=en&saddr={0}&daddr={1}&output=js", startLoc, endLoc);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    return null;

                using (MemoryStream memory = new MemoryStream())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        byte[] buffer = new byte[1024];
                        int read = -1;
                        while (read != 0)
                        {
                            read = stream.Read(buffer, 0, buffer.Length);
                            memory.Write(buffer, 0, read);
                        }
                    }

                    int polyLineStart = FindFirstOccurence(memory, "points:\"", 0);
                    if (polyLineStart == -1)
                        throw new Exception("Could not find Polyline blob.");
                    int polyLineEnd = FindFirstOccurence(memory, "\"", polyLineStart);
                    if (polyLineEnd == -1)
                        throw new Exception("Could not find Polyline blob.");
                    memory.Seek(polyLineStart, SeekOrigin.Begin);
                    byte[] byteBuffer = new byte[polyLineEnd - polyLineStart - 1];
                    memory.Read(byteBuffer, 0, byteBuffer.Length);
                    List<Geocode> geocodes = DecodeGeocodes(byteBuffer);

                    int levelsStart = FindFirstOccurence(memory, "levels:\"", polyLineEnd);
                    if (levelsStart == -1)
                        throw new Exception("Could not find Levels blob.");
                    int levelsEnd = FindFirstOccurence(memory, "\"", levelsStart);
                    if (polyLineEnd == -1)
                        throw new Exception("Could not find Levels blob.");
                    memory.Seek(levelsStart, SeekOrigin.Begin);
                    byteBuffer = new byte[levelsEnd - levelsStart - 1];
                    memory.Read(byteBuffer, 0, byteBuffer.Length);
                    List<int> levels = DecodeLevels(byteBuffer);

                    int stepsGeocodesStart = FindFirstOccurence(memory, "steps:[", levelsEnd);
                    if (stepsGeocodesStart == -1)
                        throw new Exception("Could not find Steps Geocodes blob.");
                    int stepsGeocodesEnd = FindFirstOccurence(memory, "]", stepsGeocodesStart);
                    if (stepsGeocodesEnd == -1)
                        throw new Exception("Could not find Steps Geocodes blob.");
                    memory.Seek(stepsGeocodesStart, SeekOrigin.Begin);
                    char[] charBuffer = new char[stepsGeocodesEnd - stepsGeocodesStart - 1];
                    StreamReader reader = new StreamReader(memory);
                    reader.Read(charBuffer, 0, charBuffer.Length - 1);
                    List<int> stepsGeocodes = DecodeStepsGeocodes(charBuffer);

                    int stepsStart = FindFirstOccurence(memory, "panel:\"", stepsGeocodesEnd);
                    if (stepsStart == -1)
                        throw new Exception("Could not find Directions Steps blob.");
                    int stepsEnd = FindFirstOccurence(memory, "\",", stepsStart);
                    if (stepsEnd == -1)
                        throw new Exception("Could not find Directions Steps blob.");
                    memory.Seek(stepsStart, SeekOrigin.Begin);
                    charBuffer = new char[stepsEnd - stepsStart - 1];
                    reader = new StreamReader(memory);
                    reader.Read(charBuffer, 0, charBuffer.Length);
                    List<Segment> steps = DecodeSteps(charBuffer);

                    T ret = new T();
                    ret.PolyLine = geocodes.ToArray();
                    ret.Levels = levels.ToArray();
                    ret.Segments = steps.ToArray();
                    for (int i = 0; i < stepsGeocodes.Count; i++)
                    {
                        ret.Segments[i].Geocode = ret.PolyLine[stepsGeocodes[i]];
                    }
                    return ret;
                }
            }
        }
    }
}
