using System;
using System.IO;

namespace bms2csv
{
    class Program
    {
        static void Main(string[] args)
        {
            string PATH = ".";
            string OUTPUT = "";
            int totalCount = 0;
            int successCount = 0;
            int failureCount = 0;
            if (args.Length > 0)
            {
                PATH = args[0];
            }
            if (args.Length > 1)
            {
                OUTPUT = args[1];
            }

            if (!Directory.Exists(PATH))
            {
                Console.WriteLine("Error: パスが見つかりません");
                return;
            }
            string[] files = Directory.GetFiles(PATH, "*", SearchOption.TopDirectoryOnly);
            foreach (string f in files)
            {
                if ((!(Path.GetExtension(f).Equals(".bms", StringComparison.OrdinalIgnoreCase))) && (!(Path.GetExtension(f).Equals(".bme", StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                totalCount += 1;

                Console.WriteLine(string.Format("Convert: {0}", f));

                if (BmsConverter.Convert_Bms(f, OUTPUT, out string exportPath))
                {
                    successCount++;
                }
                else
                {
                    failureCount++;
                }

                if (exportPath.Length > 0)
                {
                    Console.WriteLine(string.Format("Export: {0}", exportPath));
                }

                Console.WriteLine();
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
