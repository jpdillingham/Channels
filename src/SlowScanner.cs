using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Channels
{
    internal class SlowScanner
    {
        public void Scan(string root)
        {
            var factory = new SoulseekFileFactory();

            Console.WriteLine("Enumerating directories");

            var directories = Directory.GetDirectories(root, "*", new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
            });

            Console.WriteLine($"Enumerated directories.  Found {directories.Length}");

            foreach (var directory in directories)
            {
                Console.WriteLine($"Scanning {directory}");

                var files = Directory.GetFiles(directory, "*", new EnumerationOptions
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                });

                foreach (var file in files)
                {
                    _ = factory.Create(file, file);
                }
            }
        }
    }
}
