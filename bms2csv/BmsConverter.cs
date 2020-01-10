using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace bms2csv
{
    class BmsConverter
    {
        public class Note
        {
            public long Time;
            public int Lane;
            public int Type;
        }

        private static List<Note> CalcAllNotes(Chart chartData)
        {
            // BMSカウントのままのデータを先に生成する
            List<Note> bmsCountMainData = new List<Note>();
            List<MainData> mainData = chartData.main;
            foreach (MainData lineData in mainData)
            {
                GetMainData(lineData, ref bmsCountMainData);
            }

            List<Tuple<double, double, long>> checkPoint = CalcBpmChangeTiming(chartData.header.bpm, chartData.bpm, chartData);
            foreach (Tuple<double, double, long> c in checkPoint)
            {
                Console.WriteLine(c);
            }

            return ConvertBmsCountToRealCount(bmsCountMainData, checkPoint, chartData.start);
        }

        private static void GetMainData(MainData chartMainData, ref List<Note> mainData)
        {
            const int lineLength = 9600;
            if (chartMainData.data.Count == 0)
            {
                return;
            }
            int unit = lineLength / chartMainData.data.Count;
            for (int i = 0; i < chartMainData.data.Count; i++)
            {
                int numData = Convert.ToInt32(chartMainData.data[i]);
                if (numData == 0)
                {
                    continue;
                }

                mainData.Add(new Note { Time = i * unit + chartMainData.line * lineLength, Lane = chartMainData.channel, Type = numData });
            }
        }

        // BMSカウントと実時間(ms)の対応表を作る
        private static List<Tuple<double, double, long>> CalcBpmChangeTiming(double initialBpm, List<BpmChange> bpmChangeArray, Chart chartData)
        {
            bpmChangeArray.Sort((a, b) => a.line - b.line);
            List<Tuple<double, double, long>> changeTimingList = new List<Tuple<double, double, long>> { Tuple.Create(0.0, initialBpm, 0L) };
            double currentBpm = initialBpm;
            foreach (var change in bpmChangeArray)
            {
                int lineHeadBmsCount = change.line * 9600;
                int cnt = change.data.Length;
                for (var i = 0; i < cnt; i++)
                {
                    if (change.data[i] == 0)
                    {
                        continue;
                    }

                    double bpm = change.index ? chartData.bpmHeader.Find(n => n.Item1 == change.data[i]).Item2 : change.data[i];

                    Tuple<double, double, long> beforePoint = changeTimingList[changeTimingList.Count - 1];
                    double bmscnt = lineHeadBmsCount + 9600.0 * i / cnt;
                    long realTimeCount = (long)((bmscnt - beforePoint.Item1) / 9600.0 * 60 / currentBpm * 4 * 1000 + beforePoint.Item3);
                    changeTimingList.Add(Tuple.Create(bmscnt, bpm, realTimeCount));
                    currentBpm = bpm;
                }
            }
            return changeTimingList;
        }

        private static List<Note> ConvertBmsCountToRealCount(List<Note> bmsCountMainData, List<Tuple<double, double, long>> checkpoint, int start)
        {
            for (var j = 0; j < bmsCountMainData.Count; j++)
            {
                bmsCountMainData[j] = GetRealCountMainData(bmsCountMainData[j], checkpoint, start);
            }
            return bmsCountMainData;
        }

        private static Note GetRealCountMainData(Note srcData, IList<Tuple<double, double, long>> checkpoint, int start)
        {
            long bmsCount = srcData.Time;
            Tuple<double, double, long> nearestCheckPoint = checkpoint[0];
            foreach (var t in checkpoint)
            {
                if (t.Item1 < bmsCount)
                {
                    nearestCheckPoint = t;
                }
                else
                {
                    break;
                }
            }
            long realTimeCount = (long)((bmsCount - nearestCheckPoint.Item1) / 9600.0 * 60 / nearestCheckPoint.Item2 * 4 * 1000 + nearestCheckPoint.Item3 - start);
            return new Note { Time = realTimeCount, Lane = srcData.Lane, Type = srcData.Type };
        }


        public static bool Convert_Bms(string f, string outputPath, out string exportPath)
        {
            exportPath = "";

            try
            {
                Chart chartData = BmsReader.Read_Bms(f);
                List<Note> allNotes = CalcAllNotes(chartData);

                string path;
                string filename;
                string root = Path.GetFileNameWithoutExtension(f);
                if (outputPath == "")
                {
                    path = @"./csv";
                }
                else
                {
                    path = outputPath;
                }
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                filename = root + ".csv";
                exportPath = Path.Combine(path, filename);
                Encoding encode = new UTF8Encoding(false);

                using (StreamWriter output = new StreamWriter(exportPath, false, encode))
                {
                    foreach (Note note in allNotes)
                    {
                        output.WriteLine("{0},{1},{2}", note.Time, note.Lane, note.Type);
                    }
                }

                filename = root + ".meta";
                exportPath = Path.Combine(path, filename);

                using (MemoryStream ms = new MemoryStream())
                {
                    using (StreamReader sr = new StreamReader(ms))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(Header));
                        serializer.WriteObject(ms, chartData.header);
                        ms.Position = 0;

                        var json = sr.ReadToEnd();

                        using (StreamWriter output = new StreamWriter(exportPath, false, encode))
                        {
                            output.WriteLine($"{json}");
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return false;
            }
        }
    }
}
