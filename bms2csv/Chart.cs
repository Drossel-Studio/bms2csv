using System.Collections.Generic;

namespace bms2csv
{
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
        public int bpm;

        // レベル
        public int playlevel;

        // ランク
        public int rank;
    }

    public class MainData
    {
        public int channel;
        public List<int> data;
        public int line;
    }

    public class BpmChange
    {
        public int line;
        public int[] data;
    }

    public class ScoreWeight
    {
        public float each;
        public float long_note;
        public float normal;
        public float slide;
        public float special;
    }

    public class Chart
    {
        public int start;
        public List<BpmChange> bpm; // 曲中のBPM変化リスト
        public string hash;
        public Header header;
        public List<MainData> main;
        public ScoreWeight notes_weight;
    }
}
