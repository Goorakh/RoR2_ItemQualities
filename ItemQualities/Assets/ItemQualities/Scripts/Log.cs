using BepInEx.Logging;
using ItemQualities.Utilities.Extensions;
using MonoMod.Cil;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace ItemQualities
{
    internal static class Log
    {
        private static readonly StringBuilder _sharedStringBuilder = new StringBuilder(256);

        private static readonly int _cachedCallerPathPrefixLength;

        private static readonly object _logLock = new object();

        private static ManualLogSource _logSource;

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

        private static void AppendLogPrefix(StringBuilder stringBuilder, string callerPath, string callerMemberName, int callerLineNumber)
        {
            stringBuilder.Append(callerPath, _cachedCallerPathPrefixLength, callerPath.Length - _cachedCallerPathPrefixLength)
                         .Append(":").Append(callerLineNumber)
                         .Append(" (").Append(callerMemberName).Append("): ");
        }

        [Conditional("DEBUG")]
        internal static void Debug(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogDebug(_sharedStringBuilder.ToString());
            }
        }

        [Conditional("DEBUG")]
        internal static void Debug_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogDebug(data);
            }
        }

        internal static void Error(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogError(_sharedStringBuilder.ToString());
            }
        }

        internal static void Error_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogError(data);
            }
        }

        internal static void PatchError(ILCursor cursor, string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                _sharedStringBuilder.Append($"Patch error for method: {cursor.Method.FullName} ({cursor.Next.SafeToString()}) at ");
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogError(_sharedStringBuilder.ToString());
            }
        }

        internal static void PatchError(ILContext context, string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                _sharedStringBuilder.Append($"Patch error for method: {context.Method.FullName} at ");
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogError(_sharedStringBuilder.ToString());
            }
        }

        internal static void Fatal(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogFatal(_sharedStringBuilder.ToString());
            }
        }

        internal static void Fatal_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogFatal(data);
            }
        }

        internal static void Info(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogInfo(_sharedStringBuilder.ToString());
            }
        }

        internal static void Info_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogInfo(data);
            }
        }

        internal static void Message(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogMessage(_sharedStringBuilder.ToString());
            }
        }

        internal static void Message_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogMessage(data);
            }
        }

        internal static void Warning(string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.LogWarning(_sharedStringBuilder.ToString());
            }
        }

        internal static void Warning_NoCallerPrefix(string data)
        {
            lock (_logLock)
            {
                _logSource.LogWarning(data);
            }
        }

        internal static void LogType(LogLevel level, string data, [CallerFilePath] string callerPath = "", [CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1)
        {
#if !DEBUG
            if ((level & LogLevel.Debug) != 0)
                return;
#endif

            lock (_logLock)
            {
                _sharedStringBuilder.Clear();
                AppendLogPrefix(_sharedStringBuilder, callerPath, callerMemberName, callerLineNumber);
                _sharedStringBuilder.Append(data);

                _logSource.Log(level, _sharedStringBuilder.ToString());
            }
        }

        internal static void LogType_NoCallerPrefix(LogLevel level, string data)
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
