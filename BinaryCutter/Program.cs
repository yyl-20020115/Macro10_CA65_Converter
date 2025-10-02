namespace BinaryCutter;
using System;
using System.Text;
using System.Threading;

internal static class Program
{
    public static readonly byte[] StartPattern 
            = Encoding.ASCII.GetBytes("<<ROM-START>>"); // Example pattern (ZIP file header)
    /// <summary>
    /// Searches for the first occurrence of a byte pattern within a byte array.
    /// </summary>
    /// <param name="source">The byte array to search within.</param>
    /// <param name="pattern">The byte pattern to search for.</param>
    /// <returns>The starting index of the first occurrence of the pattern, or -1 if not found.</returns>
    public static int IndexOf(this byte[] source, byte[] pattern)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (pattern.Length == 0)
            return 0; // Empty pattern found at index 0 by convention
        if (pattern.Length > source.Length)
            return -1; // Pattern cannot be larger than the source array

        for (int i = 0; i <= source.Length - pattern.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < pattern.Length; j++)
            {
                if (source[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }
            if (match)
            {
                return i; // Pattern found at index i
            }
        }
        return -1; // Pattern not found
    }

    static int Main(string[] args)
    {
        if(args.Length==0 || args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine("Usage: BinaryCutter <options>");
            Console.WriteLine("Options:");
            Console.WriteLine("  -h, --help       Show help information");
            // Add more options as needed
            return 0;
        }
        if (File.Exists(args[0]))
        {
            var binary = File.ReadAllBytes(args[0]);
            var length = StartPattern.Length;
            if (binary.Length >= length)
            {
                var start = binary.IndexOf(StartPattern);
                if (start >= 0)
                {
                    var rom_location = (uint)binary[start + StartPattern.Length + 0] |
                        ((uint)binary[start + StartPattern.Length + 0]) << 8;
                    var init_location = (uint)binary[start + StartPattern.Length + 2] |
                        ((uint)binary[start + StartPattern.Length + 3]) << 8;
                    var io_location = (uint)binary[start + StartPattern.Length + 4] |
                        ((uint)binary[start + StartPattern.Length + 5]) << 8;
                    var real_io = binary[start + StartPattern.Length + 6];
                    var reset_pointer = start + length + 7;
                    Console.Write($"Pos:{start:X04},ROM:{rom_location:X04},INIT:{init_location:X04},IO:{io_location:X04},REAL-IO:{real_io}");
                    var output= Path.GetFileNameWithoutExtension(
                        args[0])+"-rom"+Path.GetExtension(args[0]);
                    var p = Array.LastIndexOf(binary, (byte)0);
                    binary = p>reset_pointer 
                        ? binary[reset_pointer..p] 
                        : binary[reset_pointer ..]
                        ;
                    File.WriteAllBytes(output, binary);
                }
            }
        }
        return 0;
    }
}
