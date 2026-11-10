using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using AvaloniaDnaTest.UI;

internal sealed class NativeProbe : AssemblyLoadContext
{
    private readonly string _nativeDirectory;
    private NativeProbe(string nativeDirectory) : base(isCollectible: false) => _nativeDirectory = nativeDirectory;
    protected override Assembly? Load(AssemblyName name) => null;
    protected override nint LoadUnmanagedDll(string name)
    {
        // Excel-DNA 1.9 resource lookup is exact (case-insensitive), without suffix probing.
        var path = Path.Combine(_nativeDirectory, name);
        Console.WriteLine($"ALC native request: '{name}'; exact file exists={File.Exists(path)}");
        return File.Exists(path) ? LoadUnmanagedDllFromPath(path) : 0;
    }
    public static int Run(string[] args)
    {
        try
        {
            bool fix = args.Contains("--fix");
            var context = new NativeProbe(Path.GetFullPath(args[2]));
            using var stream = File.OpenRead(args[1]);
            var assembly = context.LoadFromStream(stream);
            Console.WriteLine($"Assembly={assembly.FullName}; Location='{assembly.Location}'; fix={fix}");
            string library = assembly.GetName().Name == "SkiaSharp" ? "libSkiaSharp" : "libHarfBuzzSharp";
            if (fix)
            {
                PackedNativeLibraries.Configure(assembly, library);
                PackedNativeLibraries.Configure(assembly, library); // Registration must be idempotent.
            }
            string type = library == "libSkiaSharp" ? "SkiaSharp.SKImageInfo" : "HarfBuzzSharp.Blob";
            if (library == "libSkiaSharp") RuntimeHelpers.RunClassConstructor(assembly.GetType(type)!.TypeHandle);
            else ((IDisposable)assembly.GetType(type)!.GetProperty("Empty")!.GetValue(null)!).Dispose();
            Console.WriteLine("PASS: native-backed managed initialization.");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
    }
}
