"""Check actual packed assemblies and byte-exact HTML resources; no build-directory fallback."""
import hashlib
import lzma
import runpy

context = runpy.run_path('scripts/verify-ribbon.py')
root, resources = context['root'], context['resources']

def assembly(name):
    payload, = [v for k, v in resources.items() if k.startswith('ASSEMBLY_LZMA/' + name.upper() + '/')]
    return lzma.decompress(payload, format=lzma.FORMAT_ALONE)

ui = assembly('AvaloniaDnaTest.UI')
assert ui == (root / 'src/AvaloniaDnaTest.UI/bin/Release/net8.0-windows/AvaloniaDnaTest.UI.dll').read_bytes()
web = assembly('Avalonia.Controls.WebView')
assert web == (root / '.packages/avalonia.controls.webview/12.1.1/lib/net8.0/Avalonia.Controls.WebView.dll').read_bytes()
templates = sorted((root / 'mallar').glob('*.html'))
assert len(templates) == 5
for path in templates:
    data = path.read_bytes()
    assert data in ui, path.name
    assert ('AvaloniaDnaTest.Templates.' + path.name).encode() in ui, path.name
    print(f'PASS: embedded byte-exact {path.name}; SHA256={hashlib.sha256(data).hexdigest()}')
assert len([k for k in resources if k.startswith('NATIVE_LIBRARY_LZMA/')]) == 3
print('PASS: NativeWebView managed assembly packed; exactly the three existing native payloads; no new native dependency.')
print(f'XLL size: {(root / "dist/AvaloniaDnaTest-x64.xll").stat().st_size} bytes')
