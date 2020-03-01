using System;
using System.Collections.Generic;

namespace bms2csv
{
    static public class Measure
    {
        public const long measureLength = 9600;
    }

    public class Header
    {
        public string genre;
        public string title;
        public string artist;
        public string wav;
        public double bpm;
        public int playlevel;
        public int rank;
    }

    public class BmsObject
    {
        public int measure;
        public int unit_denom;
        public int unit_numer;
        public long bmscnt;
        public int lane;
        public int type;
    }

    public class MainData
    {
        public List<BmsObject> obj;
    }

    public class RhythmChange
    {
        public int measure;
        public double mag;
    }

    public class BpmChange
    {
        public int measure;
        public int unit_denom;
        public int unit_numer;
        public long bmscnt;
        public double bpm;
    }

    public class Chart
    {
        public Header header;
        public List<RhythmChange> rhythm;
        public List<BpmChange> bpm;
        public BmsObject start;
        public MainData main;
    }
}
