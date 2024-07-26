using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace MidiApp
{
    internal static class Logger
    {
        private static readonly string logFilePath = "log.txt";
        private static readonly object lockObj = new();

        public static ObservableCollection<string> logList = new();

        public static event EventHandler<LogEventArgs> OnWarning;
        public static event EventHandler<LogEventArgs> OnError;

        public static void EnableSync()
        {
            BindingOperations.EnableCollectionSynchronization(logList, lockObj);
        }

        public static void Log(object message, Severity severity = Severity.INFO, [CallerMemberName] string caller = "")
        {
            lock (lockObj)
            {
                string log = $"[{DateTime.Now}] [{caller}] [{severity}] {message}";
                File.AppendAllText(logFilePath, $"{log}\n");

                if (severity == Severity.INFO) { }
                else if (severity == Severity.WARNING)
                    OnWarning?.Invoke(null, new() { LogEntry = log, Message = message?.ToString(), Caller = caller, Severity = severity });
                else if (severity == Severity.ERROR || severity == Severity.FATAL)
                    OnError?.Invoke(null, new() { LogEntry = log, Message = message?.ToString(), Caller = caller, Severity = severity });

                logList.Add(log);
#if DEBUG
                Debug.WriteLine(message);
#endif
            }
        }
    }

    internal class LogEventArgs : EventArgs
    {
        public string LogEntry { get; init; }
        public string Message { get; init; }
        public string Caller { get; init; }
        public Severity Severity { get; init; }

        public LogEventArgs() { }
    }

    internal enum Severity
    {
        INFO,
        WARNING, 
        ERROR,
        FATAL
    }
}
