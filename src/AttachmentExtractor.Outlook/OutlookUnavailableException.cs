namespace AttachmentExtractor.Outlook;

public sealed class OutlookUnavailableException : Exception
{
    public OutlookUnavailableException(string message)
        : base(message)
    {
    }

    public OutlookUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
