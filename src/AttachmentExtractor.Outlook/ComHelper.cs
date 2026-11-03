using System.Globalization;
using System.Runtime.InteropServices;

namespace AttachmentExtractor.Outlook;

internal static class ComHelper
{
    public static void Release(object? value)
    {
        if (value is null)
        {
            return;
        }

        try
        {
            if (Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }
        catch
        {
            // COM objects may already be released by Outlook.
        }
    }

    public static string GetString(Func<object?> getter, string fallback = "")
    {
        try
        {
            var value = getter();
            return value?.ToString() ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static int GetInt(Func<object?> getter, int fallback)
    {
        try
        {
            var value = getter();
            return value is null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public static long GetLong(Func<object?> getter, long fallback)
    {
        try
        {
            var value = getter();
            return value is null ? fallback : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public static bool GetBool(Func<object?> getter, bool fallback)
    {
        try
        {
            var value = getter();
            return value is null ? fallback : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public static DateTime? GetDate(Func<object?> getter)
    {
        try
        {
            var value = getter();
            if (value is null)
            {
                return null;
            }

            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }
}
