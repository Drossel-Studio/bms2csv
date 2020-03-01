using System;
using System.Collections.Generic;
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
            FlickUpNote = 0x10,
            FlickDownNote = 0x11,
            SpecialNote = 0x20
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

        /// <summary>
        /// レーンごとの有効なノーツ番号
        /// </summary>
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
        /// <param name="errorFlag">エラーフラグを出力する配列</param>
        /// <returns>オブジェクトペアリスト</returns>
        private static List<ObjectPair> CreatePairList(List<BmsObject> obj, ref NoteErrorFlag[] errorFlag)
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

        /// <summary>
        /// 譜面の変換警告チェック
        /// </summary>
        /// <param name="chartData">譜面データ</param>
        /// <returns>変換警告の有無</returns>
        private static bool CheckChartWarning(Chart chartData)
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
            }

            // オブジェクトペアの確認
            CreatePairList(chartData.main.obj, ref errorFlag);

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
            }
            Console.ForegroundColor = ConsoleColor.Gray;

            return result;
        }

        /// <summary>
        /// オブジェクトからノーツへの変換
        /// </summary>
        /// <param name="chartData">譜面データ</param>
        /// <returns>ノーツリスト</returns>
        private static List<Note> CalcAllNotes(Chart chartData, List<BpmChangeTiming> checkPoint)
        {
            // 曲の開始点の変換
            Note startNote = GetRealCountMainData(chartData.start, checkPoint, 0L);

            // 全ノーツの変換
            return ConvertBmsCountToRealCount(chartData.main, checkPoint, startNote.Time);
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
            // リストのソート
            rhythmChange.Sort((a, b) => a.measure - b.measure);
            bpmChange.Sort((a, b) => (int)(a.bmscnt - b.bmscnt));

            // 処理用変数の初期化
            double currentBpm = initialBpm;
            bool rhythmChanged = false;

            // 0小節目の拍子変更の検索
            int measure = 0;
            int rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
            if (rhythmIndex != -1)
            {
                rhythmChanged = true;

                int bpmIndex = bpmChange.FindIndex(c => ((c.measure == measure) && (c.unit_numer == 0)));
                if (bpmIndex != -1)
                {
                    // 小節の最初でBPM変更がある場合はBPM変更の書き換え
                    currentBpm = bpmChange[bpmIndex].bpm;
                    bpmChange[bpmIndex].bpm /= rhythmChange[rhythmIndex].mag;
                }
                else
                {
                    // BPM変更がない場合はBPM変更の新規作成
                    bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });
                }
            }

            foreach (BpmChange change in bpmChange)
            {
                if (change.measure != measure)
                {
                    // 当該小節のBMP変更でない場合
                    if (rhythmChanged)
                    {
                        // 当該小節で拍子変更している場合
                        if ((change.measure != (measure + 1)) || (change.unit_numer != 0))
                        {
                            // 次小節の最初でBPM変更がない場合は打ち消し用BPM変更の新規作成
                            bpmChange.Add(new BpmChange { measure = measure + 1, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * (measure + 1), bpm = currentBpm });
                        }
                    }

                    // BPM変更がない小節での拍子変更
                    for (measure++; measure < change.measure; measure++)
                    {
                        rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
                        if (rhythmIndex != -1)
                        {
                            rhythmChanged = true;

                            // 拍子変更に対応するBPM変更の作成
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });

                            if ((change.measure != (measure + 1)) || (change.unit_numer != 0))
                            {
                                // 次小節の最初でBPM変更がない場合は打ち消し用BPM変更の新規作成
                                bpmChange.Add(new BpmChange { measure = measure + 1, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * (measure + 1), bpm = currentBpm });
                            }
                        }
                    }

                    // BPM変更がある小節での拍子変更
                    measure = change.measure;
                    rhythmIndex = rhythmChange.FindIndex(c => c.measure == measure);
                    if (rhythmIndex != -1)
                    {
                        rhythmChanged = true;

                        if (change.unit_numer != 0)
                        {
                            // 小節の最初以外の場合はBPM変更の作成
                            bpmChange.Add(new BpmChange { measure = measure, unit_denom = 1, unit_numer = 0, bmscnt = Measure.measureLength * measure, bpm = currentBpm / rhythmChange[rhythmIndex].mag });
                        }
                    }
                    else
                    {
                        // 当該小節での拍子変更なし
                        rhythmChanged = false;
                    }
                }

                // 現在のBPMを変更
                currentBpm = change.bpm;
                if (rhythmChanged)
                {
                    // 拍子変更中ならBPM変更の書き換え
                    change.bpm /= rhythmChange[rhythmIndex].mag;
                }
            }
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
        /// <param name="wavePath">再生するWAVEファイルのパスを格納する変数 (ビューアモード用)</param>
        /// <param name="viewerStartTime">再生を開始する実時間を格納する変数 (ビューアモード用)</param>
        /// <param name="viewerEndTime">再生を終了する実時間を格納する変数 (ビューアモード用)</param>
        /// <param name="warning">変換警告の有無を格納する変数</param>
        /// <returns>変換エラーの有無</returns>
        public static bool Convert_Bms(string bmsFilePath, string outputPath, int startMeasure, int endMeasure, bool loop, int loopDisplayNum, out string exportCSVPath, out string wavePath, out long viewerStartTime, out long viewerEndTime, out bool warning)
        {
            // 出力変数の初期化
            exportCSVPath = "";
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
                Chart chartData = BmsReader.Read_Bms(bmsFilePath);

                // 変換警告のチェック
                warning = CheckChartWarning(chartData);

                // BPM変換リストの作成
                CreateRhythmChange(chartData.rhythm, chartData.header.bpm, ref chartData.bpm);
                List<BpmChangeTiming> checkPoint = CalcBpmChangeTiming(chartData.header.bpm, chartData.bpm);
                foreach (BpmChangeTiming c in checkPoint)
                {
                    Console.WriteLine("({0}, {1}, {2})", c.bmsCount, c.bpm, c.realTimeCount);
                }

                // ノーツへの変換
                List<Note> allNotes = CalcAllNotes(chartData, checkPoint);

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
                Console.ForegroundColor = ConsoleColor.Gray;

                return false;
            }
        }
    }
}
