using MelonLoader;
using Il2CppTMPro;
using UnityEngine;

namespace NameBending;

internal static class Debug
{
    public static bool debugMode = false;
    private static string lastDiffLogMessage = string.Empty;

    /// <summary>
    /// Call in OnUpdate to monitor variables per frame but only logs if they change
    /// </summary>
    /// <param name="message"></param>
    /// <param name="debugOnly"></param>
    /// <param name="logLevel"></param>
    internal static void DiffLog(string message, bool debugOnly = true, int logLevel = 0)
    {
        if (message != lastDiffLogMessage)
        {
            lastDiffLogMessage = message;
            Log("DIFFLOG: " + message, debugOnly, logLevel);
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="debugOnly"></param>
    /// <param name="logLevel"></param>
    internal static void Log(string message, bool debugOnly = false, int logLevel = 0)
    {
        if (!debugMode && debugOnly)
            return;
        switch (logLevel)
        {
            case 1:
                Melon<Core>.Logger.Warning(message);
                break;
            case 2:
                Melon<Core>.Logger.Error(message);
                break;
            default:
                Melon<Core>.Logger.Msg(message);
                break;
        }
    }

    internal static void Deb(string message)
    {
        Log(message, true, 0);
    }

    internal static void Msg(string message)
    {
        Log(message, false, 0);
    }

    internal static void Warning(string message)
    {
        Log(message, false, 1);
    }

    internal static void Error(string message)
    {
        Log(message, false, 2);
    }
}