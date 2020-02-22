using System;
using System.Diagnostics;
using System.IO;

namespace bms2csv
{
    class Program
    {
        static void Main(string[] args)
        {
            string PATH = ".";
            string OUTPUT = "";
            int MEASURE = 0;
            bool viewerMode = false;
            string exportCSVPath = "";
            string wavePath = "";
            long viewerStartTime = 0;
            int totalCount = 0;
            int successCount = 0;
            int failureCount = 0;

            if (args.Length > 0)
            {
                if (args[0] != "-V")
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
                    if (args[1] != "-P")
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

                if (BmsConverter.Convert_Bms(f, OUTPUT, out exportCSVPath, MEASURE, out wavePath, out viewerStartTime))
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
                    string exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UGUISU.exe");
                    Process.Start(exePath, "\"" + wavePath + "\" \"" + exportCSVPath + "\" " + viewerStartTime.ToString());
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
