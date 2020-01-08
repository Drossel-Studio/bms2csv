using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace bms2csv
{
    class BmsReader
    {
        private static List<Tuple<int, int>> Lanes = new List<Tuple<int, int>>
        {
            new Tuple<int, int>(11, 5),
            new Tuple<int, int>(12, 1),
            new Tuple<int, int>(13, 2),
            new Tuple<int, int>(14, 3),
            new Tuple<int, int>(15, 4),
            new Tuple<int, int>(18, 6),
            new Tuple<int, int>(19, 7)
        };

        static string GetWav(string bms, string key, int head)
        {
            string wav = "";

            if (head != -1)
            {
                int start = head + key.Length + 1;
                int end = bms.IndexOf("\n", head);
                wav = bms.Substring(start, end - start - 1);
            }

            return wav;
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
                return "NONE";
            }
            if (key == "WAV01")
            {
                return GetWav(bms, key, head);
            }
            int start = head + key.Length + 1;
            int end = bms.IndexOf("\n", head);
            return bms.Substring(start, end - start - 1);
        }

        static int Read_Header_Int(string bms, string key)
        {
            if (int.TryParse(Read_Header(bms, key), out int res))
            {
                return res;
            }

            return 0;
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
                if (int.TryParse(num_text, style, CultureInfo.InvariantCulture, out int res))
                {
                    num.Add(res);
                }
            }
            return num;
        }

        static List<MainData> Read_Main(string bms)
        {
            List<MainData> main_data = new List<MainData>();
            int head = bms.IndexOf("MAIN DATA FIELD");
            int measure = 0;
            while (head != -1)
            {
                //Console.WriteLine("MAIN");
                int i = 0;
                while (i < Lanes.Count)
                {
                    //Console.WriteLine(i);
                    head = bms.IndexOf("#", head + 1);
                    if (head == -1)
                    {
                        break;
                    }
                    //Console.WriteLine(head);
                    int lane = int.Parse(bms.Substring(head + 4, 2));
                    if (!Lanes.Exists(t => t.Item1 == lane))
                    {
                        // Console.WriteLine("NOT LANE")
                        continue;
                    }
                    //Console.WriteLine(int.Parse(bms.Substring(head + 1, 3)) != measure);
                    //Console.WriteLine("{0}, {1}", lane, i);
                    //Console.WriteLine(lane != i)
                    if ((int.Parse(bms.Substring(head + 1, 3)) != measure) || (lane != Lanes[i].Item1))
                    {
                        head--;
                        i++;
                        continue;
                    }
                    int slice_start = bms.IndexOf(":", head) + 1;
                    int slice_end = bms.IndexOf("\n", head);
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1));
                    main_data.Add(new MainData { line = measure, channel = Lanes.Find(t => t.Item1 == lane).Item2, data = data });
                    i += 1;
                }
                measure += 1;
            }
            return main_data;
        }

        static int Read_Start(string bms, int initialBpm)
        {
            if (initialBpm == 0)
            {
                throw new ArgumentException("Error: BPMが不正です");
            }
            int head = bms.IndexOf("MAIN DATA FIELD");
            while (head != -1)
            {
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    Console.WriteLine("Warning: 曲の開始点がありません、1小節目の始まりを曲の開始点とします");
                    break;
                }
                if (int.Parse(bms.Substring(head + 4, 2)) != 1)
                {
                    continue;
                }
                int line = int.Parse(bms.Substring(head + 1, 3));
                int slice_start = head + 7;
                int slice_end = bms.IndexOf("\n", head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1));
                // 1小節の秒数
                double one_line_time = 60.0 / initialBpm * 4;
                double before_line_time = one_line_time * line;
                double current_line_time = one_line_time * data.IndexOf(1) / data.Count;
                return (int)((before_line_time + current_line_time) * 1000);
            }

            return 0;
        }

        static List<BpmChange> Read_BpmChange(string bms)
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
                if ((int.Parse(bms.Substring(head + 4, 2))) == 3)
                {
                    int line = int.Parse(bms.Substring(head + 1, 3));
                    int index = bms.IndexOf(":", head);
                    int slice_start = index + 1;
                    int slice_end = bms.IndexOf("\n", index);
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start - 1), 16);
                    bpmChange.Add(new BpmChange { line = line, data = data.ToArray() });
                }
            }
            return bpmChange;
        }

        public static Chart Read_Bms(string filename)
        {
            //string[] header_string_list = { "genre", "title", "artist", "wav" };
            //string[] header_integer_list = { "bpm", "playlevel", "rank" };

            Chart chart = new Chart();
            string bms;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
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
                bpm = Read_Header_Int(bms, "bpm"),
                playlevel = Read_Header_Int(bms, "playlevel"),
                rank = Read_Header_Int(bms, "rank")
            };

            chart.main = Read_Main(bms);
            chart.start = Read_Start(bms, chart.header.bpm);
            chart.bpm = Read_BpmChange(bms);

            return chart;
        }
    }
}
