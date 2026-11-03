namespace AttachmentExtractor.Core.Models;

public enum FilterNodeKind
{
    Group,
    Condition
}

public enum GroupOperator
{
    And,
    Or
}

public enum FilterField
{
    Subject,
    Body,
    Sender,
    SenderName,
    SenderEmail,
    SenderDomain,
    Recipient,
    To,
    Cc,
    FolderPath,
    ReceivedDate,
    SentDate,
    AttachmentName,
    AttachmentExtension,
    AttachmentSize,
    AttachmentCount,
    HasAttachments,
    IsRead,
    Importance,
    Category,
    MessageSize
}

public enum ConditionOperator
{
    Contains,
    NotContains,
    Equals,
    NotEquals,
    StartsWith,
    EndsWith,
    Regex,
    GreaterThan,
    LessThan,
    GreaterOrEqual,
    LessOrEqual,
    Between,
    Before,
    After,
    IsTrue,
    IsFalse,
    In,
    NotIn
}

public enum OutputStructure
{
    Flat,
    BySender,
    ByDate,
    ByFolder,
    Custom
}

public enum CollisionPolicy
{
    Rename,
    Skip,
    Overwrite
}

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public enum FilterInputKind
{
    Text,
    Date,
    Number,
    Size,
    Count,
    Boolean,
    Importance,
    Extension
}
