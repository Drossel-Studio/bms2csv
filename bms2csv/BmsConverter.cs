using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace bms2csv
{
    class BmsConverter
    {
        public class BpmChangeTiming
        {
            public long bmsCount;
            public double bpm;
            public long realTimeCount;
        }

        public class Note
        {
            public long Time;
            public int Lane;
            public int Type;
        }

        private static List<Note> CalcAllNotes(Chart chartData)
        {
            CreateRhythmChange(chartData.rhythm, chartData.header.bpm, ref chartData.bpm);
            List<BpmChangeTiming> checkPoint = CalcBpmChangeTiming(chartData.header.bpm, chartData.bpm, chartData);
            foreach (BpmChangeTiming c in checkPoint)
            {
                Console.WriteLine("({0}, {1}, {2})", c.bmsCount, c.bpm, c.realTimeCount);
            }
            Note startNote = GetRealCountMainData(chartData.start, checkPoint, 0L);

            return ConvertBmsCountToRealCount(chartData.main, checkPoint, startNote.Time);
        }

        // 拍子変更に対応するBPM変更を作る
        private static void CreateRhythmChange(List<RhythmChange> rhythmChange, double initialBpm, ref List<BpmChange> bpmChange)
        {
            rhythmChange.Sort((a, b) => a.measure - b.measure);
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            double currentBpm = initialBpm;
            bool rhythmChanged = false;

            int measure = 0;
            int rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
            if (rhythmIndex != -1)
            {
                rhythmChanged = true;

                int bpmIndex = bpmChange.FindIndex(c => ((c.measure == measure) && (c.unit_numer == 0)));
                if (bpmIndex != -1)
                {
                    currentBpm = bpmChange[bpmIndex].bpm;
                    bpmChange[bpmIndex].bpm /= rhythmChange[rhythmIndex].mag;
                }
                else
                {
                    bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });
                }
            }

            foreach (BpmChange change in bpmChange)
            {
                if (change.measure != measure)
                {
                    if (rhythmChanged)
                    {
                        if ((change.measure != (measure + 1)) || (change.unit_numer != 0))
                        {
                            bpmChange.Add(new BpmChange { measure = measure + 1, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * (measure + 1), bpm = currentBpm });
                        }
                    }

                    for (measure = measure + 1; measure < change.measure; measure++)
                    {
                        rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
                        if (rhythmIndex != -1)
                        {
                            rhythmChanged = true;
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });

                            if ((change.measure != (measure + 1)) || (change.unit_numer != 0))
                            {
                                bpmChange.Add(new BpmChange { measure = measure + 1, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * (measure + 1), bpm = currentBpm });
                            }
                        }
                    }

                    measure = change.measure;
                    rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
                    if (rhythmIndex != -1)
                    {
                        rhythmChanged = true;

                        if (change.unit_numer != 0)
                        {
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });
                        }
                    }
                    else
                    {
                        rhythmChanged = false;
                    }
                }

                if (rhythmChanged)
                {
                    currentBpm = change.bpm;
                    change.bpm /= rhythmChange[rhythmIndex].mag;
                }
            }
        }

        // BMSカウントと実時間(ms)の対応表を作る
        private static List<BpmChangeTiming> CalcBpmChangeTiming(double initialBpm, List<BpmChange> bpmChange, Chart chartData)
        {
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));
            List<BpmChangeTiming> changeTimingList = new List<BpmChangeTiming> { new BpmChangeTiming { bmsCount = 0L, bpm = initialBpm, realTimeCount = 0L } };
            double currentBpm = initialBpm;
            foreach (var change in bpmChange)
            {
                BpmChangeTiming beforePoint = changeTimingList[changeTimingList.Count - 1];
                long realTimeCount = (long)((double)(change.bmscnt - beforePoint.bmsCount) / Measure.measureLength * 60 / currentBpm * 4 * 1000 + beforePoint.realTimeCount);
                changeTimingList.Add(new BpmChangeTiming { bmsCount = change.bmscnt, bpm = change.bpm, realTimeCount = realTimeCount });
                currentBpm = change.bpm;
            }

            return changeTimingList;
        }

        private static List<Note> ConvertBmsCountToRealCount(MainData bmsCountMainData, List<BpmChangeTiming> checkpoint, long start)
        {
            List<Note> realCountMainData = new List<Note>();
            bmsCountMainData.obj.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));
            foreach (Object obj in bmsCountMainData.obj)
            {
                realCountMainData.Add(GetRealCountMainData(obj, checkpoint, start));
            }
            return realCountMainData;
        }

        private static Note GetRealCountMainData(Object srcData, IList<BpmChangeTiming> checkpoint, long start)
        {
            long bmsCount = srcData.bmscnt;
            BpmChangeTiming nearestCheckPoint = checkpoint[0];
            foreach (var c in checkpoint)
            {
                if (c.bmsCount < bmsCount)
                {
                    nearestCheckPoint = c;
                }
                else
                {
                    break;
                }
            }
            long realTimeCount = (long)((double)(bmsCount - nearestCheckPoint.bmsCount) / Measure.measureLength * 60 / nearestCheckPoint.bpm * 4 * 1000 + nearestCheckPoint.realTimeCount - start);
            return new Note { Time = realTimeCount, Lane = srcData.lane, Type = srcData.type };
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

                filename = root + ".csv.meta";
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
