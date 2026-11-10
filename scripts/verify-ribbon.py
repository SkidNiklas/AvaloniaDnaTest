"""Verify the actual compressed AddIn assembly and all three PNGs in the final XLL."""
import runpy
import lzma
import pathlib
import struct

context = runpy.run_path(str(pathlib.Path(__file__).with_name('verify-native.py')))
root = context['root']
resources = context['resources']
payload, = [v for k, v in resources.items() if k.startswith('ASSEMBLY_LZMA/AVALONIADNATEST.ADDIN/')]
assembly = lzma.decompress(payload, format=lzma.FORMAT_ALONE)
assert assembly == (root / 'src/AvaloniaDnaTest.AddIn/bin/Release/net8.0-windows/AvaloniaDnaTest.AddIn.dll').read_bytes()
for state in ('Ready', 'Loading', 'Open'):
    png = (root / f'src/AvaloniaDnaTest.AddIn/Icons/{state}.png').read_bytes()
    assert png in assembly, state
    assert struct.unpack('>II', png[16:24]) == (32, 32)
    assert png[25] == 6, 'Expected RGBA transparency'
    assert f'AvaloniaDnaTest.AddIn.Icons.{state}.png'.encode() in assembly
    print(f'PASS: {state}: named embedded resource, exact PNG bytes, 32x32 RGBA in packed assembly')
