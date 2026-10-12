using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pw.Hub.Tools;

public static class WaitHandleExtensions
{
	public static Task AsTask(this WaitHandle handle)
	{
		return handle.AsTask(Timeout.InfiniteTimeSpan);
	}

	public static Task AsTask(this WaitHandle handle, TimeSpan timeout)
	{
		TaskCompletionSource<object> taskCompletionSource = new TaskCompletionSource<object>();
		RegisteredWaitHandle state = ThreadPool.RegisterWaitForSingleObject(handle, delegate(object? obj, bool timedOut)
		{
			TaskCompletionSource<object> taskCompletionSource2 = (TaskCompletionSource<object>)obj;
			if (timedOut)
			{
				taskCompletionSource2.TrySetCanceled();
			}
			else
			{
				taskCompletionSource2.TrySetResult(null);
			}
		}, taskCompletionSource, timeout, executeOnlyOnce: true);
		taskCompletionSource.Task.ContinueWith((Task<object> _, object? obj) => ((RegisteredWaitHandle)obj).Unregister(null), state, TaskScheduler.Default);
		return taskCompletionSource.Task;
	}
}
