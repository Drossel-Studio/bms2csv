using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace bms2csv
{
    class Program
    {
        [DllImport("kernel32.dll", EntryPoint = "GetPrivateProfileStringW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern uint GetPrivateProfileString(string lpAppName, string lpKeyName, string lpDefault, StringBuilder lpReturnedString, uint nSize, string lpFileName);
        
        private const ConsoleColor gray = ConsoleColor.Gray;
        private const uint BufferSize = 256;

        static void Main(string[] args)
        {
            string PATH = ".";
            string OUTPUT = "";
            int MEASURE = 0;
            int SPEED = 1;
            bool viewerMode = false;
            string exportCSVPath = "";
            string wavePath = "";
            long viewerStartTime = 0;
            bool success = false;
            bool warning = false;
            int totalCount = 0;
            int successCount = 0;
            int failureCount = 0;

            if (args.Length > 0)
            {
                if (!args[0].Equals("-V"))
                {
                    viewerMode = false;
                    PATH = args[0];
                }
                else
                {
                    viewerMode = true;
                    PATH = "";
                }
            }
            if (args.Length > 1)
            {
                if (!viewerMode)
                {
                    OUTPUT = args[1];
                }
                else
                {
                    if (!args[1].Equals("-P"))
                    {
                        return;
                    }
                }
            }
            if (args.Length > 2)
            {
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
                if (viewerMode)
                {
                    PATH = args[3];
                    OUTPUT = Path.GetDirectoryName(PATH);
                }
            }
            if (viewerMode)
            {
                StringBuilder returnedString = new StringBuilder((int)BufferSize);
                uint size = GetPrivateProfileString("CONFIG", "Speed", "1", returnedString, BufferSize, "Config.ini");
                if (size > 0)
                {
                    SPEED = int.Parse(returnedString.ToString());
                }
            }

            if (!viewerMode)
            {
                if (!Directory.Exists(PATH))
                {
                    Console.WriteLine("Error: パスが見つかりません");
                    return;
                }
            }
            else
            {
                if (!File.Exists(PATH))
                {
                    Console.WriteLine("Error: パスが見つかりません");
                    return;
                }
            }

            string[] files;
            if (!viewerMode)
            {
                files = Directory.GetFiles(PATH, "*", SearchOption.TopDirectoryOnly);
            }
            else
            {
                files = new string[] { PATH };
            }
            foreach (string f in files)
            {
                if ((!(Path.GetExtension(f).Equals(".bms", StringComparison.OrdinalIgnoreCase))) && (!(Path.GetExtension(f).Equals(".bme", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                totalCount += 1;

                Console.WriteLine(string.Format("Convert: {0}", f));

                if (success = BmsConverter.Convert_Bms(f, OUTPUT, MEASURE, out exportCSVPath, out wavePath, out viewerStartTime, out warning))
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }

                if (exportCSVPath.Length > 0)
                {
                    Console.WriteLine(string.Format("Export: {0}", exportCSVPath));
                }

                Console.WriteLine();

                if (viewerMode)
                {
                    if (!File.Exists(wavePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: Waveファイルが見つかりません");
                        Console.WriteLine(wavePath);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Gray;
                        success = false;
                    }
                    if (!File.Exists(exportCSVPath))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: CSVファイルが見つかりません");
                        Console.WriteLine(exportCSVPath);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Gray;
                        success = false;
                    }

                    if (!success)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("エラーが発生したため、UGUISU Viewerの起動を中止します");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine("続行するには何かキーを押してください . . .");
                        Console.ReadKey();
                        return;
                    }

                    if (warning)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("警告が発生しています、UGUISU Viewerを起動しますか？[Y/N]");
                        Console.ForegroundColor = gray;
                        Console.WriteLine("続行するには何かキーを押してください . . .");
                        string line = Console.ReadLine();
                        if (!line.Equals("Y", StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }
                    }

                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UGUISU.exe");
                    Process.Start(exePath, "\"" + wavePath + "\" \"" + exportCSVPath + "\" " + viewerStartTime.ToString() + " " + SPEED.ToString());
                }
            }

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
