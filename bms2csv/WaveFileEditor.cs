using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace bms2csv
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 8)]
    struct ChunkHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] ID;
        public UInt32 Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    struct RiffChunk
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] Type;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
    struct FormatChunk
    {
        public UInt16 CompressionCode;
        public UInt16 Channels;
        public UInt32 SampleRate;
        public UInt32 BytePerSec;
        public UInt16 BlockAlign;
        public UInt16 BitPerSample;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
    struct SampleChunk
    {
        public UInt32 Manufacturer;
        public UInt32 Product;
        public UInt32 SamplePeriod;
        public UInt32 MIDIUnityNote;
        public UInt32 MIDIPitchFraction;
        public UInt32 SMPTEFormat;
        public UInt32 SMPTEOffset;
        public UInt32 SampleLoops;
        public UInt32 SamplerDataSize;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 24)]
    struct SampleLoopData
    {
        public UInt32 CuePointID;
        public UInt32 Type;
        public UInt32 Start;
        public UInt32 End;
        public UInt32 Fraction;
        public UInt32 PlayCount;
    }

    /// <summary>
    /// WAVEファイルの編集を行うクラス
    /// </summary>
    class WaveFileEditor
    {
        /// <summary>
        /// WAVEファイルの読み書き用バッファサイズ
        /// </summary>
        const int BufferSize = 1024;

        /// <summary>
        /// WAVEファイルにループ情報を加える
        /// </summary>
        /// <param name="inputFilename">入力ファイル名</param>
        /// <param name="outputFilename">出力ファイル名</param>
        /// <param name="start">ループ開始点 [ms]</param>
        /// <param name="end">ループ終了点 [ms]</param>
        /// <param name="warning">編集警告の有無を返す変数</param>
        /// <returns>編集エラーの有無</returns>
        static public bool AddSampleLoop(string inputFilename, string outputFilename, long start, long end, ref bool warning)
        {
            using (FileStream input = new FileStream(inputFilename, FileMode.Open, FileAccess.Read))
            using (FileStream output = new FileStream(outputFilename, FileMode.Create, FileAccess.Write))
            {
                // データの格納用変数
                ChunkHeader chunkHeader = new ChunkHeader();
                RiffChunk riffChunk = new RiffChunk();
                bool format = false;
                FormatChunk formatChunk = new FormatChunk();
                bool data = false;
                SampleChunk sampleChunk = new SampleChunk();
                SampleLoopData sampleLoopData = new SampleLoopData();

                // マーシャリング用変数
                GCHandle gch;
                IntPtr ptr;

                // 処理用変数
                int size;
                string id;
                byte[] buffer = new byte[BufferSize];

                // RIFFヘッダの読み込み
                input.Read(buffer, 0, Marshal.SizeOf(typeof(ChunkHeader)));
                gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                chunkHeader = (ChunkHeader)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(ChunkHeader));
                gch.Free();

                // RIFFヘッダの確認
                id = Encoding.ASCII.GetString(chunkHeader.ID);
                if (id != "RIFF")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: WAVEファイルの形式が異常です");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return false;
                }

                // これから加えるSampleチャンクのサイズを加算
                chunkHeader.Size += (UInt32)(Marshal.SizeOf(typeof(ChunkHeader)) + Marshal.SizeOf(typeof(SampleChunk)) + Marshal.SizeOf(typeof(SampleLoopData)));

                // RIFFチャンクの読み込み
                input.Read(buffer, 0, Marshal.SizeOf(typeof(RiffChunk)));
                gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                riffChunk = (RiffChunk)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(RiffChunk));
                gch.Free();

                // RIFFチャンクの確認
                id = Encoding.ASCII.GetString(riffChunk.Type);
                if (id != "WAVE")
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: WAVEファイルの形式が異常です");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return false;
                }

                // RIFFヘッダの書き込み
                size = Marshal.SizeOf(typeof(ChunkHeader));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(chunkHeader, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                output.Write(buffer, 0, size);

                // RIFFチャンクの書き込み
                size = Marshal.SizeOf(typeof(RiffChunk));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(riffChunk, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                output.Write(buffer, 0, size);

                // サブチャンクの読み書き
                while(true)
                {
                    // ヘッダの読み込み
                    if (input.Read(buffer, 0, Marshal.SizeOf(typeof(ChunkHeader))) < Marshal.SizeOf(typeof(ChunkHeader)))
                    {
                        // 読み込み完了
                        break;
                    }
                    gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    chunkHeader = (ChunkHeader)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(ChunkHeader));
                    gch.Free();

                    // チャンクIDの確認
                    id = Encoding.ASCII.GetString(chunkHeader.ID);

                    // 必須チャンクの存在確認
                    if (id == "fmt ")
                    {
                        // Formatチャンク
                        format = true;
                    }
                    if (id == "data")
                    {
                        // Dataチャンク
                        data = true;
                    }

                    // Sampleチャンクの重複確認
                    if (id == "smpl")
                    {
                        // 重複していたら古いSampleチャンクをJUNKチャンクに変更する
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: すでにsmplチャンクが存在します、smplチャンクを上書きします");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        warning = true;

                        id = "JUNK";
                        chunkHeader.ID = System.Text.Encoding.ASCII.GetBytes(id);
                    }

                    // ヘッダの書き込み
                    size = Marshal.SizeOf(typeof(ChunkHeader));
                    ptr = Marshal.AllocHGlobal(size);
                    Marshal.StructureToPtr(chunkHeader, ptr, false);
                    Marshal.Copy(ptr, buffer, 0, size);
                    Marshal.FreeHGlobal(ptr);
                    output.Write(buffer, 0, size);

                    // チャンクの読み書き
                    if (id == "fmt ")
                    {
                        // Formatチャンクの読み込み
                        input.Read(buffer, 0, Marshal.SizeOf(typeof(FormatChunk)));
                        gch = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        formatChunk = (FormatChunk)Marshal.PtrToStructure(gch.AddrOfPinnedObject(), typeof(FormatChunk));
                        gch.Free();

                        // Formatチャンクの書き込み
                        size = Marshal.SizeOf(typeof(FormatChunk));
                        ptr = Marshal.AllocHGlobal(size);
                        Marshal.StructureToPtr(formatChunk, ptr, false);
                        Marshal.Copy(ptr, buffer, 0, size);
                        Marshal.FreeHGlobal(ptr);
                        output.Write(buffer, 0, size);

                        // 追加データがある場合の読み書き
                        UInt32 extraSize = chunkHeader.Size - (UInt32)Marshal.SizeOf(typeof(FormatChunk));
                        for (UInt32 i = 0; i < extraSize;)
                        {
                            size = ((extraSize - i) < BufferSize) ? (int)(extraSize - i) : BufferSize;
                            input.Read(buffer, 0, size);
                            output.Write(buffer, 0, size);
                            i += (UInt32)size;
                        }
                    }
                    else
                    {
                        // その他のチャンクの読み書き
                        for (UInt32 i = 0; i < chunkHeader.Size;)
                        {
                            size = ((chunkHeader.Size - i) < BufferSize) ? (int)(chunkHeader.Size - i) : BufferSize;
                            input.Read(buffer, 0, size);
                            output.Write(buffer, 0, size);
                            i += (UInt32)size;
                        }
                    }
                }

                // 必須チャンクの存在確認
                if ((!format) || (!data))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: WAVEファイルの形式が異常です");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return false;
                }

                // Sampleヘッダの作成
                id = "smpl";
                chunkHeader.ID = System.Text.Encoding.ASCII.GetBytes(id);
                chunkHeader.Size = (UInt32)(Marshal.SizeOf(typeof(SampleChunk)) + Marshal.SizeOf(typeof(SampleLoopData)));

                // Sampleヘッダの書き込み
                size = Marshal.SizeOf(typeof(ChunkHeader));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(chunkHeader, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                output.Write(buffer, 0, size);

                // Sampleチャンクの作成
                double samplePeriod = 1.0 / formatChunk.SampleRate;
                sampleChunk.Manufacturer = 0;
                sampleChunk.Product = 0;
                sampleChunk.SamplePeriod = (UInt32)Math.Round(samplePeriod * 1000000000);
                sampleChunk.MIDIUnityNote = 60;
                sampleChunk.MIDIPitchFraction = 0;
                sampleChunk.SMPTEFormat = 0;
                sampleChunk.SMPTEOffset = 0;
                sampleChunk.SampleLoops = 1;
                sampleChunk.SamplerDataSize = 0;

                // Sampleチャンクの書き込み
                size = Marshal.SizeOf(typeof(SampleChunk));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(sampleChunk, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                output.Write(buffer, 0, size);

                // ループ情報の作成
                sampleLoopData.CuePointID = 0;
                sampleLoopData.Type = 0;
                sampleLoopData.Start = (UInt32)Math.Round(start / samplePeriod / 1000);
                sampleLoopData.End = (UInt32)Math.Round(end / samplePeriod / 1000);
                sampleLoopData.Fraction = 0;
                sampleLoopData.PlayCount = 0;

                // ループ情報の書き込み
                size = Marshal.SizeOf(typeof(SampleLoopData));
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(sampleLoopData, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                output.Write(buffer, 0, size);
            }

            return true;
        }
    }
}
