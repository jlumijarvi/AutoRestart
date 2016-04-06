using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoRestart
{
    class Program
    {
        static Process process;
        static ProcessStartInfo startinfo;
        static FileSystemWatcher watcher = new FileSystemWatcher();
        static bool buildFailed = false;
        static string[] extensions;

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: AutoRestart.exe <path> <executable>");
                return;
            }

            extensions = ConfigurationManager.AppSettings["extensions"].Split('|');

            watcher.Path = args[0];
            watcher.IncludeSubdirectories = true;

            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnChanged);
            watcher.Deleted += new FileSystemEventHandler(OnChanged);
            watcher.Renamed += new RenamedEventHandler(OnRenamed);

            DoBuild(true);

            watcher.EnableRaisingEvents = true;

            startinfo = new ProcessStartInfo()
            {
                FileName = args[1],
                UseShellExecute = false
            };
            Console.WriteLine("Starting " + startinfo.FileName);
            process = Process.Start(startinfo);

            Console.ReadLine();
        }

        // Define the event handlers. 
        static void OnChanged(object source, FileSystemEventArgs e)
        {
            if (!extensions.Contains(Path.GetExtension(e.FullPath)))
                return;
            Restart();
        }

        static void OnRenamed(object source, RenamedEventArgs e)
        {
            if (!extensions.Contains(Path.GetExtension(e.FullPath)))
                return;
            Restart();
        }

        static void Restart()
        {
            if (process != null && (process.HasExited && !buildFailed))
                return;

            Console.WriteLine("Changes detected");

            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(100);
            }

            Console.WriteLine(startinfo.FileName + " has been exited");

            watcher.EnableRaisingEvents = false;

            DoBuild();

            if (!buildFailed)
            {
                Console.WriteLine("Restarting " + startinfo.FileName);
                process = Process.Start(startinfo);
            }

            watcher.EnableRaisingEvents = true;
        }

        static void DoBuild(bool rebuild = false)
        {
            var info = new ProcessStartInfo()
            {
                FileName = "msbuild",
                Arguments = (rebuild ? "/t:rebuild" : ""),
                UseShellExecute = false
            };
            Console.WriteLine(info.FileName);

            watcher.EnableRaisingEvents = false;
            var p = Process.Start(info);
            p.WaitForExit(int.Parse(ConfigurationManager.AppSettings["timeout"]) * 1000);

            buildFailed = p.ExitCode != 0;
        }
    }
}
