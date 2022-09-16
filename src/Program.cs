using System.Threading.Channels;

namespace Channels;

public class Channels
{
    public static async Task Main(string[] args)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity: 1000)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true,
        });

        var workers = new List<Worker>()
        {
            new Worker(channel, "Moe"),
            new Worker(channel, "Curly"),
            new Worker(channel, "Larry"),
        };

        foreach (var worker in workers)
        {
            _ = worker.Start();
        }

        for (int i = 0; i < 10; i++)
        {
            var item = Guid.NewGuid().ToString();

            if (channel.Writer.TryWrite(item))
            {
                Console.WriteLine($"Wrote item {item}");
            }

        }
        
        channel.Writer.Complete();

        Console.WriteLine("Waiting for workers to finish");

        await Task.WhenAll(workers.Select(worker => worker.Completed));

        Console.WriteLine("Work is complete. Press any key to exit.");
        Console.ReadKey();
    }

    public class Worker
    {
        public Worker(Channel<string> channel, string name)
        {
            Channel = channel;
            Name = name;
        }

        public TaskCompletionSource CompletionSource { get; } = new TaskCompletionSource();

        public string Name { get; }
        public Channel<string> Channel { get; }
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
        public Task Completed => CompletionSource.Task;

        public async Task Start()
        {
            while (!CancellationTokenSource.Token.IsCancellationRequested && !Channel.Reader.Completion.IsCompleted)
            {
                var str = await Channel.Reader.ReadAsync(CancellationTokenSource.Token);

                await DoWork(str);
            }

            Console.WriteLine($"Worker {Name} is done.");
            CompletionSource.SetResult();
        }
        public void Stop()
        {
            CancellationTokenSource.Cancel();
        }

        public async Task DoWork(string str)
        {
            await Task.Delay(500);
            Console.WriteLine($"Worker {Name} processed item {str}");
        }
    }
}