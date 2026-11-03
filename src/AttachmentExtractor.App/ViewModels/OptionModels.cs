using AttachmentExtractor.Core.Models;

namespace AttachmentExtractor.App.ViewModels;

public sealed record StructureOption(OutputStructure Value, string Label);

public sealed record CollisionOption(CollisionPolicy Value, string Label);

public sealed record ImportanceOption(string Value, string Label);

public sealed record OperatorOption(ConditionOperator Value, string Label);

public sealed record GroupOperatorOption(GroupOperator Value, string Label);
