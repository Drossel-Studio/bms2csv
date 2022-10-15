using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace bms2csv
{
    /// <summary>
    /// BMSファイルの読み込みクラス
    /// </summary>
    class BmsReader
    {
        private class Lane
        {
            public int channel;
            public int laneNumber;
        }

        private class BpmHeader
        {
            public int index;
            public double bpm;
        }

        /// <summary>
        /// レーンとチャンネルの対応設定
        /// </summary>
        static readonly private List<Lane> Lanes = new List<Lane>()
        {
            new Lane{channel = 1, laneNumber = 0 },
            new Lane{channel = 11, laneNumber = 5 },
            new Lane{channel = 12, laneNumber = 1 },
            new Lane{channel = 13, laneNumber = 2 },
            new Lane{channel = 14, laneNumber = 3 },
            new Lane{channel = 15, laneNumber = 4 },
            new Lane{channel = 18, laneNumber = 6 },
            new Lane{channel = 19, laneNumber = 7 }
        };

        /// <summary>
        /// 曲の開始点を入力するチャンネル
        /// </summary>
        private const int StartChannel = 1;

        /// <summary>
        /// 拍子変更を入力するチャンネル
        /// </summary>
        private const int ChangeRhythmChannel = 2;

        /// <summary>
        /// BPM変更を入力するチャンネル
        /// </summary>
        private const int ChangeBPMChannel = 3;

        /// <summary>
        /// 拡張BPM変更を入力するチャンネル
        /// </summary>
        private const int ChangeBPMExChannel = 8;

        /// <summary>
        /// BPM変更のヘッダを読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <param name="initialBpm">初期BPMの出力用変数</param>
        /// <returnsヘッダのデータ</returns>
        private static List<BpmHeader> Read_Header_Bpm(string bms, out double initialBpm)
        {
            // 出力変数の初期化
            List<BpmHeader> bpmHeader = new List<BpmHeader>();
            initialBpm = 0;

            // ヘッダのキーの設定
            string key = "bpm";

            // 読み込み
            int head = 0;
            while (head != -1)
            {
                // キーの検索
                string search_key = String.Format("#{0}", key);
                int lastHead = head;
                head = bms.IndexOf(search_key, head + 1);
                if (head == -1)
                {
                    head = bms.IndexOf(search_key.ToUpper(), lastHead + 1);
                }
                if (head == -1)
                {
                    break;
                }

                // データの読み込み
                if (bms.Substring(head + key.Length + 1, 1) != " ")
                {
                    // 初期BPM
                    int index = int.Parse(bms.Substring(head + key.Length + 1, 2), NumberStyles.AllowHexSpecifier);
                    int start = head + key.Length + 3;
                    int end = bms.IndexOf("\r\n", head);
                    double bpm = double.Parse(bms.Substring(start, end - start));
                    bpmHeader.Add(new BpmHeader { index = index, bpm = bpm });
                }
                else
                {
                    // 拡張BPM変更
                    int start = head + key.Length + 1;
                    int end = bms.IndexOf(Environment.NewLine, head);
                    initialBpm = double.Parse(bms.Substring(start, end - start));
                }
            }

            return bpmHeader;
        }

        /// <summary>
        /// ヘッダの読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <param name="key">ヘッダのキー</param>
        /// <returns>ヘッダのデータ</returns>
        static string Read_Header(string bms, string key)
        {
            // キーの検索
            int head = bms.IndexOf($"#{key}", StringComparison.OrdinalIgnoreCase);
            if (head == -1)
            {
                return string.Empty;
            }

            // データの読み込み
            int start = head + key.Length + 1;
            int end = bms.IndexOf(Environment.NewLine, head);
            var result = bms.Substring(start, end - start).Trim();
            return result;
        }

        /// <summary>
        /// ヘッダの読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <param name="key">ヘッダのキー</param>
        /// <returns>ヘッダのデータ</returns>
        static int Read_Header_Int(string bms, string key)
        {
            return int.Parse(Read_Header(bms, key));
        }

        /// <summary>
        /// 数字列を2桁ごとに分割してリストにする
        /// </summary>
        /// <param name="src">数字列</param>
        /// <param name="digit">進数</param>
        /// <returns>分割された数字のリスト</returns>
        static List<int> Slice_Two(string src, int digit = 10)
        {
            // 出力変数の初期化
            List<int> num = new List<int>();

            //数字列の分割
            for (int i = 0; i < src.Length; i += 2)
            {
                string num_text = src.Substring(i, 2);
                NumberStyles style = NumberStyles.None;
                if (digit == 16)
                {
                    style = NumberStyles.AllowHexSpecifier;
                }
                num.Add(int.Parse(num_text, style));
            }

            return num;
        }

        /// <summary>
        /// メインデータの読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <returns>メインデータ</returns>
        static MainData Read_Main(string bms)
        {
            // 出力用変数の初期化
            MainData main_data = new MainData
            {
                obj = new List<BmsObject>()
            };

            // メインデータの読み込み
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (true)
            {
                // データの検索
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }

                // レーンが対象外の場合は除外
                int lane = int.Parse(bms.Substring(head + 4, 2));
                if (!Lanes.Exists(c => c.channel == lane))
                {
                    continue;
                }

                // 小節の読み込み
                int measure = int.Parse(bms.Substring(head + 1, 3));

                // オブジェクトの読み込み
                int slice_start = bms.IndexOf(":", head) + 1;
                int slice_end = bms.IndexOf(Environment.NewLine, head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start), 16);
                int cnt = data.Count;
                for (int j = 0; j < cnt; j++)
                {
                    if ((data[j] == 0) || (data[j] == 1))
                    {
                        continue;
                    }

                    long bmscnt = Measure.measureLength * measure + Measure.measureLength * j / cnt;
                    main_data.obj.Add(new BmsObject { measure = measure, unit_denom = cnt, unit_numer = j, bmscnt = bmscnt, lane = Lanes.Find(c => c.channel == lane).laneNumber, type = data[j] });
                }
            }

            return main_data;
        }

        /// <summary>
        /// 曲の開始点の読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <param name="warning">変換警告の有無を格納する変数</param>
        /// <returns>曲の開始点</returns>
        static BmsObject Read_Start(string bms, ref bool warning)
        {
            // 出力変数の初期化
            BmsObject start = new BmsObject() { measure = 0, unit_denom = 0, unit_numer = 0, bmscnt = 0, lane = 0, type = 1 };
            bool set = false;

            // 曲の開始点の読み込み
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (true)
            {
                // データの検索
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    if (!set)
                    {
                        // 曲の開始点が見つからなかった場合
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: 曲の開始点がありません、0小節目の始まりを曲の開始点とします");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        warning = true;
                    }
                    break;
                }

                // チャンネル番号が異なる場合は除外
                if (int.Parse(bms.Substring(head + 4, 2)) != StartChannel)
                {
                    continue;
                }

                // 曲の開始点の読み込み
                int measure = int.Parse(bms.Substring(head + 1, 3));
                int slice_start = head + 7;
                int slice_end = bms.IndexOf(Environment.NewLine, head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start), 16);
                int cnt = data.Count;
                for (int i = 0; i < cnt; i++)
                {
                    if (data[i] != 1)
                    {
                        continue;
                    }

                    if (set)
                    {
                        // 曲の開始点が複数ある場合
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: 曲の開始点が複数あります、最初の開始点のみが有効になります");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        warning = true;
                        continue;
                    }

                    long bmscnt = Measure.measureLength * measure + Measure.measureLength * i / cnt;
                    start = new BmsObject() { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, lane = 0, type = 1 };
                    set = true;
                }
            }

            return start;
        }

        /// <summary>
        /// 拍子変更の読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <returns>拍子変更のリスト</returns>
        static List<RhythmChange> Read_RhythmChange(string bms)
        {
            // 出力変数の初期化
            List<RhythmChange> rhythmChange = new List<RhythmChange>();

            // 拍子変更の読み込み
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (true)
            {
                // データの検索
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }

                // チャンネル番号が異なる場合は除外
                if (int.Parse(bms.Substring(head + 4, 2)) != ChangeRhythmChannel)
                {
                    continue;
                }

                // 拍子変更の読み込み
                int measure = int.Parse(bms.Substring(head + 1, 3));
                int index = bms.IndexOf(":", head);
                int start = index + 1;
                int end = bms.IndexOf(Environment.NewLine, index);
                double mag = double.Parse(bms.Substring(start, end - start));
                rhythmChange.Add(new RhythmChange { measure = measure, mag = mag });
            }

            return rhythmChange;
        }

        /// <summary>
        /// BPM変更の読み込み
        /// </summary>
        /// <param name="bms">BMSデータ</param>
        /// <returns>BPM変更のリスト</returns>
        static List<BpmChange> Read_BpmChange(string bms, List<BpmHeader> bpmHeader)
        {
            // 出力変数の初期化
            List<BpmChange> bpmChange = new List<BpmChange>();

            // BPM変更の読み込み
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (true)
            {
                // データの検索
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }

                // チャンネル番号が異なる場合は除外
                int channel = int.Parse(bms.Substring(head + 4, 2));
                if ((channel != ChangeBPMChannel) && (channel != ChangeBPMExChannel))
                {
                    continue;
                }

                // BPM変更の読み込み
                int measure = int.Parse(bms.Substring(head + 1, 3));
                int index = bms.IndexOf(":", head);
                int slice_start = index + 1;
                int slice_end = bms.IndexOf(Environment.NewLine, index);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start), 16);
                int cnt = data.Count;
                for (int i = 0; i < cnt; i++)
                {
                    if (data[i] == 0)
                    {
                        continue;
                    }

                    long bmscnt = Measure.measureLength * measure + Measure.measureLength * i / cnt;
                    switch (channel)
                    {
                        case ChangeBPMChannel:
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, bpm = data[i] });
                            break;

                        case ChangeBPMExChannel:
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, bpm = bpmHeader.Find(c => c.index == data[i]).bpm });
                            break;
                    }
                }
            }

            return bpmChange;
        }

        /// <summary>
        ///  BMSファイルの読み込み
        /// </summary>
        /// <param name="filename">ファイルパス</param>
        /// <param name="warning">変換警告の有無を格納する変数</param>
        /// <returns>譜面データ</returns>
        public static Chart Read_Bms(string filename, ref bool warning)
        {
            // 出力変数の初期化
            Chart chart = new Chart();

            // BMSファイルの読み込み
            string bms;
            using (StreamReader bmsf = new StreamReader(filename, Encoding.GetEncoding("Shift_JIS")))
            {
                bms = bmsf.ReadToEnd();
            }

            // ヘッダの読み込み
            chart.header = new Header
            {
                genre = Read_Header(bms, "genre"),
                title = Read_Header(bms, "title"),
                artist = Read_Header(bms, "artist"),
                wav = Read_Header(bms, "wav01"),
                bpm = 0,
                playlevel = Read_Header_Int(bms, "playlevel"),
                rank = Read_Header_Int(bms, "rank")
            };
            List<BpmHeader> bpmHeader = Read_Header_Bpm(bms, out chart.header.bpm);
            if (chart.header.bpm == 0)
            {
                throw new ArgumentException("Error: BPMが不正です");
            }

            // メインデータの読み込み
            chart.main = Read_Main(bms);
            chart.start = Read_Start(bms, ref warning);
            chart.rhythm = Read_RhythmChange(bms);
            chart.bpm = Read_BpmChange(bms, bpmHeader);

            return chart;
        }
    }
}
