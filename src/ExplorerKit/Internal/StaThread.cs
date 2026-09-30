using System.Runtime.ExceptionServices;

namespace SSP.ExplorerKit.Internal;

/// <summary>Runs code on an STA thread, as required by the Shell COM objects and Windows Forms.</summary>
internal static class StaThread
{
    /// <summary>Runs <paramref name="func"/> directly if the current thread is STA, otherwise on a temporary STA thread.</summary>
    public static T Run<T>(Func<T> func)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return func();
        }

        T result = default!;
        ExceptionDispatchInfo? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = func();
            }
            catch (Exception ex)
            {
                error = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        error?.Throw();
        return result;
    }
}
