"""Inspect actual packed PE resources, decompress LZMA, compare package bytes and list imports.
No third-party Python dependencies. Run from the repository root after building.
"""
import hashlib
import lzma
import pathlib
import struct


class PE:
    def __init__(self, data):
        self.data = data
        pe = self.u32(0x3c)
        self.machine = self.u16(pe + 4)
        optional = pe + 24
        self.directories = optional + (112 if self.u16(optional) == 0x20b else 96)
        sections = optional + self.u16(pe + 20)
        self.sections = []
        for i in range(self.u16(pe + 6)):
            size, rva, rawsize, raw = struct.unpack_from('<IIII', data, sections + 40*i + 8)
            self.sections.append((rva, max(size, rawsize), raw))

    def u16(self, p): return struct.unpack_from('<H', self.data, p)[0]
    def u32(self, p): return struct.unpack_from('<I', self.data, p)[0]
    def offset(self, rva):
        for start, size, raw in self.sections:
            if start <= rva < start + size: return raw + rva - start
        raise ValueError(f'RVA outside sections: {rva:x}')

    def resources(self):
        base = self.offset(self.u32(self.directories + 16))
        result = {}
        def walk(relative, names):
            p = base + relative
            count = self.u16(p+12) + self.u16(p+14)
            for i in range(count):
                name, target = struct.unpack_from('<II', self.data, p+16+8*i)
                if name & 0x80000000:
                    q = base + (name & 0x7fffffff)
                    name = self.data[q+2:q+2+2*self.u16(q)].decode('utf-16-le')
                if target & 0x80000000: walk(target & 0x7fffffff, names + [str(name)])
                else:
                    q = base + target
                    offset = self.offset(self.u32(q))
                    result['/'.join(names + [str(name)])] = self.data[offset:offset+self.u32(q+4)]
        walk(0, [])
        return result

    def imports(self):
        imports = []
        rva = self.u32(self.directories + 8)
        if rva:
            p = self.offset(rva)
            while self.u32(p+12):
                q = self.offset(self.u32(p+12))
                imports.append(self.data[q:self.data.index(b'\0', q)].decode())
                p += 20
        # Delay-load imports are a separate PE directory.
        rva = self.u32(self.directories + 13*8)
        if rva:
            p = self.offset(rva)
            while self.u32(p+4):
                assert self.u32(p) & 1, 'Expected RVA delay imports'
                q = self.offset(self.u32(p+4))
                imports.append('delay:' + self.data[q:self.data.index(b'\0', q)].decode())
                p += 32
        return imports


root = pathlib.Path(__file__).resolve().parent.parent
xll = PE((root / 'dist/AvaloniaDnaTest-x64.xll').read_bytes())
assert xll.machine == 0x8664
resources = xll.resources()
expected = {
    'libSkiaSharp.dll': '.packages/skiasharp.nativeassets.win32/3.119.4/runtimes/win-x64/native/libSkiaSharp.dll',
    'libHarfBuzzSharp.dll': '.packages/harfbuzzsharp.nativeassets.win32/8.3.1.3/runtimes/win-x64/native/libHarfBuzzSharp.dll',
    'av_libglesv2.dll': '.packages/avalonia.angle.windows.natives/2.1.25547.20250602/runtimes/win-x64/native/av_libglesv2.dll',
}
for name, package in expected.items():
    matches = [v for k, v in resources.items() if k.startswith('NATIVE_LIBRARY_LZMA/' + name.upper() + '/')]
    assert len(matches) == 1, name
    data = lzma.decompress(matches[0], format=lzma.FORMAT_ALONE)
    package_path = root / package
    if not package_path.exists() and name == 'av_libglesv2.dll':
        package_path, = root.glob('.packages/avalonia.angle.windows.natives/*/runtimes/win-x64/native/av_libglesv2.dll')
    assert data == package_path.read_bytes(), f'Wrong packed payload: {name}'
    native = PE(data)
    assert native.machine == 0x8664, name
    print(f'{name}: AMD64; package bytes match; SHA256={hashlib.sha256(data).hexdigest()}')
    print('  imports: ' + ', '.join(native.imports()))
print('PASS: actual compressed native payloads match x64 NuGet assets.')
