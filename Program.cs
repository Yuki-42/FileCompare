

using System.Diagnostics;
using System.IO.Hashing;

namespace FileCompare;

class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Program arguments</param>
    public static void Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.WriteLine("Usage: FileCompare <file1> <file2>");
            return;
        }
        new Program().Run(new Tuple<string, string>(args[0], args[1]));
    }

    private void Run(Tuple<string, string> paths)
    {
        // Get file objects
        FileInfo file1 = new(paths.Item1);
        FileInfo file2 = new(paths.Item2);
        
        // Check how long a file comparison takes
        Stopwatch stopwatch = new();
        stopwatch.Start();
        
        // Get length in bytes of file1
        long file1Length = file1.Length;
        
        Console.WriteLine($"{file1.FullName} length: {file1Length}. Took {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed})");
        
        // Reset stopwatch
        stopwatch.Restart();
        
        // Get length in bytes of file2
        long file2Length = file2.Length;
        
        Console.WriteLine($"{file2.FullName} length: {file2Length}. Took {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed})");
        
        // Reset stopwatch
        stopwatch.Stop();
        
        // Check if the files are the same length
        if (file1Length != file2Length)
        {
            Console.WriteLine("Files are not the same length.");
            return;
        }
        
        stopwatch.Restart();
        // Get the hash of file1
        string file1Hash = GetFileHash(file1);
        
        Console.WriteLine($"File1 hash: {file1Hash}. Took {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed})");
        
        stopwatch.Restart();
        
        // Get the hash of file2
        string file2Hash = GetFileHash(file2);
        
        Console.WriteLine($"File2 hash: {file2Hash}. Took {stopwatch.ElapsedMilliseconds}ms ({stopwatch.Elapsed})");
        
        // Check if the hashes are the same
        if (file1Hash == file2Hash)
        {
            Console.WriteLine("Files are the same.");
        }
        else
        {
            Console.WriteLine("Files are not the same.");
        }
        
    }
    
    /// <summary>
    /// Calculates the number of 4MB buffers needed to store a file inclusive of any remainder.
    /// </summary>
    /// <param name="fileLength">File length in bytes.</param>
    /// <returns>Number of 4MB buffers needed to store a file</returns>
    private int CalculateBufferLength(long fileLength)
    {
        // Calculate the number of 4MB buffers needed to store the file
        int bufferLength = (int)(fileLength / 4194304);
        
        // Check if there is a remainder
        if (fileLength % 4194304 != 0)
        {
            bufferLength++;
        }
        
        return bufferLength;
    }
    
    /// <summary>
    /// Gets the CRC32 hash of a file.
    /// </summary>
    /// <param name="file">File to hash</param>
    /// <returns>Hash of the file</returns>
    private string GetFileHash(FileInfo file)
    {
        /*
         * 1. Get the number of 4MB buffers needed to store the file.
         * 2. Create a list of byte arrays to store the file data.
         * 3. Calculate how many buffers can be stored in memory at once.
         * 4. Create a watcher to read the file data into the byte arrays while keeping track of the maximum memory used. Each time a buffer is filled, hand it off to a thread to hash the data.
         * 5. Wait for all threads to finish hashing the data.
         * 6. Combine the hashes of the data.
         */
        
        Console.WriteLine($"Getting file hash for {file.FullName}...");
        
        // 1. Get the number of 4MB buffers needed to store the file.
        int bufferLength = CalculateBufferLength(file.Length);

        // 2. Create a list of byte arrays of the buffer length.
        List<byte[]?> buffers = new(bufferLength);

        // 3. Calculate how many buffers can be stored in memory at once. Assume system has 16GB of memory.
        int buffersInMemory = 16 * 1024 / 4;

        // 4. Create a watcher to read the file data into the byte arrays while keeping track of the maximum memory used. Each time a buffer is filled, hand it off to a thread to hash the data.
        int buffersFilled = 0;
        // ReSharper disable once CollectionNeverUpdated.Local
        List<Thread> threads = [];
        List<byte[]> hashes = [];
        
        // Create a loop that pauses when the number of buffers in memory is reached.
        for (int i = 0; i < bufferLength; i++)
        {
            while (buffersFilled - i >= buffersInMemory)
            {
                Thread.Sleep(100);
            }
            
            // Create a buffer to store the file data
            byte[]? buffer = new byte[4194304];
            
            // Read the file data into the buffer
            using (FileStream stream = file.OpenRead())
            {
                stream.Seek(i * 4194304, SeekOrigin.Begin);
                stream.Read(buffer, 0, 4194304);
            }
            
            // Add the buffer to the list
            buffers.Add(buffer);
            
            // Add one to the number of buffers filled
            buffersFilled++;
            
            // Create a thread to hash the buffer, passing the buffer index as a new variable to prevent it from being overwritten.
            Thread thread = new(() =>
            {
                // Hash the buffer
                byte[] hash = HashBuffer(buffer, i);
                
                // Add the hash to the list
                hashes.Add(hash);
                
                // Empty the buffer
                buffer = null;
                buffersFilled--;
            });
            thread.Start();
            threads.Add(thread);
        }
        
        // 5. Wait for all threads to finish hashing the data.
        foreach (Thread thread in threads)
        {
            thread.Join();
        }
        
        // 6. Combine the hashes of the data into one larger hash.
        return CombineHashes(hashes);
    }

    private byte[] HashBuffer(byte[]? buffer, int index)
    {
        // Create a new crc32
        Crc32 crc32 = new();
        
        // Add the buffer to the crc32
        crc32.Append(buffer);
        
        // Get the hash of the buffer
        return crc32.GetCurrentHash();
    }
    
    private string CombineHashes(List<byte[]> hashes)
    {
        // Create a new crc32
        Crc32 crc32 = new();
        
        // Add each hash to the crc32
        foreach (byte[] hash in hashes)
        {
            crc32.Append(hash);
        }
        
        // Get the hash of the combined hashes
        return crc32.GetCurrentHash().ToString();
    }
}