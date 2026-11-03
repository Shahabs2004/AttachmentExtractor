namespace AttachmentExtractor.Core.Models;

public sealed class FilterProfile
{
    public string Name { get; set; } = string.Empty;

    public FilterNode AdvancedRoot { get; set; } = FilterNode.NewGroup();

    public QuickFilterSettings Quick { get; set; } = new();

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
