namespace WorkOrderService.Messages;

internal sealed class Transaction : IDisposable
{
    private readonly object owner;
    private readonly Action commit;
    private readonly Action dispose;
    private readonly object sync = new();
    private bool disposed;

    internal Transaction(object owner, Action commit, Action dispose)
    {
        this.owner = owner;
        this.commit = commit;
        this.dispose = dispose;
    }

    internal void Commit(object transactionOwner)
    {
        lock (sync)
        {
            if (!ReferenceEquals(owner, transactionOwner))
                throw new ArgumentException("The transaction belongs to a different data store.", nameof(transactionOwner));
            ObjectDisposedException.ThrowIf(disposed, this);
            try
            {
                commit();
            }
            finally
            {
                Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            dispose();
        }
    }
}