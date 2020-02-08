using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace bms2csv
{
    class BmsReader
    {
        private class Lane
        {
            public int channel;
            public int laneNumber;
        }

        //レーンとチャンネルの対応設定
        readonly private static List<Lane> Lanes = new List<Lane>()
        {
            new Lane{channel = 11, laneNumber = 5 },
            new Lane{channel = 12, laneNumber = 1 },
            new Lane{channel = 13, laneNumber = 2 },
            new Lane{channel = 14, laneNumber = 3 },
            new Lane{channel = 15, laneNumber = 4 },
            new Lane{channel = 18, laneNumber = 6 },
            new Lane{channel = 19, laneNumber = 7 }
        };

        private class BpmHeader
        {
            public int index;
            public double bpm;
        }

        private static List<BpmHeader> Read_Header_Bpm(string bms, string key, out double initialBpm)
        {
            List<BpmHeader> bpmHeader = new List<BpmHeader>();
            initialBpm = 0;

            int head = 0;
            while (head != -1)
            {
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

                if (bms.Substring(head + key.Length + 1, 1) != " ")
                {
                    int index = int.Parse(bms.Substring(head + key.Length + 1, 2), NumberStyles.AllowHexSpecifier);
                    int start = head + key.Length + 3;
                    int end = bms.IndexOf("\n", head);
                    double bpm = double.Parse(bms.Substring(start, end - start - 1));
                    bpmHeader.Add(new BpmHeader { index = index, bpm = bpm });
                }
                else
                {
                    int start = head + key.Length + 1;
                    int end = bms.IndexOf("\n", head);
                    initialBpm = double.Parse(bms.Substring(start, end - start - 1));
                }
            }

            return bpmHeader;
        }

        static string Read_Header(string bms, string key)
        {
            int head = bms.IndexOf(key);
            if (head == -1)
            {
                head = bms.IndexOf(key.ToUpper());
            }
            if (head == -1)
            {
                return "";
            }
            int start = head + key.Length + 1;
            int end = bms.IndexOf("\n", head);
            return bms.Substring(start, end - start - 1);
        }

        static int Read_Header_Int(string bms, string key)
        {
            return int.Parse(Read_Header(bms, key));
        }

        static List<int> Slice_Two(string src, int digit = 10)
        {
            List<int> num = new List<int>();
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

        static MainData Read_Main(string bms)
        {
            MainData main_data = new MainData();
            main_data.obj = new List<Object>();
            int head = bms.IndexOf("MAIN DATA FIELD");
            int measure = 0;
            while (head != -1)
            {
                int i = 0;
                while (i < Lanes.Count)
                {
                    head = bms.IndexOf("#", head + 1);
                    if (head == -1)
                    {
                        break;
                    }
                    int lane = int.Parse(bms.Substring(head + 4, 2));
                    if (!Lanes.Exists(c => c.channel == lane))
                    {
                        continue;
                    }
                    if ((int.Parse(bms.Substring(head + 1, 3)) != measure) || (lane != Lanes[i].channel))
                    {
                        head--;
                        i++;
                        continue;
                    }
                    int slice_start = bms.IndexOf(":", head) + 1;
                    int slice_end = bms.IndexOf("\n", head);
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1), 16);
                    int cnt = data.Count;
                    for (int j = 0; j < cnt; j++)
                    {
                        if (data[j] == 0)
                        {
                            continue;
                        }
                        long bmscnt = Measure.measureLength * measure + Measure.measureLength * j / cnt;
                        main_data.obj.Add(new Object { measure = measure, unit_denom = cnt, unit_numer = j, bmscnt = bmscnt, lane = Lanes.Find(c => c.channel == lane).laneNumber, type = data[j] });
                    }
                    i += 1;
                }
                measure += 1;
            }
            return main_data;
        }

        static Object Read_Start(string bms)
        {
            Object start = new Object() { measure = 0, unit_denom = 0, unit_numer = 0, bmscnt = 0, lane = 0, type = 1 };
            bool set = false;
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (head != -1)
            {
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    if (!set)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: 曲の開始点がありません、0小節目の始まりを曲の開始点とします");
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }
                    break;
                }
                if (int.Parse(bms.Substring(head + 4, 2)) != 1)
                {
                    continue;
                }
                int measure = int.Parse(bms.Substring(head + 1, 3));
                int slice_start = head + 7;
                int slice_end = bms.IndexOf("\n", head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1), 16);

                int cnt = data.Count;
                for (int i = 0; i < cnt; i++)
                {
                    if (data[i] != 1)
                    {
                        continue;
                    }
                    if (set)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: 曲の開始点が複数あります、最初の開始点のみが有効になります");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        continue;
                    }
                    long bmscnt = Measure.measureLength * measure + Measure.measureLength * i / cnt;
                    start = new Object() { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, lane = 0, type = 1 };
                    set = true;
                }
            }

            return start;
        }
        static List<RhythmChange> Read_RhythmChange(string bms)
        {
            List<RhythmChange> rhythmChange = new List<RhythmChange>();
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (head != -1)
            {
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }
                int channel = int.Parse(bms.Substring(head + 4, 2));
                if ((channel == 2))
                {
                    int measure = int.Parse(bms.Substring(head + 1, 3));
                    int index = bms.IndexOf(":", head);
                    int start = index + 1;
                    int end = bms.IndexOf("\n", index);
                    double mag = double.Parse(bms.Substring(start, end - start - 1));
                    rhythmChange.Add(new RhythmChange { measure = measure, mag = mag });
                }
            }

            return rhythmChange;
        }

        static List<BpmChange> Read_BpmChange(string bms, List<BpmHeader> bpmHeader)
        {
            List<BpmChange> bpmChange = new List<BpmChange>();
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (head != -1)
            {
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }
                int channel = int.Parse(bms.Substring(head + 4, 2));
                if ((channel == 3) || (channel == 8))
                {
                    int measure = int.Parse(bms.Substring(head + 1, 3));
                    int index = bms.IndexOf(":", head);
                    int slice_start = index + 1;
                    int slice_end = bms.IndexOf("\n", index);
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1), 16);

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
                            case 3:
                                bpmChange.Add(new BpmChange { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, bpm = data[i] });
                                break;
                            case 8:
                                bpmChange.Add(new BpmChange { measure = measure, unit_denom = cnt, unit_numer = i, bmscnt = bmscnt, bpm = bpmHeader.Find(c => c.index == data[i]).bpm });
                                break;
                        }
                    }
                }
            }

            return bpmChange;
        }

        public static Chart Read_Bms(string filename)
        {
            Chart chart = new Chart();
            string bms;
            using (StreamReader bmsf = new StreamReader(filename, Encoding.GetEncoding("Shift_JIS")))
            {
                bms = bmsf.ReadToEnd();
            }

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
            List<BpmHeader> bpmHeader = Read_Header_Bpm(bms, "bpm", out chart.header.bpm);
            if (chart.header.bpm == 0)
            {
                throw new ArgumentException("Error: BPMが不正です");
            }

            chart.main = Read_Main(bms);
            chart.start = Read_Start(bms);
            chart.rhythm = Read_RhythmChange(bms);
            chart.bpm = Read_BpmChange(bms, bpmHeader);

            return chart;
        }
    }
}
