param([string]$Xll = "$PSScriptRoot/../dist/AvaloniaDnaTest-x64.xll")
$ErrorActionPreference = 'Stop'
if (-not ('PackedResourceInspector' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class PackedResourceInspector {
    delegate bool TypeCallback(IntPtr module, IntPtr type, IntPtr parameter);
    delegate bool NameCallback(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool EnumResourceTypesW(IntPtr module, TypeCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    static extern bool EnumResourceNamesW(IntPtr module, IntPtr type, NameCallback callback, IntPtr parameter);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    static string Label(IntPtr p) { return ((ulong)p.ToInt64() <= 65535) ? "#" + p.ToInt64() : Marshal.PtrToStringUni(p); }
    public static string[] Inspect(string path) {
        var module = LoadLibraryEx(path, IntPtr.Zero, 2 /* LOAD_LIBRARY_AS_DATAFILE: never execute XLL */);
        if (module == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        var names = new List<string>();
        try {
            TypeCallback types = (m,t,p) => {
                NameCallback callback = (mm,tt,n,pp) => { names.Add(Label(t) + "/" + Label(n)); return true; };
                if (!EnumResourceNamesW(m,t,callback,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception();
                GC.KeepAlive(callback); return true;
            };
            if (!EnumResourceTypesW(module,types,IntPtr.Zero)) throw new System.ComponentModel.Win32Exception();
            GC.KeepAlive(types);
        } finally { FreeLibrary(module); }
        return names.ToArray();
    }
}
'@
}
$path = (Resolve-Path -LiteralPath $Xll).Path
$bytes = [IO.File]::ReadAllBytes($path)
$pe = [BitConverter]::ToInt32($bytes, 0x3c)
if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw 'Expected an AMD64 PE file.' }
$resources = [PackedResourceInspector]::Inspect($path) | Sort-Object
foreach ($name in @('AVALONIADNATEST.ADDIN','AVALONIADNATEST.UI','AVALONIA.BASE','AVALONIA.CONTROLS','AVALONIA.MARKUP.XAML','AVALONIA.THEMES.FLUENT','AVALONIA.CONTROLS.DATAGRID','AVALONIA.HARFBUZZ','AVALONIA.SKIA','SKIASHARP','HARFBUZZSHARP')) {
    if ($resources -notcontains "ASSEMBLY_LZMA/$name") { throw "Missing managed resource: $name" }
}
foreach ($name in @('LIBSKIASHARP.DLL','LIBHARFBUZZSHARP.DLL','AV_LIBGLESV2.DLL')) {
    if ($resources -notcontains "NATIVE_LIBRARY_LZMA/$name") { throw "Missing native resource: $name" }
}
if ($resources -notcontains 'DNA/__MAIN__') { throw 'Missing packed DNA.' }
$resources
Write-Output 'PASS: AMD64 XLL; managed assemblies and three native libraries are embedded. This is not an Excel load test.'
