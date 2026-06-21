using System.Text;

var output = args.Length > 0 ? args[0] : "ModTheSpire2.pck";
using var fs = File.Create(output);
using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

// Godot 4 PCK header, matching the format used by the game's Workshop pcks.
bw.Write(Encoding.ASCII.GetBytes("GDPC"));
bw.Write(3); // format version
bw.Write(4); // engine major
bw.Write(5); // engine minor
bw.Write(1); // engine patch
bw.Write(2); // reserved/revision field observed in STS2 pcks
bw.Write((long)112); // file table offset
bw.Write((long)4);   // file table size: just the file count

// Reserved bytes up to offset 112.
while (fs.Position < 112)
{
    bw.Write((byte)0);
}

// Empty file table.
bw.Write(0);
