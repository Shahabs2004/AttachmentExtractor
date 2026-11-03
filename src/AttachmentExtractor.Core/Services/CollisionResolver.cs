using AttachmentExtractor.Core.Models;
using AttachmentExtractor.Core.Utilities;

namespace AttachmentExtractor.Core.Services;

public static class CollisionResolver
{
    public static string? Resolve(
        string desiredFullPath,
        CollisionPolicy policy,
        ISet<string> reservedPaths)
    {
        if (string.IsNullOrWhiteSpace(desiredFullPath))
        {
            return null;
        }

        var exists = LongPath.FileExists(desiredFullPath);
        var reserved = reservedPaths.Contains(desiredFullPath);

        if (policy == CollisionPolicy.Skip && (exists || reserved))
        {
            return null;
        }

        if (policy == CollisionPolicy.Overwrite && !reserved)
        {
            return desiredFullPath;
        }

        if (!exists && !reserved)
        {
            return desiredFullPath;
        }

        var directory = Path.GetDirectoryName(desiredFullPath) ?? string.Empty;
        var extension = Path.GetExtension(desiredFullPath);
        var baseName = Path.GetFileNameWithoutExtension(desiredFullPath);

        for (var counter = 1; counter <= 100_000; counter++)
        {
            var candidate = Path.Combine(directory, $"{baseName} ({counter}){extension}");
            if (!LongPath.FileExists(candidate) && !reservedPaths.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static ISet<string> CreateReservationSet() =>
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
