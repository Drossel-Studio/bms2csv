using System;
using System.Collections.Generic;

namespace bms2csv
{
    public class Measure
    {
        public const long measureLength = 9600;
    }

    public class Header
    {
        // ジャンル
        public string genre;

        // タイトル
        public string title;

        // アーティスト
        public string artist;

        // Waveファイル名
        public string wav;

        // 初期BPM
        public double bpm;

        // レベル
        public int playlevel;

        // ランク
        public int rank;
    }

    public class Object
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
        public List<Object> obj;
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
        public List<RhythmChange> rhythm; // 曲中のBPM変化リスト
        public List<BpmChange> bpm; // 曲中のBPM変化リスト
        public Object start;
        public MainData main;
    }
}
