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

        private enum NoteErrorFlag
        {
            None = 0,
            InvalidTime = 0b1,
            InvalidType = 0b10,
            NoLongPair = 0b100,
            NoSlidePair = 0b1000,
            NoSlideParent = 0b10000
        }

        private class NoteTypeRange
        {
            public int lane;
            public int from;
            public int to;
        }

        readonly private static List<NoteTypeRange> NoteTypeRanges = new List<NoteTypeRange>()
        {
            new NoteTypeRange {lane = 1, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 2, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 3, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 4, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 5, from = 0x10, to = 0x1F},
            new NoteTypeRange {lane = 6, from = 0x10, to = 0x1F},
            new NoteTypeRange {lane = 7, from = 0x20, to = 0x2F}
        };

        private class LaneName
        {
            public int lane;
            public string name;
        }

        readonly private static List<LaneName> LaneNames = new List<LaneName>()
            {
            new LaneName{lane = 1, name = "1"},
            new LaneName{lane = 2, name = "2"},
            new LaneName{lane = 3, name = "3"},
            new LaneName{lane = 4, name = "4"},
            new LaneName{lane = 5, name = "L"},
            new LaneName{lane = 6, name = "R"},
            new LaneName{lane = 7, name = "SP"}
            };

        private enum NoteType
        {
            NormalNote = 0x02,
            LongNote = 0x03,
            SlideParentNote1 = 0x04,
            SlideChildNote1 = 0x05,
            SlideParentNote2 = 0x06,
            SlideChildNote2 = 0x07,
            FlickUpNote = 0x10,
            FlickDownNote = 0x11,
            SpecialNote = 0x20
        }

        private class ObjectPair
        {
            public int type;
            public int ID;
            public int startID;
            public int endID;
            public int nextID;
        }

        //ペアノーツリストの作成
        private static List<ObjectPair> CreatePairList(List<Object> obj, ref NoteErrorFlag[] errorFlag)
        {
            int pairNum;            //登録したペアリストの数
            int firstPair;          //スライド親ノーツのペアリストインデックス（スライドノーツ登録中に使用）
            NoteType parentType;    //親のノーツ種類
            NoteType childType;     //親のノーツ種類
            bool reg;               //すでにペアリストに登録したか否か

            //オブジェのソート
            obj.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            //ペアリストの初期化
            List<ObjectPair> pair = new List<ObjectPair>();
            pairNum = 0;

            //ペアリストの作成
            for (int i = 0; i < obj.Count; i++)
            {
                switch ((NoteType)obj[i].type)
                {
                    //ロングノーツ
                    case NoteType.LongNote:
                        //登録済みかを検索
                        reg = false;
                        foreach (ObjectPair p in pair)
                        {
                            if (p.endID == i)
                            {
                                reg = true;
                                break;
                            }
                        }
                        if (reg)
                        {
                            continue;
                        }

                        //ペアリストを作成
                        pair.Add(new ObjectPair { type = obj[i].type, ID = i, startID = i, endID = -1, nextID = -1 });

                        for (int j = i + 1; j < obj.Count; j++)
                        {
                            //ペアとなるロングノーツを検索

                            //対象でなければ除外
                            //レーンが異なる
                            if (obj[i].lane != obj[j].lane)
                            {
                                continue;
                            }
                            //種類が異なる
                            if ((NoteType)obj[j].type != NoteType.LongNote)
                            {
                                continue;
                            }

                            //ペアとして登録
                            pair[pairNum].endID = j;
                            pair[pairNum].nextID = j;
                            break;
                        }

                        //ペアなしフラグを立てる
                        if (pair[pairNum].endID == -1)
                        {
                            errorFlag[pair[pairNum].ID] |= NoteErrorFlag.NoLongPair;
                        }

                        //登録完了
                        pairNum++;
                        break;

                    //スライドノーツ
                    case NoteType.SlideParentNote1:
                    case NoteType.SlideParentNote2:
                        parentType = NoteType.SlideParentNote1;
                        childType = NoteType.SlideChildNote1;
                        if ((NoteType)obj[i].type == NoteType.SlideParentNote2)
                        {
                            parentType = NoteType.SlideParentNote2;
                            childType = NoteType.SlideChildNote2;
                        }

                        //登録済みかを検索
                        reg = false;
                        foreach (ObjectPair p in pair)
                        {
                            if (p.endID == i)
                            {
                                reg = true;
                                break;
                            }
                        }
                        if (reg)
                        {
                            continue;
                        }

                        //ペアリストを作成
                        firstPair = pairNum;
                        pair.Add(new ObjectPair { type = obj[i].type, ID = i, startID = i, endID = -1, nextID = -1 });

                        for (int j = i + 1; j < obj.Count; j++)
                        {
                            //ペアとなるノーツを検索

                            //対象でなければ除外
                            //種類が異なる
                            if (((NoteType)obj[j].type != parentType) && ((NoteType)obj[j].type != childType))
                            {
                                continue;
                            }

                            //スライド子ノーツの場合
                            if ((NoteType)obj[j].type == childType)
                            {
                                //ペアとして登録
                                pair[pairNum].nextID = j;
                                pairNum++;

                                //子ノーツ用のペアリストを作成
                                pair.Add(new ObjectPair { type = obj[j].type, ID = j, startID = pair[firstPair].startID, endID = -1, nextID = -1 });
                            }

                            //スライド親ノーツの場合
                            if ((NoteType)obj[j].type == parentType)
                            {
                                //ペアとして登録
                                pair[pairNum].nextID = j;
                                for (int k = firstPair; k <= pairNum; k++)
                                {
                                    pair[k].endID = j;
                                }
                                break;
                            }
                        }

                        //ペアなしフラグを立てる
                        for (int k = firstPair; k <= pairNum; k++)
                        {
                            if (pair[k].endID == -1)
                            {
                                errorFlag[pair[k].ID] |= NoteErrorFlag.NoSlidePair;
                            }
                        }

                        //登録完了
                        pairNum++;
                        break;

                    //スライド子ノーツ（スライド親ノーツに挟まれていないノーツを検出する）
                    case NoteType.SlideChildNote1:
                    case NoteType.SlideChildNote2:
                        //登録済みかを検索
                        reg = false;
                        foreach (ObjectPair p in pair)
                        {
                            if (p.nextID == i)
                            {
                                reg = true;
                                break;
                            }
                        }
                        if (reg)
                        {
                            continue;
                        }

                        //ペアなしフラグを立てる
                        errorFlag[i] |= NoteErrorFlag.NoSlideParent;
                        break;
                }
            }

            return pair;
        }

        private static void CheckChart(Chart chartData)
        {
            int cnt = chartData.main.obj.Count;
            NoteErrorFlag[] errorFlag = new NoteErrorFlag[cnt];
            for (int i = 0; i < cnt; i++)
            {
                errorFlag[i] = NoteErrorFlag.None;
            }

            for (int i = 0; i < cnt; i++)
            {
                if (chartData.main.obj[i].bmscnt < chartData.start.bmscnt)
                {
                    errorFlag[i] |= NoteErrorFlag.InvalidTime;
                }

                NoteTypeRange validNoteTypeRange = NoteTypeRanges.Find(c => c.lane == chartData.main.obj[i].lane);
                if ((chartData.main.obj[i].type < validNoteTypeRange.from) || (chartData.main.obj[i].type > validNoteTypeRange.to))
                {
                    errorFlag[i] |= NoteErrorFlag.InvalidType;
                }

                CreatePairList(chartData.main.obj, ref errorFlag);
            }

            for (int i = 0; i < cnt; i++)
            {
                if (errorFlag[i] == NoteErrorFlag.None)
                {
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                if ((errorFlag[i] & NoteErrorFlag.InvalidTime) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("曲の開始点より前にノーツがあります");
                }
                if ((errorFlag[i] & NoteErrorFlag.InvalidType) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ノーツ種類{0:x2}が{1}レーンにあります", chartData.main.obj[i].type, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                }
                if ((errorFlag[i] & NoteErrorFlag.NoLongPair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないロングノーツがあります");
                }
                if ((errorFlag[i] & NoteErrorFlag.NoSlidePair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないスライドノーツがあります");
                }
                if ((errorFlag[i] & NoteErrorFlag.NoSlideParent) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("スライド親ノーツのないスライド子ノーツがあります");
                }
            }
            Console.ForegroundColor = ConsoleColor.Gray;
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
                CheckChart(chartData);
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ForegroundColor = ConsoleColor.Gray;
                return false;
            }
        }
    }
}
