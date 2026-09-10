using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using Vulkan;

namespace Renderer;

internal sealed class QueueContext : IDisposable
{
	private readonly Renderer renderer;

	private readonly Queue queue;
	private readonly CommandPool commandPool;
	private readonly Vulkan.Semaphore timelineSemaphore;
	private readonly Thread thread;

	private readonly Lock submissionLock = new();
	private readonly EventWaitHandle done = new(false, EventResetMode.AutoReset);
	private readonly BlockingCollection<Job> jobs = new();
	private ulong nextValue = 0;

	public uint FamilyIndex { get; }

	private readonly static SemaphoreTypeCreateInfo semaphoreTypeCreateInfo = new(next: default, semaphoreType: SemaphoreType.Timeline, initialValue: 0);
	private readonly unsafe static SemaphoreCreateInfo semaphoreCreateInfo = new(next: (nint)Unsafe.AsPointer(ref semaphoreTypeCreateInfo), flags: default);

	private void Run()
	{
		var allocateInfo = new CommandBufferAllocateInfo(
			next: default,
			commandPool: commandPool,
			level: CommandBufferLevel.Primary,
			commandBufferCount: 1
		);

		using var beginInfo = new CommandBufferBeginInfo(
			next: default,
			usage: CommandBufferUsage.OneTimeSubmit,
			inheritanceInfo: null
		);

		var freeBuffers = new Stack<CommandBuffer>();
		var inFlight = new Queue<(CommandBuffer Buffer, ulong SignalValue)>();

		foreach (var job in jobs.GetConsumingEnumerable())
		{
			ulong completed = timelineSemaphore.GetCounterValue();

			while (inFlight.Count > 0 && inFlight.Peek().SignalValue <= completed)
			{
				(var cmd, _) = inFlight.Dequeue();

				cmd.Reset(default);
				freeBuffers.Push(cmd);
			}

			if (job is RecordJob recordJob)
			{
				var cmd = (freeBuffers.Count > 0) ? freeBuffers.Pop() : allocateInfo.CreateCommandBuffers(renderer.Device, commandPool)[0];

				cmd.Begin(beginInfo);
				recordJob.Action(cmd);
				cmd.End();

				using var submitInfo = new SubmitInfo2(
					next: default,
					flags: default,
					waitSemaphoreInfos: null,
					commandBufferInfos:
					[
						new(
							next: default,
							commandBuffer: cmd,
							deviceMask: default
						)
					],
					signalSemaphoreInfos:
					[
						new(
							next: default,
							semaphore: timelineSemaphore,
							value: recordJob.SignalValue,
							stage: PipelineStage2.AllCommands,
							deviceIndex: default
						)
					]
				);

				queue.Submit2(null, submitInfo);
				inFlight.Enqueue((cmd, recordJob.SignalValue));
			}
			else if (job is PresentJob presentJob)
			{
				queue.Present(presentJob.Info);
				presentJob.Info.Dispose();
			}
			else
			{
				throw new UnreachableException();
			}
		}

		queue.WaitIdle();

		foreach (var x in freeBuffers)
			x.Dispose();

		foreach ((var cmd, _) in inFlight)
			cmd.Dispose();

		done.Set();
	}

	public async Task SubmitAsync(Action<CommandBuffer> action)
	{
		if (Thread.CurrentThread.ManagedThreadId == thread.ManagedThreadId)
			throw new InvalidOperationException("Submitting a job for a queue from inside an already submitted job will result in a deadlock.");

		ulong value;

		lock (submissionLock)
		{
			value = Interlocked.Increment(ref nextValue);
			jobs.Add(new RecordJob(action, value));
		}

		await Task.Run(() => timelineSemaphore.Wait(value));
	}

	public void Present(Swapchain[] swapchains, uint[] imageIndices)
	{
		jobs.Add(new PresentJob(
				new PresentInfo(
					next: default,
					waitSemaphores: null,
					swapchains: swapchains,
					imageIndices: imageIndices,
					results: null
				)
			)
		);
	}

	public void Dispose()
	{
		jobs.CompleteAdding();
		done.WaitOne();
		thread.Join();
		commandPool.Dispose();
		timelineSemaphore.Dispose();
	}

	public QueueContext(Renderer renderer, uint familyIndex)
	{
		this.renderer = renderer;
		this.FamilyIndex = familyIndex;

		var commandPoolCreateInfo = new CommandPoolCreateInfo(
			next: default,
			flags: CommandPoolCreateFlags.ResetCommandBuffer,
			queueFamilyIndex: familyIndex
		);

		this.queue = renderer.Device.GetQueue(familyIndex, 0);
		this.commandPool = commandPoolCreateInfo.CreateCommandPool(renderer.Device, renderer.Allocator);
		this.timelineSemaphore = semaphoreCreateInfo.CreateSemaphore(renderer.Device, renderer.Allocator);

		thread = new Thread(Run) { IsBackground = true, Name = $"QueueFamily{familyIndex}" };
		thread.Start();
	}

	private abstract record Job;
	private sealed record RecordJob(Action<CommandBuffer> Action, ulong SignalValue) : Job;
	private sealed record PresentJob(PresentInfo Info) : Job;
}
