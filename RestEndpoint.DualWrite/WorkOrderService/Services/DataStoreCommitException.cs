namespace WorkOrderService.Services;

internal sealed class DataStoreCommitException : Exception
{
    public DataStoreCommitException() : base("The data store commit failed.")
    {
    }

    public DataStoreCommitException(string message) : base(message)
    {
    }

    public DataStoreCommitException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
