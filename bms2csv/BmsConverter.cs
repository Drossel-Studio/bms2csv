using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace bms2csv
{
    /// <summary>
    /// BMSをCSVに変換するクラス
    /// </summary>
    class BmsConverter
    {
        public class Note
        {
            public long Time;
            public int Lane;
            public int Type;
        }

        private class NoteTypeRange
        {
            public int lane;
            public int from;
            public int to;
        }

        private class ObjectPair
        {
            public int type;
            public int ID;
            public int startID;
            public int endID;
            public int nextID;
        }

        private class LaneName
        {
            public int lane;
            public string name;
        }

        public class BpmChangeTiming
        {
            public long bmsCount;
            public double bpm;
            public long realTimeCount;
        }

        private enum NoteType
        {
            NormalNote = 0x02,
            LongNote = 0x03,
            SlideParentNote1 = 0x04,
            SlideChildNote1 = 0x05,
            SlideParentNote2 = 0x06,
            SlideChildNote2 = 0x07,
            PairNote = 0x08,
            FlickUpNote = 0x10,
            FlickDownNote = 0x11,
            FlickNote = 0x12,
            SpecialNote = 0x20,
            SpecialFlickRightNote = 0x21,
            SpecialFlickUpperRightNote = 0x22,
            SpecialFlickLowerRightNote = 0x23,
            RainbowNote = 0x24,
            BPMChange = 0xA0,
            StartNoteFromMeasureLine = 0xA1,
            AutoPlayOn = 0xB0,
            AutoPlayOff = 0xB1,
            GaugeLockOn = 0xB2,
            GaugeLockOff = 0xB3,
            TouchGuardOn = 0xB4,
            TouchGuardOff = 0xB5
        }

        private enum NoteErrorFlag
        {
            None = 0,
            InvalidTime = 0b1,
            InvalidType = 0b10,
            NoLongPair = 0b100,
            NoSlidePair = 0b1000,
            NoSlideParent = 0b10000,
            NoPairPair = 0b100000,
            NoRainbowPair = 0b1000000,
            OverNoteLimit = 0b10000000,
            OverLineLengthLimit = 0b100000000
        }

        /// <summary>
        /// レーンごとの有効なノーツ番号
        /// </summary>
        readonly private static List<NoteTypeRange> NoteTypeRanges = new List<NoteTypeRange>()
        {
            new NoteTypeRange {lane = 0, from = 0xB0, to = 0xBF},
            new NoteTypeRange {lane = 1, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 2, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 3, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 4, from = 0x02, to = 0x0F},
            new NoteTypeRange {lane = 5, from = 0x10, to = 0x1F},
            new NoteTypeRange {lane = 6, from = 0x10, to = 0x1F},
            new NoteTypeRange {lane = 7, from = 0x20, to = 0x2F}
        };

        /// <summary>
        /// 同時に配置できるノーツ数
        /// </summary>
        const int MAX_SIMUL_NOTE = 3;

        /// <summary>
        /// 線の最大長さ (ms)
        /// </summary>
        const long MAX_LINE_LENGTH = 12000;

        /// <summary>
        /// レーンの名称
        /// </summary>
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

        /// <summary>
        /// オブジェクトペアリストの作成
        /// </summary>
        /// <param name="obj">オブジェクトの配列</param>
        /// <param name="checkPoint">BMSカウントと実時間の対応表</param>
        /// <param name="errorFlag">エラーフラグを出力する配列</param>
        /// <returns>オブジェクトペアリスト</returns>
        private static List<ObjectPair> CreatePairList(List<BmsObject> obj, List<BpmChangeTiming> checkPoint, ref NoteErrorFlag[] errorFlag)
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
                        else
                        {
                            //線の長さを計算し、長い場合は警告を出す
                            if ((GetRealCount(obj[pair[pairNum].nextID].bmscnt, checkPoint, 0) - GetRealCount(obj[pair[pairNum].ID].bmscnt, checkPoint, 0)) > MAX_LINE_LENGTH)
                            {
                                errorFlag[pair[pairNum].ID] |= NoteErrorFlag.OverLineLengthLimit;
                                errorFlag[pair[pairNum].nextID] |= NoteErrorFlag.OverLineLengthLimit;
                            }
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

                        //ペアのチェック
                        for (int k = firstPair; k <= pairNum; k++)
                        {
                            //ペアなしフラグを立てる
                            if (pair[k].endID == -1)
                            {
                                errorFlag[pair[k].ID] |= NoteErrorFlag.NoSlidePair;
                            }
                            else
                            {
                                //線の長さを計算し、長い場合は警告を出す
                                if ((GetRealCount(obj[pair[k].nextID].bmscnt, checkPoint, 0) - GetRealCount(obj[pair[k].ID].bmscnt, checkPoint, 0)) > MAX_LINE_LENGTH)
                                {
                                    errorFlag[pair[k].ID] |= NoteErrorFlag.OverLineLengthLimit;
                                    errorFlag[pair[k].nextID] |= NoteErrorFlag.OverLineLengthLimit;
                                }
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

                    //ペアノーツ
                    case NoteType.PairNote:
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
                            //ペアとなるペアノーツを検索

                            //対象でなければ除外
                            //時間が異なる
                            if (obj[i].bmscnt != obj[j].bmscnt)
                            {
                                continue;
                            }
                            //種類が異なる
                            if ((NoteType)obj[j].type != NoteType.PairNote)
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
                            errorFlag[pair[pairNum].ID] |= NoteErrorFlag.NoPairPair;
                        }

                        //登録完了
                        pairNum++;
                        break;

                    //レインボーノーツ
                    case NoteType.RainbowNote:
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
                            //ペアとなるレインボーノーツを検索

                            //対象でなければ除外
                            //レーンが異なる
                            if (obj[i].lane != obj[j].lane)
                            {
                                continue;
                            }
                            //種類が異なる
                            if ((NoteType)obj[j].type != NoteType.RainbowNote)
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
                            errorFlag[pair[pairNum].ID] |= NoteErrorFlag.NoRainbowPair;
                        }

                        //登録完了
                        pairNum++;
                        break;
                }
            }

            return pair;
        }

        /// <summary>
        /// 譜面の変換警告チェック
        /// </summary>
        /// <param name="chartData">譜面データ</param>
        /// <param name="checkPoint">BMSカウントと実時間の対応表</param>
        /// <returns>変換警告の有無</returns>
        private static bool CheckChartWarning(Chart chartData, List<BpmChangeTiming> checkPoint)
        {
            // 出力変数の初期化
            bool result = false;

            // 処理用変数の初期化
            int cnt = chartData.main.obj.Count;
            NoteErrorFlag[] errorFlag = new NoteErrorFlag[cnt];
            for (int i = 0; i < cnt; i++)
            {
                errorFlag[i] = NoteErrorFlag.None;
            }
            
            // 変換警告のチェック
            for (int i = 0; i < cnt; i++)
            {
                // 曲の開始点以前にオブジェクトがある
                if (chartData.main.obj[i].bmscnt < chartData.start.bmscnt)
                {
                    errorFlag[i] |= NoteErrorFlag.InvalidTime;
                }

                // ノーツ番号が有効な範囲でない
                NoteTypeRange validNoteTypeRange = NoteTypeRanges.Find(c => c.lane == chartData.main.obj[i].lane);
                if ((chartData.main.obj[i].type < validNoteTypeRange.from) || (chartData.main.obj[i].type > validNoteTypeRange.to))
                {
                    errorFlag[i] |= NoteErrorFlag.InvalidType;
                }

                // 同じ時間に制限数以上のノーツがある
                if (chartData.main.obj.Count(obj => (obj.bmscnt == chartData.main.obj[i].bmscnt) && (obj.lane != 0)) >= MAX_SIMUL_NOTE)
                {
                    errorFlag[i] |= NoteErrorFlag.OverNoteLimit;
                }
            }

            // オブジェクトペアの確認
            CreatePairList(chartData.main.obj, checkPoint, ref errorFlag);

            for (int i = 0; i < cnt; i++)
            {
                if (errorFlag[i] == NoteErrorFlag.None)
                {
                    continue;
                }

                // 警告の出力
                Console.ForegroundColor = ConsoleColor.Yellow;
                if ((errorFlag[i] & NoteErrorFlag.InvalidTime) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("曲の開始点より前にノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.InvalidType) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ノーツ種類{0:x2}が{1}レーンにあります", chartData.main.obj[i].type, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.NoLongPair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないロングノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.NoSlidePair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないスライドノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.NoSlideParent) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("スライド親ノーツのないスライド子ノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.NoPairPair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないペアノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.NoRainbowPair) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("ペアになっていないレインボーノーツがあります");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.OverNoteLimit) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("同時に配置できるノーツを超過しています");
                    result = true;
                }
                if ((errorFlag[i] & NoteErrorFlag.OverLineLengthLimit) != 0)
                {
                    Console.Write("Warning: {0}Measure {1}/{2} {3}レーン ", chartData.main.obj[i].measure, chartData.main.obj[i].unit_numer, chartData.main.obj[i].unit_denom, LaneNames.Find(c => c.lane == chartData.main.obj[i].lane).name);
                    Console.WriteLine("正常に処理できる線の長さを超過しています");
                    result = true;
                }
            }
            Console.ForegroundColor = ConsoleColor.Gray;

            return result;
        }

        /// <summary>
        /// オブジェクトからノーツへの変換
        /// </summary>
        /// <param name="chartData">譜面データ</param>
        /// <returns>ノーツリスト</returns>
        private static List<Note> CalcAllNotes(Chart chartData, List<BpmChangeTiming> checkPoint, long start)
        {
            // 全ノーツの変換
            return ConvertBmsCountToRealCount(chartData.main, checkPoint, start);
        }

        /// <summary>
        /// 小節の実開始時間を取得
        /// </summary>
        /// <param name="chartData">譜面データ</param>
        /// <param name="measure">小節</param>
        /// <returns>小節の実開始時間 [ms]</returns>
        private static long GetMeasureStartTime(Chart chartData, List<BpmChangeTiming> checkPoint, int measure)
        {
            // 曲の開始点の変換
            Note startNote = GetRealCountMainData(chartData.start, checkPoint, 0L);

            // 小節の実開始時間の変換
            return GetRealCount(Measure.measureLength * measure, checkPoint, startNote.Time);
        }

        /// <summary>
        /// 拍子変更に対応するBPM変更を作る
        /// </summary>
        /// <param name="rhythmChange">拍子変更のリスト</param>
        /// <param name="initialBpm">初期BPM</param>
        /// <param name="bpmChange">BPM変更のリスト</param>
        private static void CreateRhythmChange(List<RhythmChange> rhythmChange, double initialBpm, ref List<BpmChange> bpmChange)
        {
            // 拍子変更が入る前のBPM変更リスト
            List<BpmChange> actualBpmChange = new List<BpmChange>();
            foreach (BpmChange change in bpmChange)
            {
                actualBpmChange.Add(new BpmChange { measure = change.measure, unit_denom = change.unit_denom, unit_numer = change.unit_numer, bmscnt = change.bmscnt, bpm = change.bpm });
            }

            // リストのソート
            rhythmChange.Sort((a, b) => a.measure - b.measure);
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));
            actualBpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            // 処理用変数の初期化
            double currentBpm = initialBpm;
            int nearestBpmIndex = 0;
            int measure = 0;
            int bpmIndex = -1;
            int rhythmIndex = -1;

            // 拍子変更の探索
            foreach (RhythmChange rchange in rhythmChange)
            {
                // 直近までのBPM変更の適用
                measure = rchange.measure;
                while ((actualBpmChange.Count > nearestBpmIndex) && (actualBpmChange[nearestBpmIndex].measure < measure))
                {
                    currentBpm = actualBpmChange[nearestBpmIndex].bpm;
                    nearestBpmIndex++;
                }

                // 該当小節のBPM変更に拍子変更を適用
                for (int i = 0; i < bpmChange.Count; i++)
                {
                    if (bpmChange[i].measure == measure)
                    {
                        bpmChange[i].bpm /= rchange.mag;
                    }
                }

                // 小節開始時にBPM変更があるかを確認
                bpmIndex = bpmChange.FindIndex(c => ((c.measure == rchange.measure) && (c.unit_numer == 0)));
                if (bpmIndex == -1)
                {
                    // ない場合は拍子変更に該当するBPM変更を追加
                    bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rchange.mag });
                }

                // 次の小節開始時にBPM変更があるか、あるいは次の小節で拍子変更があるかを確認
                bpmIndex = bpmChange.FindIndex(c => ((c.measure == (measure + 1)) && (c.unit_numer == 0)));
                rhythmIndex = rhythmChange.FindIndex(c => c.measure == (measure + 1));
                if ((bpmIndex == -1) && (rhythmIndex == -1))
                {
                    // 次の小節までのBPM変更の適用
                    while ((actualBpmChange.Count > nearestBpmIndex) && (actualBpmChange[nearestBpmIndex].measure < (measure + 1)))
                    {
                        currentBpm = actualBpmChange[nearestBpmIndex].bpm;
                        nearestBpmIndex++;
                    }

                    // 両方ともない場合は次小節の最初に拍子変更を解除するBPM変更を追加
                    bpmChange.Add(new BpmChange { measure = (measure + 1), unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * (measure + 1), bpm = currentBpm});
                }
            }

            // リストの再ソート
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));
        }

        /// <summary>
        /// BMSカウントと実時間の対応表を作成
        /// </summary>
        /// <param name="initialBpm">初期BPM</param>
        /// <param name="bpmChange">BPM変更のリスト</param>
        /// <returns></returns>
        private static List<BpmChangeTiming> CalcBpmChangeTiming(double initialBpm, List<BpmChange> bpmChange)
        {
            // 出力変数の初期化
            List<BpmChangeTiming> changeTimingList = new List<BpmChangeTiming> { new BpmChangeTiming { bmsCount = 0L, bpm = initialBpm, realTimeCount = 0L } };

            // BPM変更のリストのソート
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            // 処理用変数の初期化
            double currentBpm = initialBpm;

            // BPM変更の検索
            foreach (BpmChange change in bpmChange)
            {
                // BMSカウントと実時間の対応表を作る
                BpmChangeTiming beforePoint = changeTimingList[changeTimingList.Count - 1];
                long realTimeCount = (long)((double)(change.bmscnt - beforePoint.bmsCount) / Measure.measureLength * 60 / currentBpm * 4 * 1000 + beforePoint.realTimeCount);
                changeTimingList.Add(new BpmChangeTiming { bmsCount = change.bmscnt, bpm = change.bpm, realTimeCount = realTimeCount });

                // 現在のBPMの変更
                currentBpm = change.bpm;
            }

            return changeTimingList;
        }

        /// <summary>
        /// メインデータからノーツリストへの変換
        /// </summary>
        /// <param name="bmsCountMainData">メインデータ</param>
        /// <param name="checkpoint">BMSカウントと実時間の対応表</param>
        /// <param name="start">曲の開始点 [ms]</param>
        /// <returns>ノーツリスト</returns>
        private static List<Note> ConvertBmsCountToRealCount(MainData bmsCountMainData, List<BpmChangeTiming> checkpoint, long start)
        {
            // 出力変数の初期化
            List<Note> realCountMainData = new List<Note>();

            // オブジェクトのソート
            bmsCountMainData.obj.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            // オブジェクトからノーツへの変換
            foreach (BmsObject obj in bmsCountMainData.obj)
            {
                realCountMainData.Add(GetRealCountMainData(obj, checkpoint, start));
            }

            return realCountMainData;
        }

        /// <summary>
        /// オブジェクトからノーツへの変換
        /// </summary>
        /// <param name="srcData">オブジェクト</param>
        /// <param name="checkpoint">BMSカウントと実時間の対応表</param>
        /// <param name="start">曲の開始点 [ms]</param>
        /// <returns>ノーツ</returns>
        private static Note GetRealCountMainData(BmsObject srcData, List<BpmChangeTiming> checkpoint, long start)
        {
            long bmsCount = srcData.bmscnt;
            long realTimeCount = GetRealCount(bmsCount, checkpoint, start);
            return new Note { Time = realTimeCount, Lane = srcData.lane, Type = srcData.type };
        }

        /// <summary>
        /// BMSカウントから実時間への変換
        /// </summary>
        /// <param name="bmsCount">BMSカウント</param>
        /// <param name="checkpoint">BMSカウントと実時間の対応表</param>
        /// <param name="start">曲の開始点 [ms]</param>
        /// <returns>実時間 [ms]</returns>
        private static long GetRealCount(long bmsCount, List<BpmChangeTiming> checkpoint, long start)
        {
            // 直前のBMSカウントと実時間の対応を取得
            BpmChangeTiming nearestCheckPoint = checkpoint[0];
            foreach (BpmChangeTiming c in checkpoint)
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

            return (long)((double)(bmsCount - nearestCheckPoint.bmsCount) / Measure.measureLength * 60 / nearestCheckPoint.bpm * 4 * 1000 + nearestCheckPoint.realTimeCount - start);
        }

        private static List<Note> ConvertNoteListForLoop(List<Note> notes, long loopStartTime, long loopEndTime, int loopDisplayNum)
        {
            // 出力変数の初期化
            List<Note> notesForLoop = new List<Note>();

            // ループ区間以外のノーツを削除
            for (int i = (notes.Count - 1); i >= 0; i--)
            {
                if ((notes[i].Time < loopStartTime) || (notes[i].Time >= loopEndTime))
                {
                    notes.RemoveAt(i);
                }
            }

            // ループ用ノーツリストの作成
            long loopLength = loopEndTime - loopStartTime;
            for (int i = 0; i < loopDisplayNum; i++)
            {
                foreach (Note note in notes)
                {
                    notesForLoop.Add(new Note { Time = (note.Time + loopLength * i), Lane = note.Lane, Type = note.Type });
                }
            }

            return notesForLoop;
        }

        /// <summary>
        /// BMSからCSVへの変換
        /// </summary>
        /// <param name="bmsFilePath">BMSファイルパス</param>
        /// <param name="outputPath">出力パス</param>
        /// <param name="startMeasure">再生を開始する小節 (ビューアモード用)</param>
        /// <param name="endMeasure">再生を終了する小節 (ビューアモード用)</param>
        /// <param name="loop">再生をループするか (ビューアモード用)</param>
        /// <param name="loopDisplayNum">ループ時のノーツ表示回数 (ビューアモード用)</param>
        /// <param name="exportCSVPath">出力したCSVのパスを格納する変数</param>
        /// <param name="exportHeaderPath">出力したヘッダのパスを格納する変数</param>
        /// <param name="wavePath">再生するWAVEファイルのパスを格納する変数 (ビューアモード用)</param>
        /// <param name="viewerStartTime">再生を開始する実時間を格納する変数 (ビューアモード用)</param>
        /// <param name="viewerEndTime">再生を終了する実時間を格納する変数 (ビューアモード用)</param>
        /// <param name="warning">変換警告の有無を格納する変数</param>
        /// <returns>変換エラーの有無</returns>
        public static bool Convert_Bms(string bmsFilePath, string outputPath, int startMeasure, int endMeasure, bool loop, int loopDisplayNum, out string exportCSVPath, out string exportHeaderPath, out string wavePath, out long viewerStartTime, out long viewerEndTime, out bool warning)
        {
            // 出力変数の初期化
            exportCSVPath = "";
            exportHeaderPath = "";
            wavePath = "";
            viewerStartTime = 0;
            viewerEndTime = 0;
            warning = false;

            try
            {
                // 処理用変数
                string path;
                string filename;

                // BMSファイルの読み込み
                Chart chartData = BmsReader.Read_Bms(bmsFilePath, ref warning);

                // BPM変換リストの作成
                CreateRhythmChange(chartData.rhythm, chartData.header.bpm, ref chartData.bpm);
                List<BpmChangeTiming> checkPoint = CalcBpmChangeTiming(chartData.header.bpm, chartData.bpm);
                foreach (BpmChangeTiming c in checkPoint)
                {
                    Console.WriteLine("({0}, {1}, {2})", c.bmsCount, c.bpm, c.realTimeCount);
                }

                // 変換警告のチェック
                warning |= CheckChartWarning(chartData, checkPoint);

                //開始ノーツの時間を取得
                Note startNote = GetRealCountMainData(chartData.start, checkPoint, 0L);

                // ノーツへの変換
                List<Note> allNotes = CalcAllNotes(chartData, checkPoint, startNote.Time);

                // 再生を開始する実時間の取得 (ビューアモード用)
                viewerStartTime = GetMeasureStartTime(chartData, checkPoint, startMeasure);
                viewerEndTime = GetMeasureStartTime(chartData, checkPoint, endMeasure);

                if (loop)
                {
                    // ループ用に変換
                    allNotes = ConvertNoteListForLoop(allNotes, viewerStartTime, viewerEndTime, loopDisplayNum);
                }

                // 出力パスの作成
                string exportPath;

                // 出力ディレクトリの作成
                string originalPath = Path.GetDirectoryName(bmsFilePath);
                if (string.IsNullOrEmpty(outputPath))
                {
                    path = @"./csv";
                }
                else
                {
                    path = outputPath;
                }

                // 出力ディレクトリがなければ作成する
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                // 出力CSVファイル名の作成
                string root = Path.GetFileNameWithoutExtension(bmsFilePath);
                filename = root + ".csv";

                // 出力CSVパスの作成
                exportPath = Path.Combine(path, filename);

                // CSVの出力
                Encoding encode = new UTF8Encoding(false);
                using (StreamWriter output = new StreamWriter(exportPath, false, encode))
                {
                    foreach (Note note in allNotes)
                    {
                        output.WriteLine("{0},{1},{2}", note.Time, note.Lane, note.Type);
                    }
                    foreach (BpmChangeTiming bpm in checkPoint)
                    {
                        output.WriteLine("{0},{1},{2}", bpm.realTimeCount - startNote.Time, (int)(bpm.bpm * Measure.measureLength), (int)NoteType.BPMChange);
                    }
                    long measure = GetRealCount(Measure.measureLength, checkPoint, 0);
                    output.WriteLine("{0},{1},{2}", 0, (measure - (startNote.Time % measure)) % measure, (int)NoteType.StartNoteFromMeasureLine);
                }
                exportCSVPath = exportPath;

                // 出力ヘッダファイル名の作成
                filename = root + ".csv.header";

                // 出力ヘッダパスの作成
                exportPath = Path.Combine(path, filename);

                // ヘッダの出力
                using (MemoryStream ms = new MemoryStream())
                using (StreamReader sr = new StreamReader(ms))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(Header));
                    serializer.WriteObject(ms, chartData.header);
                    ms.Position = 0;

                    string json = sr.ReadToEnd();

                    using (StreamWriter output = new StreamWriter(exportPath, false, encode))
                    {
                        output.WriteLine($"{json}");
                    }
                }
                exportHeaderPath = exportPath;

                // 再生するWAVEファイルのパスを作成 (ビューアモード用)
                if (!loop)
                {
                    wavePath = Path.Combine(originalPath, chartData.header.wav);
                }
                else
                {
                    root = Path.GetFileNameWithoutExtension(chartData.header.wav);
                    filename = root + "_smplloop.wav";
                    wavePath = Path.Combine(originalPath, filename);

                    //ループ用にWAVEファイルを編集
                    string originalWavePath = Path.Combine(originalPath, chartData.header.wav);
                    WaveFileEditor.AddSampleLoop(originalWavePath, wavePath, viewerStartTime, viewerEndTime, ref warning);
                }

                return true;
            }
            catch (Exception e)
            {
                // 変換エラー発生時
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                Console.ForegroundColor = ConsoleColor.Gray;

                return false;
            }
        }
    }
}
