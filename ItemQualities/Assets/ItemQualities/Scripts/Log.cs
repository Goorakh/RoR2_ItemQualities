using BepInEx.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ItemQualities
{
    internal static class Log
    {
        static readonly StringBuilder _sharedStringBuilder = new StringBuilder(256);

        static readonly int _cachedCallerPathPrefixLength;

        static readonly object _logLock = new object();

        static ManualLogSource _logSource;

        static Log()
        {
            _cachedCallerPathPrefixLength = getCallerPathPrefixLength();

            static int getCallerPathPrefixLength([CallerFilePath] string callerPath = null)
            {
                const string MOD_NAME = nameof(ItemQualities) + @"\Scripts\";

                int modNameLastPathIndex = callerPath.LastIndexOf(MOD_NAME);
                if (modNameLastPathIndex >= 0)
                {
                    return modNameLastPathIndex + MOD_NAME.Length;
                }
                else
                {
                    UnityEngine.Debug.LogError($"[{ItemQualitiesPlugin.PluginName}] Logger failed to determine caller path prefix length");
                    return 0;
                }
            }
        }

        internal static void Init(ManualLogSource logSource)
        {
            _logSource = logSource;
        }

        static StringBuilder buildCallerLogString(string callerPath, string callerMemberName, int callerLineNumber, object data)
        {
            return _sharedStringBuilder.Clear()
                                       .Append(callerPath, _cachedCallerPathPrefixLength, callerPath.Length - _cachedCallerPathPrefixLength)
                                       .Append(":").Append(callerLineNumber)
                                       .Append(" (").Append(callerMemberName).Append("): ")
                                       .Append(data);
        }

        [Conditional("DEBUG")]
        internal static void Debug(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogDebug(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        [Conditional("DEBUG")]
        internal static void Debug_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogDebug(data);
            }
        }

        internal static void Error(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogError(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void Error_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogError(data);
            }
        }

        internal static void Fatal(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogFatal(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void Fatal_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogFatal(data);
            }
        }

        internal static void Info(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogInfo(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void Info_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogInfo(data);
            }
        }

        internal static void Message(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogMessage(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void Message_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogMessage(data);
            }
        }

        internal static void Warning(object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _logSource.LogWarning(buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void Warning_NoCallerPrefix(object data)
        {
            lock (_logLock)
            {
                _logSource.LogWarning(data);
            }
        }

        internal static void LogType(LogLevel level, object data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
#if !DEBUG
            if ((level & LogLevel.Debug) != 0)
                return;
#endif

            lock (_logLock)
            {
                _logSource.Log(level, buildCallerLogString(callerPath, callerMemberName, callerLineNumber, data));
            }
        }

        internal static void LogType_NoCallerPrefix(LogLevel level, object data)
        {
#if !DEBUG
            if ((level & LogLevel.Debug) != 0)
                return;
#endif

            lock (_logLock)
            {
                _logSource.Log(level, data);
            }
        }
    }
}
