using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using bms2csv.Model;
using CommandLine;

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

        private static CommandLineOption _commandLineOption;
        private static ConverterModeValueOption _converterModeValueOption;
        private static ViewerModeValueOption _viewerModeValueOption;

        static void Main(string[] args)
        {
            // 入力データ
            string PATH = ".";
            string OUTPUT = "";
            int LOOP_MEASURE = 0;
            int SPEED = 1;
            int LOOP_DISPLAY_NUM = 1;
            int PAUSE_BEFORE_LOOP = 0;
            double MUSIC_SPEED = 1.0;
            int CORRECT_PITCH = 1;
            int BGM_VOLUME = 100;
            int SE_VOLUME = 100;

            // 内部処理用
            string exportCSVPath;
            string exportHeaderPath;
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
            ParseCommandLineArgs(args);

            if (_commandLineOption.IsViewerMode)
            {
                PATH = _viewerModeValueOption.FileName;
                OUTPUT = Path.GetDirectoryName(PATH);
            }
            else
            {
                PATH = _converterModeValueOption.InputPath;
                OUTPUT = _converterModeValueOption.OutputPath;
            }

            // ループ設定の読み込み
            if (_commandLineOption.Loop)
            {
                Console.WriteLine("ループする小節数を入力してください (開始小節: {0}小節)", _commandLineOption.Measure);
                if (!int.TryParse(Console.ReadLine(), out LOOP_MEASURE))
                {
                    _commandLineOption.Loop = false;
                }

                if (LOOP_MEASURE < 1)
                {
                    _commandLineOption.Loop = false;
                }
            }

            // INIファイルの読み込み
            if (_commandLineOption.IsViewerMode)
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

                // ループ間でポーズするかの読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "PauseBeforeLoop", "0", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    PAUSE_BEFORE_LOOP = int.Parse(returnedString.ToString());
                }

                // 再生速度の読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "MusicSpeed", "1.0", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    MUSIC_SPEED = double.Parse(returnedString.ToString());
                }

                // ピッチ補正するかの読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "CorrectPitch", "1", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    CORRECT_PITCH = int.Parse(returnedString.ToString());
                }

                // BGM音量の読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "BGMVolume", "100", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    BGM_VOLUME = int.Parse(returnedString.ToString());
                }

                // SE音量の読み込み
                size = UnsafeNativeMethods.GetPrivateProfileString("CONFIG", "SEVolume", "100", returnedString, BufferSize, iniPath);
                if (size > 0)
                {
                    SE_VOLUME = int.Parse(returnedString.ToString());
                }
            }

            // 入力パスの存在確認
            if (!_commandLineOption.IsViewerMode)
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
            if (!_commandLineOption.IsViewerMode)
            {
                files = Directory.GetFiles(PATH, "*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                files = new string[] { PATH };
            }

            // Shift-JISを利用するためのセットアップ
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
                if (success = BmsConverter.Convert_Bms(f, OUTPUT, _commandLineOption.Measure, (_commandLineOption.Measure + LOOP_MEASURE), _commandLineOption.Loop, LOOP_DISPLAY_NUM, out exportCSVPath, out exportHeaderPath, out wavePath, out viewerStartTime, out viewerEndTime, out warning))
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

                if (_commandLineOption.IsViewerMode)
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
                        DeleteTemporaryFile(exportCSVPath, exportHeaderPath, wavePath, _commandLineOption.Loop);
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
                            DeleteTemporaryFile(exportCSVPath, exportHeaderPath, wavePath, _commandLineOption.Loop);
                            return;
                        }
                    }

                    // ビューアの起動
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _viewerModeValueOption.ExeName);
                    string loopFlag = _commandLineOption.Loop ? "1" : "0";
                    using (Process process = Process.Start(exePath, "\"" + wavePath + "\" \"" + exportCSVPath + "\" " + viewerStartTime.ToString() + " " + SPEED.ToString() + " " + loopFlag + " " + PAUSE_BEFORE_LOOP + " " + MUSIC_SPEED.ToString() + " " + CORRECT_PITCH + " " + BGM_VOLUME + " " + SE_VOLUME))
                    {
                        while (!process.WaitForExit(1000));
                    }
                    DeleteTemporaryFile(exportCSVPath, exportHeaderPath, wavePath, _commandLineOption.Loop);
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

        private static void ParseCommandLineArgs(string[] args)
        {
            var result = Parser.Default.ParseArguments<CommandLineOption>(args);
            if (result.Tag == ParserResultType.NotParsed)
            {
                throw new FormatException("コマンドライン引数のパースに失敗");
            }

            _commandLineOption = result.Value;
            if (_commandLineOption.IsViewerMode)
            {
                _viewerModeValueOption = new ViewerModeValueOption(_commandLineOption.ValueOptions);
            }
            else
            {
                _converterModeValueOption = new ConverterModeValueOption(_commandLineOption.ValueOptions);
            }
        }

        /// <summary>
        /// 一時ファイルを削除
        /// </summary>
        /// <param name="exportCSVPath">出力したCSVのパスを格納する変数</param>
        /// <param name="exportHeaderPath">出力したヘッダのパスを格納する変数</param>
        /// <param name="wavePath">再生するWAVEファイルのパスを格納する変数 (ビューアモード用)</param>
        /// <param name="loopMode">再生をループするか (ビューアモード用)</param>
        static void DeleteTemporaryFile(string exportCSVPath, string exportHeaderPath, string wavePath, bool loopMode)
        {
            if (File.Exists(exportCSVPath))
            {
                File.Delete(exportCSVPath);
            }
            if (File.Exists(exportHeaderPath))
            {
                File.Delete(exportHeaderPath);
            }
            if (loopMode)
            {
                if (File.Exists(wavePath))
                { 
                    File.Delete(wavePath);
                }
            }
        }
    }
}
