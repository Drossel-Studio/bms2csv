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
        public struct NoteType
        {
            public string name;
            public int index;
        }

        static string GetWav(string bms, string key, int head)
        {
            string wav = "";

            while (head != -1)
            {
                int start = head + key.Length + 3;
                int end = bms.IndexOf("\n", head);
                wav += bms.Substring(start, end -start) + ",";
                string search_key = String.Format("#{0}", key);
                head = bms.IndexOf(search_key, head + 1);
                if (head == -1)
                {
                    head = bms.IndexOf(search_key.ToUpper());
                }
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
            return bms.Substring(start, end - start);
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
                int i = 11;
                while (i < 14)
                {
                    //Console.WriteLine(i);
                    head = bms.IndexOf("#", head + 1);
                    if (head == -1)
                    {
                        break;
                    }
                    //Console.WriteLine(head);
                    int lane = int.Parse(bms.Substring(head + 4, 2));
                    if ((lane < 11) || (lane > 14))
                    {
                        // print("NOT LANE")
                        continue;
                    }
                    //Console.WriteLine(int.Parse(bms.Substring(head + 1, 3)) != measure);
                    //Console.WriteLine("{0}, {1}", lane, i);
                    //Console.WriteLine(lane != i)
                    if ((int.Parse(bms.Substring(head + 1, 3)) != measure) || (lane != i))
                    {
                        head--;
                        i += 1;
                        continue;
                    }
                    int slice_start = bms.IndexOf(":", head) + 1;
                    int slice_end = bms.IndexOf("\n", head);
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start));
                    main_data.Add(new MainData { line = measure, channel = lane - 11, data = data });
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
                if (int.Parse(bms.Substring(head + 4, 2)) != 1)
                {
                    continue;
                }
                int line = int.Parse(bms.Substring(head + 1, 3));
                int slice_start = head + 7;
                int slice_end = bms.IndexOf("\n", head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start));
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
                    List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start), 16);
                    bpmChange.Add(new BpmChange { line = line, data = data.ToArray() });
                }
            }
            return bpmChange;
        }

        static void PrintNoteRate(string name, int sum, int allsum)
        {
            float rate = (float)(sum / allsum * 100.0);
            Console.WriteLine("{0,-8}: {1,3} ({2:p1})", name, sum, rate);
        }

        static ScoreWeight Calc_Notes_Weight(string bms)
        {
            ScoreWeight notes_weight = new ScoreWeight();
            int head = bms.IndexOf("MAIN DATA FIELD");
            // notesnum[i] 添え字が実際のbmsファイルのノーツ番号と対応
            int[] notesnum = { 0, 0, 0, 0, 0, 0, 0, 0 };
            int notessum = 0;
            while (head != -1)
            {
                head = bms.IndexOf("#", head + 1);
                if (head == -1)
                {
                    break;
                }

                int lane = int.Parse(bms.Substring(head + 4, 2));
                if ((lane < 11) || (lane > 14))
                {
                    continue;
                }

                int slice_start = bms.IndexOf(":", head) + 1;
                int slice_end = bms.IndexOf("\n", head);
                List<int> data = Slice_Two(bms.Substring(slice_start, slice_end - slice_start));

                foreach (int notes in data)
                {
                    if (notes == 0)
                    {
                        continue;
                    }
                    notesnum[notes]++;
                    notessum++;
                }
            }

            List<NoteType> noteType = new List<NoteType>
            {
                new NoteType { name = "normal", index = 2 },
                new NoteType { name = "red", index = 3 },
                new NoteType { name = "long", index = 4 },
                new NoteType { name = "slide", index = 6 },
                new NoteType { name = "special", index = 7 }
            };

            Console.WriteLine("---notesrate-------------");
            foreach (NoteType type in noteType)
            {
                PrintNoteRate(type.name, notesnum[type.index], notessum);
            }
            Console.WriteLine("-------------------------");

            notes_weight.normal = 1;
            notes_weight.each = 2;
            notes_weight.long_note = 2;
            notes_weight.slide = 0.5f;
            notes_weight.special = 5;

            if ((notesnum[5] + notesnum[6]) == 0)
            {
                return notes_weight;
            }

            double slide_weight = (notes_weight.normal * notesnum[2] + notes_weight.each * notesnum[3] * 0.6) / (notesnum[5] + notesnum[6]);
            if (slide_weight < 0.5f)
            {
                notes_weight.slide = (float)Math.Round(slide_weight, 3);
                Console.WriteLine("slide_weight is corrected");
            }
            return notes_weight;
        }

        public static Chart Read_Bms(string filename)
        {
            //string[] header_string_list = { "genre", "title", "artist", "wav" };
            //string[] header_integer_list = { "bpm", "playlevel", "rank" };

            Chart chart = new Chart();
            StreamReader bmsf = new StreamReader(filename);
            string bms = bmsf.ReadToEnd();
            bmsf.Close();

            chart.header = new Header
            {
                genre = Read_Header(bms, "genre"),
                title = Read_Header(bms, "title"),
                artist = Read_Header(bms, "artist"),
                wav = Read_Header(bms, "wav"),
                bpm = Read_Header_Int(bms, "bpm"),
                playlevel = Read_Header_Int(bms, "playlevel"),
                rank = Read_Header_Int(bms, "rank")
            };

            chart.main = Read_Main(bms);
            chart.start = Read_Start(bms, chart.header.bpm);
            chart.bpm = Read_BpmChange(bms);
            chart.notes_weight = Calc_Notes_Weight(bms);

            Console.WriteLine("Notes weight");
            Console.WriteLine("Each: {0}", chart.notes_weight.each);
            Console.WriteLine("Long note: {0}", chart.notes_weight.long_note);
            Console.WriteLine("Normal: {0}", chart.notes_weight.normal);
            Console.WriteLine("Slide: {0}", chart.notes_weight.slide);
            Console.WriteLine("Special: {0}", chart.notes_weight.special);

            //BMSファイルのMD5ハッシュを取得
            byte[] bmsdata = Encoding.UTF8.GetBytes(bms);
            MD5CryptoServiceProvider md5Hasher = new MD5CryptoServiceProvider();
            byte[] md5data = md5Hasher.ComputeHash(bmsdata);
            md5Hasher.Clear();
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < md5data.Length; i++)
            {
                sBuilder.Append(md5data[i].ToString("x2"));
            }
            chart.hash = sBuilder.ToString();
            Console.WriteLine("Hash: {0}", chart.hash);

            return chart;
        }
    }
}
