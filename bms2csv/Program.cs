﻿using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace bms2csv
{
    static class UnsafeNativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode, SetLastError = true)]
        static public extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);
    }

    class Program
    {        
        /// <summary>
        /// INIファイルの読み込み用バッファサイズ
        /// </summary>
        private const uint BufferSize = 256;

        static void Main(string[] args)
        {
            // 入力データ
            string PATH = ".";
            string OUTPUT = "";
            int MEASURE = 0;
            int LOOP_MEASURE = 0;
            int SPEED = 1;
            int LOOP_DISPLAY_NUM = 1;

            // 内部処理用
            bool viewerMode = false;
            bool loopMode = false;
            string exportCSVPath;
            string wavePath;
            long viewerStartTime;
            long viewerEndTime;
            bool success;
            bool warning;

            // 変換結果
            int totalCount = 0;
            int successCount = 0;
            int failureCount = 0;

            // コマンドライン引数の処理
            if (args.Length > 0)
            {
                if (!args[0].Equals("-V"))
                {
                    // 一括変換モード: BMSファイルのパス
                    viewerMode = false;
                    loopMode = false;
                    PATH = args[0];
                }
                else
                {
                    // ビューアモード: ビューアモード
                    viewerMode = true;
                    PATH = "";
                }
            }
            if (args.Length > 1)
            {
                if (!viewerMode)
                {
                    // 一括変換モード: 出力パス
                    OUTPUT = args[1];
                }
                else
                {
                    // ビューアモード: 再生モード
                    if (args[1].Equals("-P"))
                    {
                        loopMode = false;
                    }
                    else if(args[1].Equals("-R"))
                    {
                        loopMode = true;
                    }
                    else
                    { 
                        return;
                    }
                }
            }
            if (args.Length > 2)
            {
                // ビューアモード: 再生開始小節
                if (viewerMode)
                {
                    if (args[2].StartsWith("-N"))
                    {
                        MEASURE = int.Parse(args[2].Substring(2));
                    }
                    else
                    {
                        MEASURE = 0;
                    }
                }
            }
            if (args.Length > 3)
            {
                // ビューアモード: BMSファイルのパス
                if (viewerMode)
                {
                    PATH = args[3];
                    OUTPUT = Path.GetDirectoryName(PATH);
                }
            }

            // ループ設定の読み込み
            if (loopMode)
            {
                Console.WriteLine("ループする小節数を入力してください (開始小節: {0}小節)", MEASURE);
                if (!int.TryParse(Console.ReadLine(), out LOOP_MEASURE))
                {
                    loopMode = false;
                }

                if (LOOP_MEASURE < 1)
                {
                    loopMode = false;
                }
            }

            // INIファイルの読み込み
            if (viewerMode)
            {
                StringBuilder returnedString = new StringBuilder((int)BufferSize);
                string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");

                // ハイスピの読み込み
                uint size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "Speed", "1", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    SPEED = int.Parse(returnedString.ToString());
                }

                // ループ時のノーツ表示回数の読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "LoopDisplayNum", "1", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    LOOP_DISPLAY_NUM = int.Parse(returnedString.ToString());
                }
            }

            // 入力パスの存在確認
            if (!viewerMode)
            {
                if (!Directory.Exists(PATH))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: パスが見つかりません");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return;
                }
            }
            else
            {
                if (!File.Exists(PATH))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: パスが見つかりません");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    return;
                }
            }

            // 入力ファイルのリストアップ
            string[] files;
            if (!viewerMode)
            {
                files = Directory.GetFiles(PATH, "*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                files = new string[] { PATH };
            }

            // ファイルの変換
            foreach (string f in files)
            {
                // BMSファイルのみ抽出
                if ((!(Path.GetExtension(f).Equals(".bms", StringComparison.OrdinalIgnoreCase))) && (!(Path.GetExtension(f).Equals(".bme", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                // 変換開始
                totalCount += 1;
                Console.WriteLine(string.Format("Convert: {0}", f));

                // 変換
                if (success = BmsConverter.Convert_Bms(f, OUTPUT, MEASURE, (MEASURE + LOOP_MEASURE), loopMode, LOOP_DISPLAY_NUM, out exportCSVPath, out wavePath, out viewerStartTime, out viewerEndTime, out warning))
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }

                // 変換結果の出力
                if (exportCSVPath.Length > 0)
                {
                    Console.WriteLine(string.Format("Export: {0}", exportCSVPath));
                }

                Console.WriteLine();

                if (viewerMode)
                {
                    // WAVEファイルの存在確認
                    if (!File.Exists(wavePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: Waveファイルが見つかりません");
                        Console.WriteLine(wavePath);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Gray;
                        success = false;
                    }

                    // 変換済みファイルの存在確認
                    if (!File.Exists(exportCSVPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: CSVファイルが見つかりません");
                        Console.WriteLine(exportCSVPath);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Gray;
                        success = false;
                    }

                    // 変換エラー発生時の処理
                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("エラーが発生したため、UGUISU Viewerの起動を中止します");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("続行するには何かキーを押してください . . .");
                        Console.ReadKey();
                        return;
                    }

                    // 変換警告発生時の処理
                    if (warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("警告が発生しています、UGUISU Viewerを起動しますか？[Y/N]");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("続行するには何かキーを押してください . . .");
                        string line = Console.ReadLine();
                        if (!line.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }

                    // ビューアの起動
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UGUISU.exe");
                    string loopFlag = loopMode ? "1" : "0";
                    Process.Start(exePath, "\"" + wavePath + "\" \"" + exportCSVPath + "\" " + viewerStartTime.ToString() + " " + SPEED.ToString() + " " + loopFlag);
                }
            }

            // 変換結果の出力
            Console.WriteLine("===SUMMARY===");

            Console.WriteLine(string.Format("TOTAL: {0}", totalCount));

            Console.Write(string.Format("SUCCESS: "));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(string.Format(successCount.ToString()));
            Console.ForegroundColor = ConsoleColor.Gray;

            Console.Write(string.Format("FAILURE: "));
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(string.Format(failureCount.ToString()));
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
