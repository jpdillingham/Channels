using System.Diagnostics;
using System.Threading.Channels;

namespace Channels;

public class Channels
{
    public static async Task Main(string[] args)
    {
        var sw = new Stopwatch();
        sw.Start();

        // 559560ms
        //var slow = new SlowScanner();
        //slow.Scan(@"\\NAS");

        // 44030ms 10 threads
        // 39349ms 100 threads, 10,000 channel limit
        // 37790ms 20 threads, 5,000 channel limit
        // 37293ms 50/5000
        // 39772ms 10/100
        // 52002ms 5/1000
        // 40740ms 8/1000
        // 36422ms 16/1000
        // 35798ms 16/5000
        // 35763ms 16/50000
        // 37417ms 16/500
        // 33812ms 16/500
        // i don't think the upper bound of the channel matters much in this case
        // 38030ms 8/500
        // 16 gives the best time/performance ratio *on this system*, which has 4 vCPUs
        var lessSlow = new LessSlowScanner(16, 50000);
        await lessSlow.Scan(@"\\NAS");

        sw.Stop();
        Console.WriteLine($"Scanned in {sw.ElapsedMilliseconds}ms");
    }

    public async Task DoSampleWork()
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