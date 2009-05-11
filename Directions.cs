using System; // © 2008 Koushik Dutta - www.koushikdutta.com
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace TiledMaps
{
    public class Segment
    {
        string mySegmentText;

        public string Text
        {
            get { return mySegmentText; }
            set { mySegmentText = value; }
        }
        string mySegmentDistance;

        public string Distance
        {
            get { return mySegmentDistance; }
            set { mySegmentDistance = value; }
        }

        string myTime;

        public string Time
        {
            get { return myTime; }
            set { myTime = value; }
        }

        Geocode myGeocode;

        public Geocode Geocode
        {
            get { return myGeocode; }
            set { myGeocode = value; }
        }
        string myRoadName;

        public string RoadName
        {
            get { return myRoadName; }
            set { myRoadName = value; }
        }

        string myFormattedText;

        public string FormattedText
        {
            get { return myFormattedText; }
            set { myFormattedText = value; }
        }

        string[] myNotes;

        public string[] Notes
        {
            get { return myNotes; }
            set { myNotes = value; }
        }
    }


    public class Route
    {
        public Geocode[] PolyLine
        {
            get;
            set;
        }

        int[] myLevels;

        public int[] Levels
        {
            get { return myLevels; }
            set { myLevels = value; }
        }

        Color myColor = Color.Cyan;
        public Color Color
        {
            get
            {
                return myColor;
            }
            set
            {
                myColor = value;
            }
        }

        float myLineWidth = 4.0f;
        public float LineWidth
        {
            get
            {
                return myLineWidth;
            }
            set
            {
                myLineWidth = value;
            }
        }
    }

    public class Directions : Route
    {
        Segment[] mySegments;

        public Segment[] Segments
        {
            get { return mySegments; }
            set { mySegments = value; }
        }
    }
}
