using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace TiledMaps
{
    public static class VirtualEarthServices
    {
        static int FindFirstOccurence(MemoryStream memory, string searchString, int start)
        {
            memory.Seek(start, SeekOrigin.Begin);
            StreamReader reader = new StreamReader(memory, Encoding.ASCII);

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

        static List<Segment> DecodeSteps(char[] chars)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(chars);
            string blob = builder.ToString();
            Regex regex = new Regex(@"new VE_RouteInstruction\('(.*?)',(-*[\d]+.[\d]+),(-*[\d]+.[\d]+),(-*[\d]+.[\d]+)");
            Regex cleaner = new Regex(@"&#[\d]+;.*?&#[\d]+;");
            Regex bolder = new Regex(@"( [A-Z][A-Z]+ )");
            Match m = regex.Match(blob);
            List<Segment> ret = new List<Segment>();
            while (m.Success)
            {
                Segment segment = new Segment();
                Geocode geocode = new Geocode();
                geocode.Latitude = double.Parse(m.Groups[2].Value);
                geocode.Longitude = double.Parse(m.Groups[3].Value);
                segment.Geocode = geocode;
                segment.Distance = string.Format("{0} miles", m.Groups[4].Value);

                string text = m.Groups[1].Value;
                Match m2 = cleaner.Match(text);
                while (m2.Success)
                {
                    text = text.Replace(m2.Value, string.Empty);
                    m2 = cleaner.Match(text);
                }

                string formattedText = text;
                m2 = bolder.Match(text);
                while (m2.Success)
                {
                    text = text.Replace(m2.Value, m2.Value.ToLower());
                    formattedText = formattedText.Replace(m2.Value, string.Format("<b>{0}</b>", m2.Value.ToLower()));
                    m2 = bolder.Match(text);
                }

                text = text.Replace("  ", " ");
                formattedText = formattedText.Replace("  ", " ").Replace("<b> ", " <b>").Replace(" </b>", "</b> ");

                segment.FormattedText = formattedText;
                segment.Text = text;
                
                ret.Add(segment);
                m = m.NextMatch();
            }

            return ret;
        }
        static List<double> DecodeDoubles(string input)
        {
            string blob = input.Replace(@"\\", @"\").Replace(@"\'", @"'");
            Regex regex = new Regex(@"\\x(.)(.)");
            Match m = regex.Match(blob);
            while (m.Success)
            {
                char high = m.Groups[1].Value[0];
                char low = m.Groups[2].Value[0];
                if (high >= 'A')
                    high -= (char)(high - 'A' + (char)10);
                else
                    high -= '0';
                if (low >= 'A')
                    low = (char)(low - 'A' + (char)10);
                else
                    low -= '0';
                char val = (char)(high * 16 + low);
                // need to special case null terminations
                if (val != 0)
                    blob = blob.Replace(m.Value, val.ToString());
                m = m.NextMatch();
            }

            char[] unescapedChars = blob.ToCharArray();

            List<double> ret = new List<double>();

            int b = 0;
            bool negative = false;
            int adjust = 0;
            for (int i = 0; i < blob.Length; i++)
            {
                char currentByte = blob[i];
                if (currentByte == '\\' && i + 3 < blob.Length && blob[i + 1] == 'x' && blob[i + 2] == '0' && blob[i + 3] == '0')
                {
                    currentByte = '\0';
                    adjust -= 3;
                    i += 3;
                }

                int charNum = (i + adjust) % 4;
                if (charNum == 0)
                {
                    negative = (currentByte & 128) != 0;
                    currentByte &= (char)127;
                }

                b |= currentByte;
                if (charNum == 3)
                {
                    double val = (double)b / (double)1000000;
                    if (negative)
                        val = -val;
                    ret.Add(val);
                    b = 0;
                    negative = false;
                }
                else
                    b <<= 8;
            }

            return ret;
        }

        public static Directions GetDirections(Geocode startLoc, Geocode endLoc)
        {
            string uri = string.Format("http://dev.virtualearth.net/legacyService/directions.ashx?mkt=en-us&startlat={0}&startlon={1}&endlat={2}&endlon={3}&units=m&type=q", startLoc.Latitude, startLoc.Longitude, endLoc.Latitude, endLoc.Longitude);
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

                    string stepsStartString = "new VE_RouteInstruction";
                    int stepsStart = FindFirstOccurence(memory, stepsStartString, 0);
                    if (stepsStart == -1)
                        throw new Exception("Could not find Directions Steps blob.");
                    stepsStart -= stepsStartString.Length;
                    int stepsEnd = FindFirstOccurence(memory, "]", stepsStart);
                    if (stepsEnd == -1)
                        throw new Exception("Could not find Directions Steps blob.");
                    memory.Seek(stepsStart, SeekOrigin.Begin);
                    char [] charBuffer = new char[stepsEnd - stepsStart - 1];
                    StreamReader reader = new StreamReader(memory);
                    reader.Read(charBuffer, 0, charBuffer.Length);
                    List<Segment> steps = DecodeSteps(charBuffer);

                    int latsStart = FindFirstOccurence(memory, ",'", stepsEnd);
                    if (latsStart == -1)
                        throw new Exception("Could not find lats blob.");
                    int latsEnd = FindFirstOccurence(memory, "','", latsStart);
                    if (latsEnd == -1)
                        throw new Exception("Could not find lats blob.");
                    memory.Seek(latsStart, SeekOrigin.Begin);
                    byte[] subset = new byte[latsEnd - latsStart - 1];
                    memory.Read(subset, 0, subset.Length);
                    MemoryStream ms = new MemoryStream(subset);
                    reader = new StreamReader(ms);
                    List<double> lats = DecodeDoubles(reader.ReadToEnd());

                    int lonsStart = latsEnd;
                    if (lonsStart == -1)
                        throw new Exception("Could not find lons blob.");
                    int lonsEnd = FindFirstOccurence(memory, "',", lonsStart);
                    if (lonsEnd == -1)
                        throw new Exception("Could not find lons blob.");
                    memory.Seek(lonsStart, SeekOrigin.Begin);
                    subset = new byte[lonsEnd - lonsStart - 1];
                    memory.Read(subset, 0, subset.Length);
                    ms = new MemoryStream(subset);
                    reader = new StreamReader(ms, Encoding.UTF8);
                    List<double> lons = DecodeDoubles(reader.ReadToEnd());

                    Directions ret = new Directions();
                    ret.Segments = steps.ToArray();
                    ret.PolyLine = new Geocode[lats.Count];
                    ret.Levels = new int[lats.Count];
                    for (int i = 0; i < lats.Count; i++)
                    {
                        ret.Levels[i] = 3;
                        ret.PolyLine[i] = new Geocode(lats[i], lons[i]);
                    }

                    return ret;
                }
            }
        }
    }
}
