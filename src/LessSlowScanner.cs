using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Channels
{
    internal class LessSlowScanner
    {
        public LessSlowScanner(int scanners = 1, int capacity = 1000, CancellationToken? cancellationToken = default)
        {
            Scanners = scanners;
            DirectoryChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true,
            });

            CancellationToken = cancellationToken ?? CancellationToken.None;
        }

        private int Scanners { get; }
        private Channel<string> DirectoryChannel { get; }
        private CancellationToken CancellationToken { get; }

        public async Task Scan(string root)
        {
            Console.WriteLine("Enumerating directories");

            var directories = Directory.GetDirectories(root, "*", new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
            });

            Console.WriteLine($"Enumerated directories.  Found {directories.Length}");

            var scanners = new List<DirectoryScanner>();

            foreach (var id in Enumerable.Range(0, Scanners))
            {
                Console.WriteLine($"Creating scanner {id}");
                scanners.Add(new DirectoryScanner($"{id}", DirectoryChannel, CancellationToken));
            }

            await Task.WhenAll(scanners.Select(s => s.Start()));

            Console.WriteLine("Filling channel...");

            foreach (var directory in directories)
            {
                // Console.WriteLine($"Sending {directory} to channel...");
                await DirectoryChannel.Writer.WriteAsync(directory);
            }



            Console.WriteLine("Channel filled.");

            DirectoryChannel.Writer.Complete();

            Console.WriteLine("Channel writer completed.  Waiting for scanners to finish.");

            await Task.WhenAll(scanners.Select(s => s.Completed));

            Console.WriteLine("Done!");
        }

        public class DirectoryScanner
        {
            public DirectoryScanner(string id, Channel<string> channel, CancellationToken cancellationToken)
            {
                Id = id;
                Channel = channel;
                CancellationToken = cancellationToken;
                SoulseekFileFactory = new SoulseekFileFactory();
                TaskCompletionSource = new TaskCompletionSource();
            }
            
            public string Id { get; }
            public Task Completed => TaskCompletionSource.Task;

            private Channel<string> Channel { get; }
            private CancellationToken CancellationToken { get; }
            private SoulseekFileFactory SoulseekFileFactory { get; }
            private TaskCompletionSource TaskCompletionSource { get; }

            public Task Start()
            {
                Console.WriteLine($"Scanner {Id} is starting");
                _ = Read();
                Console.WriteLine($"Scanner {Id} started");

                return Task.CompletedTask;
            }

            private async Task Read()
            {
                Console.WriteLine($"Scanner {Id} running on thread {Thread.CurrentThread.ManagedThreadId}");
                try
                {
                    while (!CancellationToken.IsCancellationRequested && !Channel.Reader.Completion.IsCompleted)
                    {
                        var directory = await Channel.Reader.ReadAsync(CancellationToken);
                        Scan(directory);
                    }
                }
                catch (ChannelClosedException)
                {
                    Console.WriteLine($"Caught ChannelClosedException, IsCompleted:{Channel.Reader.Completion.IsCompleted}");
                    // noop. the channel might close between the time we check and when we go to read
                }
                finally
                {
                    Console.WriteLine($"Scanner {Id}'s work is complete.");
                    TaskCompletionSource.SetResult();
                }
            }

            private void Scan(string directory)
            {
                //Console.WriteLine($"Scanner {Id} Scanning {directory}");

                var files = Directory.GetFiles(directory, "*", new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                });

                foreach (var file in files)
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        Console.WriteLine($"Scanner {Id} aborting scan of {directory} due to cancellation request");
                        break;
                    }

                    _ = SoulseekFileFactory.Create(file, file);
                }
            }
        }
    }
}
