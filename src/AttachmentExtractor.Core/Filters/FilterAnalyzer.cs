using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.Core.Filters;

public static class FilterAnalyzer
{
    public static bool RequiresBody(FilterNode node)
    {
        if (node.Kind == FilterNodeKind.Condition)
        {
            return node.Field == FilterField.Body;
        }

        return node.Children.Any(RequiresBody);
    }

    public static (int Groups, int Conditions) Count(FilterNode node)
    {
        var groups = 0;
        var conditions = 0;

        void Walk(FilterNode current)
        {
            if (current.Kind == FilterNodeKind.Group)
            {
                if (current.Children.Count > 0)
                {
                    groups++;
                }

                foreach (var child in current.Children)
                {
                    Walk(child);
                }

                return;
            }

            conditions++;
        }

        Walk(node);
        return (groups, conditions);
    }
}
