using System.Reflection;
using System.Text;

namespace AvaloniaDnaTest.UI;

public static class HtmlTemplates
{
    private const string Prefix = "AvaloniaDnaTest.Templates.";
    public static IReadOnlyList<string> Names { get; } = typeof(HtmlTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(".html", StringComparison.Ordinal))
        .Select(n => n[Prefix.Length..]).Order(StringComparer.Ordinal).ToArray();

    public static string Read(string name)
    {
        if (!Names.Contains(name)) throw new ArgumentException("Okänd HTML-mall.", nameof(name));
        using var stream = typeof(HtmlTemplates).Assembly.GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException("Den inbäddade mallen saknas: " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    public static string Preview(string name)
    {
        string html = Read(name);
        int head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        if (head < 0) throw new InvalidDataException("HTML-mallen saknar head: " + name);
        // Only the in-memory preview gets this prefix. Original resource bytes, template
        // sections, placeholders, example data, CSS and scripts remain untouched.
        return html.Insert(head + 6, """
            <script>
            window.__previewErrors = [];
            window.__previewPrintCalls = 0;
            window.__previewReady = false;
            Object.defineProperty(window, 'print', {configurable:false, writable:false,
              value:function(){ window.__previewPrintCalls++; }});
            function previewError(message) {
              window.__previewErrors.push(message);
              if (typeof invokeCSharpAction === 'function') invokeCSharpAction('JavaScript error: ' + message);
            }
            window.addEventListener('error', function(e){ previewError(e.message || 'Resource error'); }, true);
            window.addEventListener('unhandledrejection', function(e){ previewError(String(e.reason)); });
            window.addEventListener('load', function(){
              document.fonts.ready.then(function(){ requestAnimationFrame(function(){
                requestAnimationFrame(function(){ window.__previewReady = true; });
              }); });
            });
            </script>
            """);
    }
}
