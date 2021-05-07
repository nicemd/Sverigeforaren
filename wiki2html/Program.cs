using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
// ReSharper disable StringLiteralTypo

namespace wiki2html
{
    class  CragInfo
    {
        public string Name;
        public double? Lat;
        public double? Lon;
    }

    class Program
    {
        private static Dictionary<string, string> _images;
        private static Dictionary<string, string> _routes;

        static void Main()
        {
            var parser = new WikitextParser();
            var path = Path.Combine(Directory.GetCurrentDirectory(), @"../../../.."); // Repo base path


            IndexImages(path);
            IndexRoutes(path);

            using var index = new StreamWriter(Path.Combine(path, @"index.html"));

            var indexHeader = LoadTemplate(path, "indexHeader.html");
            index.Write(indexHeader);

            // Process all crags in mediawiki directory
            foreach (var filename in Directory.EnumerateFiles(Path.Combine(path, @"mediawiki/")))
            {
                using var inputReader = File.OpenText(Path.Combine(path, filename));
                using var htmlOutputWriter = new StreamWriter(Path.Combine(path,  @"html/" + Path.GetFileNameWithoutExtension(filename) + ".html"));

                var text = inputReader.ReadToEnd();

                text = FixNestedBrackets(text);
                var ast = parser.Parse(text);

                var cragName = Path.GetFileNameWithoutExtension(filename);

                Console.WriteLine($"Processing {cragName}");

                var header = LoadTemplate(path, "cragHeader.html");
                header = header.Replace("{{CRAGNAME}}", cragName);

                htmlOutputWriter.WriteLine(header);

                var crag = new CragInfo {Name = cragName};
                foreach (var node in ast.EnumChildren())
                {
                    ProcessNode(cragName, htmlOutputWriter, node, crag, 0);
                }

                var footer = LoadTemplate(path, "cragFooter.html");
                htmlOutputWriter.WriteLine(footer);

                index.Write($"<li><a class=\"crag\" href=\"html/{Path.GetFileNameWithoutExtension(filename) + ".html"}\"");
                if(crag.Lat.HasValue && crag.Lon.HasValue)
                    index.Write($" data-lat=\"{crag.Lat.Value.ToString(CultureInfo.InvariantCulture)}\" data-lon=\"{crag.Lon.Value.ToString(CultureInfo.InvariantCulture)}\"");
                index.WriteLine($">{crag.Name}</a></li>");

            }

            var indexFooter = LoadTemplate(path, "indexFooter.html");
            index.Write(indexFooter);
        }

        static void ProcessNode(string cragName, TextWriter writer, Node node, CragInfo cragInfo, int level)
        {
            //writer.WriteLine($"{level} {node.GetType().Name}: " + node.ToPlainText().Excerpt(10));
            switch (node)
            {
                case Heading heading:
                    writer.WriteLine("<h2>" + heading.ToPlainText().HtmlEncode() + "</h2>");
                    break;

                case WikiLink wikiLink:
                    if ((wikiLink.Target?.ToString()?.HasPrefix("Bild:") ?? false) || (wikiLink.Target?.ToString()?.HasPrefix("Image:") ?? false))
                    {
                        var imageName = wikiLink.Target.ToString().TrimPrefix("Bild:");
                        imageName = imageName.TrimPrefix("Image:");

                        var filename = GetImage(imageName) ?? imageName;
                        writer.WriteLine($"<img src=\"../images/{filename}\"/>");
                    }
                    else
                    {
                        writer.WriteLine($"<p>{wikiLink?.ToPlainText().HtmlEncode()}");
                    }
                    break;
                
                case PlainText plainText:
                    if(string.IsNullOrWhiteSpace(plainText.Content)) break; 

                    // Don't print table stuff
                    var trimmed = plainText.Content.TrimStart();
                    if(trimmed.StartsWith("{")) break;
                    if(trimmed.StartsWith("|")) break;
                    if(trimmed.StartsWith("|")) break;

                    // Mixed html
                    if(trimmed.StartsWith("<div")) break;
                    if (trimmed.StartsWith("</div>")) break;
                    if (trimmed.StartsWith("<googlemap")) break;
                    if(trimmed.StartsWith("</googlemap")) break;

                    writer.WriteLine($"<p>{plainText.ToPlainText().HtmlEncode()}</p>");
                    break;

                case Paragraph paragraph:
                    foreach (var child in paragraph.EnumChildren().Where(c=>c!=null))
                    {
                        ProcessNode(cragName, writer, child, cragInfo, level+1);
                    }
                    break;
                case Template template:
                {
                    // Convert template args into a dictionary
                    var args = node.EnumChildren().OfType<TemplateArgument>()
                        .Where(c => c.Name?.ToString() != null && c.Value?.ToString() != null)
                        .Select(c => (Name: c.Name.ToString().Trim(), Value: c.Value.ToString().Trim()))
                        .GroupBy(c=>c.Name).Select(g=>g.Last()) // Distinct
                        .ToDictionary(c => c.Name, c => c.Value);

                    var templateName = template.Name.ToString().Trim();
                    if (templateName == "led" || templateName == "problem")
                    {
                        var lednamn = args.SafeGet("namn");
                        var ledUrl = GetRouteUrl(cragName, lednamn);

                        writer.WriteLine("<ul class=\"led\">");
                        writer.WriteLine($"<li class=\"nr\">{args.SafeGet("nr")?.HtmlEncode()}");
                        if(ledUrl!=null)
                            writer.WriteLine($"<li class=\"namn\"><a href=\"{ledUrl}\">{lednamn.HtmlEncode()}</a>");
                        else
                            writer.WriteLine($"<li class=\"namn\">{lednamn?.HtmlEncode()}");
                        writer.WriteLine($"<li class=\"grad\">{args.SafeGet("grad")?.HtmlEncode()}");
                        writer.WriteLine($"<li class=\"text\">{args.SafeGet("text")?.HtmlEncode()}");
                        writer.WriteLine("</ul>");
                    }
                    else if (templateName == "info klippa" || templateName=="info boulderområde")
                    {
                        var latstr = args.SafeGet("lat");
                        var lonstr = args.SafeGet("long");
                        if (latstr!=null && lonstr!=null && 
                            double.TryParse(latstr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lat) && 
                            double.TryParse(lonstr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lon))
                        {
                            writer.WriteLine(
                                $"<p>GPS: <span class=\"gps\">{latstr.HtmlEncode()},{lonstr.HtmlEncode()}</span></p>");
                            cragInfo.Lat = lat;
                            cragInfo.Lon = lon;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Unknown template: {templateName}");
                    }
                }
                    break;
                case HtmlTag htmlTag:
                    break;
                /*default:
                    writer.WriteLine($"{level} {node.GetType().Name}: " + node.ToPlainText().Excerpt(10));
                    break;*/
            }

        }

        private static void IndexImages(string rootPath)
        {
            // Create a dictionary with normalized filename to real filename for all images
            _images = Directory.GetFiles(Path.Combine(rootPath, "images")).Select(Path.GetFileName).ToDictionary(d => d.ToLowerInvariant(), d => d);
        }

        private static string GetImage(string imageName)
        {
            return _images.SafeGet(imageName.ToLowerInvariant())
                ?? _images.SafeGet(imageName.ToLowerInvariant().Replace(" ", "_"));
        }

        private static void IndexRoutes(string rootPath)
        {
            // Create a dictionary with normalized filename to real filename for all route files
            _routes = Directory.GetFiles(Path.Combine(rootPath, "mediawiki", "routes")).Select(Path.GetFileNameWithoutExtension).ToDictionary(d => d.ToLowerInvariant(), d => d);
        }

        private static string GetRouteUrl(string klippa, string lednamn)
        {
            var i = _routes.SafeGet($"{klippa}-{lednamn}".ToLowerInvariant());
            return i != null ? $"../mediawiki/routes/{i}.txt" : null;
        }

        private static string LoadTemplate(string rootPath, string filename)
        {
            return File.ReadAllText(Path.Combine(rootPath, "wiki2html", filename));
        }

        private static string FixNestedBrackets(string text)
        {
            // [[Bild:Brattberget.jpg|thumb|400px|right|Anne & Linda klättrar Skrubben i norrländsk kvällsol. Foto: [[Användare:Peblin|Per Lindh]]]]
            // to
            // [[Bild:Brattberget.jpg|thumb|400px|right|Anne & Linda klättrar Skrubben i norrländsk kvällsol. Foto: Användare:Peblin|Per Lindh]]
            // Because of bug in wiki parser

            StringBuilder sb = new StringBuilder();
            foreach (var line in text.Split(new []{'\n','\r'} ).Select(l=>l.Trim()).Where(l=>!string.IsNullOrWhiteSpace(l)))
            {
                if(line.StartsWith("[[") && line.EndsWith("]]"))
                {
                    var innerText = line.Substring(2, line.Length - 4);

                    sb.Append("[[");
                    sb.Append(innerText.Replace("[[", "").Replace("]]", ""));
                    sb.AppendLine("]]");
                }
                else
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }
    }

    public static class ExtensionMethods
    {
        public static string Excerpt(this string s, int l)
        {
            return s?.Substring(0, Math.Min(l, s.Length));
        }
        public static bool HasPrefix(this string s, string prefix)
        {
            return s.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase);
        }

        public static string TrimPrefix(this string s, string prefix)
        {
            return s.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase) ? s.Substring(prefix.Length) : s;
        }

        public static string HtmlEncode(this string s)
        {
            return HttpUtility.HtmlEncode(s);
        }

        public static TValue SafeGet<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
        {
            return d.TryGetValue(key, out var v) ? v : default(TValue);
        }
    }
}
