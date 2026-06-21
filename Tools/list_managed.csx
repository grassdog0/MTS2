using System;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;

var dir = args[0];
foreach (var file in Directory.EnumerateFiles(dir, "*.dll"))
{
    try
    {
        using var fs = File.OpenRead(file);
        using var pe = new PEReader(fs);
        if (pe.HasMetadata)
        {
            Console.WriteLine(file);
        }
    }
    catch { }
}
