using System.Reflection;
using System.Runtime.InteropServices;

namespace AvaloniaDnaTest.UI;

/// <summary>Bridge extensionless P/Invoke names to Excel-DNA 1.9's exact PE resource names.</summary>
public static class PackedNativeLibraries
{
    private static readonly object Gate = new();
    private static readonly HashSet<Assembly> Registered = new();

    public static void Configure()
    {
        Configure(Assembly.Load("SkiaSharp"), "libSkiaSharp");
        Configure(Assembly.Load("HarfBuzzSharp"), "libHarfBuzzSharp");
    }

    public static void Configure(Assembly assembly, string library)
    {
        lock (Gate)
        {
            if (Registered.Contains(assembly)) return;
            // Load(name, assembly, flags) bypasses this resolver and calls the assembly's
            // ALC. Excel-DNA extracts NAME.DLL and loads its absolute temporary path.
            NativeLibrary.SetDllImportResolver(assembly, (name, requestingAssembly, flags) =>
            {
                if (!string.Equals(name, library, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(name, library + ".dll", StringComparison.OrdinalIgnoreCase)) return 0;
                DiagnosticLog.Write($"Native request: {name}; normalized={library}.dll; assembly={requestingAssembly.FullName}");
                try
                {
                    var handle = NativeLibrary.Load(library + ".dll", requestingAssembly, flags);
                    DiagnosticLog.Write($"Native load succeeded: {library}.dll; handle=0x{handle:X}");
                    return handle; // Owned for process lifetime; never Free a cached P/Invoke handle.
                }
                catch (Exception ex) { DiagnosticLog.Failure("Native load: " + library, ex); throw; }
            });
            Registered.Add(assembly);
            DiagnosticLog.Write("Registered native resolver: " + assembly.FullName);
        }
    }
}
