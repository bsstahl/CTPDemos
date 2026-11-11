namespace WorkOrderService.Services;

internal sealed class DataStoreUpdateException : Exception
{
    public DataStoreUpdateException() : base("The data store update failed.")
    {
    }

    public DataStoreUpdateException(string message) : base(message)
    {
    }

    public DataStoreUpdateException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
