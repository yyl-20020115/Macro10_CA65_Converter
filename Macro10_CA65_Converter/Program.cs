using System.Text;

namespace Macro10_CA65_Converter;

internal partial class Program
{
     static int Main(string[] args)
    {
        string[] includes = ["macros.inc"];
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: Macro10_CA65_Converter <inputfile>");
            return 0;
        }
        if (!File.Exists(args[0]))
        {
            Console.WriteLine($"Error: File '{args[0]}' not found.");
            return 1;
        }
        return AssemblyConverter.Convert(args[0], Path.ChangeExtension(args[0], ".cvt.asm"), includes);
    }
}
