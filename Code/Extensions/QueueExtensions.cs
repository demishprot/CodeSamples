using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Samples
{
	namespace Extensions
	{
		public static class QueueExtensions
		{
			private static readonly object lockObject = new();

			public static async UniTask RunTasks(this Queue<Func<UniTask>> taskQueue, ValueWrapper<bool> isRunning)
			{
				lock (lockObject)
				{
					if (isRunning.Value) return;
					isRunning.Value = true;
				}
				try
				{
					while (true)
					{
						Func<UniTask> currentTask;

						lock (lockObject)
						{
							if (taskQueue.Count == 0) break;
							currentTask = taskQueue.Dequeue();
						}

						await currentTask();
					}
				}
				finally
				{
					lock (lockObject)
					{
						isRunning.Value = false;
					}
				}
			}
			public static void SoftClear(this Queue<Func<UniTask>> taskQueue, ValueWrapper<bool> isRunning)
			{
				if (isRunning.Value) return;
				taskQueue.Clear();
			}
		}

	}
}
