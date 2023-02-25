using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace MidiApp
{
    internal class Logger
    {
        private readonly string logFilePath = "log.txt";

        private static Logger _logger;
        private static Logger logger => _logger ??= new();

        private Logger()
        {
            File.WriteAllText(logFilePath, "Starting logger...\n");
        }

        public static void Log(object message, Severity severity = Severity.INFO, [CallerMemberName] string caller = "")
        {
            lock (logger)
            {
                File.AppendAllText(logger.logFilePath, $"[{DateTime.Now}] [{caller}] [{severity}] {message}\n");
#if DEBUG
                Debug.WriteLine(message);
#endif
            }
        }
    }

    internal enum Severity
    {
        INFO,
        WARNING,
        ERROR,
        FATAL
    }
}
