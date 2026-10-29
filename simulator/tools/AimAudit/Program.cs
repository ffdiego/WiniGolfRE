using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Iced.Intel;
using Winigolf.Physics;

// Independent decoding and a small interpreter of the actual arithmetic slice.
// The PE is read as bytes only. No process or Ghidra database is opened or written.
var repositoryRoot = FindRepositoryRoot();
var executablePath = GetOption("--exe") ?? Environment.GetEnvironmentVariable("WINIGOLF_EXE");
if (string.IsNullOrWhiteSpace(executablePath))
{
    var candidates = new[]
    {
        Path.Combine(repositoryRoot, "WiniGolf", "WiniGolf.exe"),
        Path.GetFullPath(Path.Combine(repositoryRoot, "..", "WiniGolf", "WiniGolf.exe"))
    };
    executablePath = candidates.FirstOrDefault(File.Exists);
}
if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
    throw new FileNotFoundException("WiniGolf.exe não encontrado. Passe --exe <caminho> ou defina WINIGOLF_EXE; o binário original não faz parte do repositório.", executablePath);

var pe = File.ReadAllBytes(executablePath);
if (Convert.ToHexString(SHA256.HashData(pe)).ToLowerInvariant() != "3b50e7311d733a1afbd3a33b31e14f95564226e1310a230ffc375e6727a6fb53")
    throw new InvalidDataException("Unexpected executable hash");
var image = new PeImage(pe);
var decoder = Decoder.Create(32, new ByteArrayCodeReader(image.Read(0x41df0c, 0x41e624 - 0x41df0c)));
decoder.IP = 0x41df0c;
var instructions = new List<Instruction>();
while (decoder.IP < 0x41e624) { decoder.Decode(out var ins); instructions.Add(ins); }
var formatter = new IntelFormatter();
string Format(Instruction ins) { var output = new StringOutput(); formatter.Format(ins, output); return output.ToString(); }
var start = instructions.FindIndex(i => i.Mnemonic == Mnemonic.Mov && i.Op0Register == Register.EAX &&
    i.Op1Kind == OpKind.Memory && i.MemoryBase == Register.EBX && i.MemoryDisplacement64 == 0xc8638);
if (start < 0) throw new InvalidDataException("Arithmetic start not found");
var end = instructions.FindIndex(start, i => i.Mnemonic == Mnemonic.Mov && i.Op0Register == Register.EDX &&
    i.Op1Kind == OpKind.Memory && i.MemoryDisplacement64 == 0xc9324);
if (end < start) throw new InvalidDataException("Arithmetic end not found");
var slice = instructions.GetRange(start, end - start);
var asm = slice.Select(i => new { address = $"0x{i.IP:x8}", bytes = Convert.ToHexString(image.Read(i.IP, i.Length)).ToLowerInvariant(), instruction = Format(i) }).ToArray();
File.WriteAllText(Path.Combine(repositoryRoot, "docs/evidencias/mira-iced-disassembly.json"), JsonSerializer.Serialize(asm, new JsonSerializerOptions { WriteIndented = true }));
var checks = new List<object>(); var mismatches = 0;
foreach (var mode in new[] { ShotMode.Mode1, ShotMode.Mode2 })
foreach (var length in new[] { 1, 4, 25, 50, 100, 250 }.Where(n => mode == ShotMode.Mode1 || n <= 50))
foreach (var direction in new[] { 0.0, .7, Math.PI / 2, 3.5 })
foreach (var yaw in new[] { 0.0, 37.0, -85.0 })
foreach (var tilt in new[] { 0.0, -60.0 })
{
    // Mouse endpoint coordinates are integers in the original handler.
    var dx = (int)Math.Round(-length * Math.Cos(direction)); var dy = (int)Math.Round(length * Math.Sin(direction));
    if (dx == 0 && dy == 0 || Math.Sqrt((double)dx * dx + (double)dy * dy) > (mode == ShotMode.Mode1 ? 250 : 50)) continue;
    var machine = new ArithmeticMachine(image, slice, dx, dy, mode, yaw, tilt);
    var actual = machine.Run();
    var implementation = Aim.FromDrag(dx, dy, mode, yaw, tilt);
    var error = Math.Max(Math.Abs(actual.X - implementation.X), Math.Abs(actual.Y - implementation.Y));
    var matched = error <= Math.Max(1e-5, Math.Max(Math.Abs(actual.X), Math.Abs(actual.Y)) * 2e-6);
    if (!matched) mismatches++;
    checks.Add(new { mode = (int)mode, dx, dy, yaw, tilt, interpretedX87 = actual, implementation, maxError = error, matched });
}
var examples = new[] { 10, 25, 50, 100, 200, 250 }.Select(length =>
{
    var v = new ArithmeticMachine(image, slice, -length, 0, ShotMode.Mode1, 0, -60).Run();
    var old = .1 * length / (.4 * Math.Sqrt(length));
    return new { length, originalArithmeticVelocity = v.X, previousFormula = old, previousWithGain2 = old * 2, ratio = v.X / old };
}).ToArray();
var destination = Path.Combine(repositoryRoot, "docs/evidencias/auditoria-mira-x87.json");
File.WriteAllText(destination, JsonSerializer.Serialize(new
{
    method = "Iced 1.21 independent x86 decoding plus interpreted arithmetic slice; does not execute Winigolf",
    limitation = "Arithmetic uses .NET double plus original float stores, not full 80-bit x87 exception/rounding emulation. MouseMove endpoint rounding is outside this slice.",
    constants = new { mode1 = image.Double(0x41e624), mode2 = image.Double(0x41e640), finalMultiplier = image.Extended(0x41e648), extendedBytes = Convert.ToHexString(image.Read(0x41e648, 10)).ToLowerInvariant() },
    inputVersion = Aim.Version,
    start = $"0x{slice[0].IP:x8}", endExclusive = $"0x{instructions[end].IP:x8}", asm,
    total = checks.Count, mismatches, examples, checks
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{checks.Count} byte-interpreted cases; {mismatches} differences against current Aim; multiplier={image.Extended(0x41e648):R}");
foreach (var example in examples) Console.WriteLine(JsonSerializer.Serialize(example));
return args.Contains("--verify") && mismatches > 0 ? 1 : 0;

string? GetOption(string name)
{
    var index = Array.IndexOf(args, name);
    if (index < 0) return null;
    if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        throw new ArgumentException($"A opção {name} exige um caminho.");
    return Path.GetFullPath(args[index + 1]);
}

string FindRepositoryRoot()
{
    foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "docs", "evidencias")) &&
                Directory.Exists(Path.Combine(directory.FullName, "simulator", "tools", "AimAudit")))
                return directory.FullName;
        }
    }
    throw new DirectoryNotFoundException("Não foi possível localizar a raiz do repositório Winigolf a partir do diretório atual ou da pasta do programa.");
}

sealed class PeImage(byte[] bytes)
{
    public byte[] Read(ulong address, int length)
    {
        var pe = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(60));
        var table = pe + 24 + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 20));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(pe + 6));
        var rva = address - 0x400000;
        for (var n = 0; n < count; n++)
        {
            var p = table + n * 40;
            var section = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p + 12));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p + 16));
            if (rva < section || rva + (ulong)length > section + size) continue;
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p + 20)) + rva - section;
            return bytes.AsSpan((int)offset, length).ToArray();
        }
        throw new InvalidDataException($"Unmapped {address:x}");
    }
    public double Double(ulong address) => BitConverter.ToDouble(Read(address, 8));
    public double Extended(ulong address)
    {
        var data = Read(address, 10);
        var significand = BinaryPrimitives.ReadUInt64LittleEndian(data);
        var exponent = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(8));
        if ((exponent & 0x7fff) is 0 or 0x7fff) throw new InvalidDataException("Only normal extended constants supported");
        return ((exponent & 0x8000) == 0 ? 1 : -1) * Math.ScaleB((double)significand, (exponent & 0x7fff) - 16383 - 63);
    }
}

sealed class ArithmeticMachine
{
    private readonly PeImage image;
    private readonly List<Instruction> code;
    private readonly Dictionary<Register, long> regs = new() { [Register.EBX] = 0x200000, [Register.EBP] = 0x300000, [Register.ESP] = 0x400000 };
    private readonly Dictionary<ulong, double> memory = [];
    private readonly List<double> stack = [];
    private bool equal;
    public ArithmeticMachine(PeImage image, List<Instruction> code, int dx, int dy, ShotMode mode, double yaw, double tilt)
    {
        this.image = image; this.code = code;
        memory[0x200000 + 0xc8638] = dx; memory[0x200000 + 0xc8630] = 0;
        memory[0x200000 + 0xc863c] = dy; memory[0x200000 + 0xc8634] = 0;
        memory[0x200000 + 0xc8644] = (int)mode;
        memory[0x200000 + 0x81b10] = (float)yaw; memory[0x200000 + 0x81b18] = (float)tilt;
    }
    private ulong Address(Instruction i) => unchecked((ulong)(regs.GetValueOrDefault(i.MemoryBase) + (long)i.MemoryDisplacement64));
    private double Read(Instruction i, int operand) => i.GetOpKind(operand) switch
    {
        OpKind.Register => regs.GetValueOrDefault(i.GetOpRegister(operand)),
        OpKind.Memory => memory.TryGetValue(Address(i), out var v) ? v :
            i.MemorySize == MemorySize.Float80 ? image.Extended(Address(i)) : image.Double(Address(i)),
        OpKind.Immediate8to32 => i.Immediate8to32,
        OpKind.Immediate32 => i.Immediate32,
        _ => throw new NotSupportedException($"Operand {i.GetOpKind(operand)} at {i.IP:x}")
    };
    private void Write(Instruction i, double value)
    {
        if (i.Op0Kind == OpKind.Register) regs[i.Op0Register] = (long)value;
        else memory[Address(i)] = i.MemorySize == MemorySize.Float32 ? (float)value : value;
    }
    private double Pop() { var v = stack[0]; stack.RemoveAt(0); return v; }
    public Vector3 Run()
    {
        var index = 0; var iterations = 0;
        while (index < code.Count)
        {
            if (++iterations > 1000) throw new InvalidDataException("Unexpected loop");
            var i = code[index++];
            switch (i.Mnemonic)
            {
                case Mnemonic.Mov: Write(i, Read(i, 1)); break;
                case Mnemonic.Sub: Write(i, Read(i, 0) - Read(i, 1)); break;
                case Mnemonic.Add: Write(i, Read(i, 0) + Read(i, 1)); break;
                case Mnemonic.Cmp: equal = Read(i, 0) == Read(i, 1); break;
                case Mnemonic.Jne: if (!equal) { index = code.FindIndex(x => x.IP == i.NearBranchTarget); if (index < 0) throw new InvalidDataException("Branch outside slice"); } break;
                case Mnemonic.Fld: case Mnemonic.Fild: stack.Insert(0, Read(i, 0)); break;
                case Mnemonic.Fstp: Write(i, Pop()); break;
                case Mnemonic.Fchs: stack[0] = -stack[0]; break;
                case Mnemonic.Fmul: stack[0] *= Read(i, 0); break;
                case Mnemonic.Fmulp: { var a = Pop(); stack[0] *= a; break; }
                case Mnemonic.Fdivrp: { var a = Pop(); stack[0] = a / stack[0]; break; }
                case Mnemonic.Faddp: { var a = Pop(); stack[0] += a; break; }
                case Mnemonic.Fsubr: stack[0] = Read(i, 0) - stack[0]; break;
                case Mnemonic.Fadd: stack[0] += Read(i, 0); break;
                case Mnemonic.Call:
                    var arg = memory[(ulong)regs[Register.ESP]];
                    stack.Insert(0, i.NearBranchTarget switch
                    {
                        0x485398 => Math.Cos(arg), 0x486a14 => Math.Sin(arg), 0x486a58 => Math.Sqrt(arg),
                        _ => throw new NotSupportedException($"Unknown call {i.NearBranchTarget:x}")
                    });
                    break;
                default: throw new NotSupportedException($"Unsupported {i.Mnemonic} at {i.IP:x}");
            }
        }
        if (stack.Count != 0) throw new InvalidDataException("Unbalanced FPU stack");
        return new(memory[0x200000 + 0xc8234], memory[0x200000 + 0xc8238], 0);
    }
}
