using System;
using System.Collections.Generic;
using System.IO;

namespace bms2csv
{
    class BmsConverter
    {
        public class NoteBase
        {
            public long Time { get; private set; }

            public int Type { get; private set; }

            public NoteBase(int type, long time)
            {
                Type = type;
                Time = time;
            }
        }

        static class Play
        {
            public static readonly int LineCount = 3;
        }

        class BmsChartToPlayMusicConverter
        {
            public static PlayMusicData RunConvert(Chart chartData)
            {
                // BMSカウントで取得したデータを実時間に変換する
                var allNotes = CalcAllNotes(chartData);

                return new PlayMusicData(allNotes);
            }

            private static List<NoteBase>[] CalcAllNotes(Chart chartData)
            {
                // BMSカウントのままのデータを先に生成する
                var bmsCountMainData = new List<NoteBase>[Play.LineCount];
                for (var i = 0; i < 3; i++)
                {
                    bmsCountMainData[i] = new List<NoteBase>();
                }

                var mainData = chartData.main;
                foreach (var lineData in mainData)
                {
                    GetMainData(bmsCountMainData, lineData);
                }

                var checkPoint = CalcBpmChangeTiming(chartData.header.bpm, chartData.bpm);
                foreach (var c in checkPoint)
                {
                    Console.WriteLine(c);
                }

                return ConvertBmsCountToRealCount(bmsCountMainData, checkPoint);
            }

            private static void GetMainData(IList<List<NoteBase>> mainData, MainData chartMainData)
            {
                const int lineLength = 9600;
                if (chartMainData.data.Count == 0) return;
                var unit = lineLength / chartMainData.data.Count;
                for (var i = 0; i < chartMainData.data.Count; i++)
                {
                    var numData = Convert.ToInt32(chartMainData.data[i]);
                    if (numData == 0)
                    {
                        continue;
                    }

                    mainData[chartMainData.channel].Add(new NoteBase(numData, i * unit + chartMainData.line * lineLength));
                }
            }

            // BMSカウントと実時間(ms)の対応表を作る
            private static List<Tuple<int, int, long>> CalcBpmChangeTiming(int initialBpm, List<BpmChange> bpmChangeArray)
            {
                bpmChangeArray.Sort((a, b) => a.line - b.line);
                var changeTimingList = new List<Tuple<int, int, long>> { Tuple.Create(0, initialBpm, 0L) };
                var currentBpm = initialBpm;
                foreach (var change in bpmChangeArray)
                {
                    var lineHeadBmsCount = change.line * 9600;
                    var cnt = change.data.Length;
                    for (var i = 0; i < cnt; i++)
                    {
                        if (change.data[i] == 0)
                        {
                            continue;
                        }
                        var beforePoint = changeTimingList[changeTimingList.Count - 1];
                        var bmscnt = lineHeadBmsCount + 9600 * i / cnt;
                        var realTimeCount = (bmscnt - beforePoint.Item1) / 9600f * 60 / currentBpm * 4 * 1000 + beforePoint.Item3;
                        changeTimingList.Add(Tuple.Create(bmscnt, change.data[i], (long)realTimeCount));
                        currentBpm = change.data[i];
                    }
                }
                return changeTimingList;
            }

            private static List<NoteBase>[] ConvertBmsCountToRealCount(List<NoteBase>[] bmsCountMainData, List<Tuple<int, int, long>> checkpoint)
            {
                var mainData = bmsCountMainData;
                foreach (var t in mainData)
                {
                    for (var j = 0; j < t.Count; j++)
                    {
                        t[j] = GetRealCountMainData(t[j], checkpoint);
                    }
                }
                return mainData;
            }

            private static NoteBase GetRealCountMainData(NoteBase srcData, IList<Tuple<int, int, long>> checkpoint)
            {
                var bmsCount = srcData.Time;
                var nearestCheckPoint = checkpoint[0];
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
                var realTimeCount = (long)((bmsCount - nearestCheckPoint.Item1) / 9600f * 60 / nearestCheckPoint.Item2 * 4 * 1000) + nearestCheckPoint.Item3;
                return new NoteBase(srcData.Type, realTimeCount);
            }
        }

        // ゲーム内で読み込む譜面データ
        // 実時間形式になっている
        public class PlayMusicData
        {
            // 全てのスコアに関係するノーツ
            public readonly List<NoteBase>[] Notes;

            public PlayMusicData(List<NoteBase>[] allNotes)
            {
                Notes = allNotes;
            }
        }

        public static bool Convert_Bms(string f, string outputPath, out string exportPath)
        {
            exportPath = "";

            try
            {
                Chart chartData = BmsReader.Read_Bms(f);
                PlayMusicData playMusicData = BmsChartToPlayMusicConverter.RunConvert(chartData);

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

                filename = root + ".csv";
                exportPath = Path.Combine(path, filename);

                StreamWriter output = new StreamWriter(exportPath);
                //output.Write(jsondata);
                output.Close();

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }
    }
}
