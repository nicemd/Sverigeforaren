using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
// ReSharper disable StringLiteralTypo

namespace wiki2html
{
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
            var indexHeader = @"<!DOCTYPE html><html><head><meta charSet=""utf-8""/><title>Sverigeföraren anno 2014</title>" +
                              @"<link rel=""stylesheet"" href=""main.css""></style>" + 
                              @"</head><body>" + 
                              @"<h1>Sverigeföraren anno 2014</h1>" + 
                              @"<p>Denna site är automatgenererad från en export av Sverigeföraren år 2014. Innehållet finns här på <a href=""https://github.com/nicemd/Sverigeforaren"">Github</a>." +
                              @"<ul>";

            var indexFooter = @"</ul>" +
                              @"<p>Copyright (C) Permission is granted to copy, distribute and/or modify this document under the terms of the GNU Free Documentation License, Version 1.3</p>" +
                              @"</body>";

            index.WriteLine(indexHeader);

            foreach (var filename in Directory.EnumerateFiles(Path.Combine(path, @"mediawiki/")))
            {
                /*if(!filename.EndsWith("Dödskalleberget.txt"))
                    continue;*/

                using var f = File.OpenText(Path.Combine(path, filename));
                using var w = new StreamWriter(Path.Combine(path,  @"html/" + Path.GetFileNameWithoutExtension(filename) + ".html"));
                var text = f.ReadToEnd();

                var ast = parser.Parse(text);

                var name = Path.GetFileNameWithoutExtension(filename);
                var header = @"<!DOCTYPE html><html><head><meta charSet=""utf-8""/><title>" + name +@"</title>" +
                             @"<link rel=""stylesheet"" href=""../main.css""></style>" +
                             @"</head><body class=""klippa"">";
                var footer = @"<p>Copyright (C) Permission is granted to copy, distribute and/or modify this document under the terms of the GNU Free Documentation License, Version 1.3</p>" +
                             @"</body>";


                w.WriteLine(header);
                w.WriteLine($"<h1>{name}</h1>");

                foreach (var node in ast.EnumChildren())
                {
                    ProcessNode(name, w, node, 0);
                }

                w.WriteLine(footer);

                index.WriteLine($"<li><a href=\"html/{Path.GetFileNameWithoutExtension(filename) + ".html"}\">{name}</a></li>");
            }
            index.WriteLine(indexFooter);
        }

        static void ProcessNode(string klippa, TextWriter writer, Node node, int level)
        {
            //writer.WriteLine($"{level} {node.GetType().Name}: " + node.ToPlainText().Excerpt(10));
            switch (node)
            {
                case Heading heading:
                    writer.WriteLine("<h2>" + heading.ToPlainText().HtmlEncode() + "</h2>");
                    break;

                case WikiLink wikiLink:
                    if (wikiLink.Target?.ToString()?.HasPrefix("Bild:") ?? false)
                    {
                        var imageName = wikiLink.Target.ToString().TrimPrefix("Bild:");
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

                    writer.WriteLine($"<p>{plainText.ToPlainText().HtmlEncode()}</p>");
                    break;

                case Paragraph paragraph:
                    foreach (var child in paragraph.EnumChildren().Where(c=>c!=null))
                    {
                        ProcessNode(klippa, writer, child, level+1);
                    }
                    break;
                case Template template:
                {
                    var args = node.EnumChildren().OfType<TemplateArgument>()
                        .Where(c => c.Name?.ToString() != null && c.Value?.ToString() != null)
                        .Select(c => (Name: c.Name.ToString().Trim(), Value: c.Value.ToString().Trim()))
                        .GroupBy(c=>c.Name).Select(g=>g.Last()) // Distinct
                        .ToDictionary(c => c.Name, c => c.Value);

                    var templateName = template.Name.ToString().Trim();
                    if (templateName == "led" || templateName == "problem")
                    {
                        var lednamn = args.SafeGet("namn");
                        var ledUrl = GetRouteUrl(klippa, lednamn);

                        writer.WriteLine("<ul>");
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
                        writer.WriteLine($"<p>Lat: " + args.SafeGet("lat")?.HtmlEncode());
                        writer.WriteLine($"Long: " + args.SafeGet("long")?.HtmlEncode() + "</p>");
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

        private static void IndexImages(string path)
        {
            _images = Directory.GetFiles(Path.Combine(path, "images")).Select(Path.GetFileName).ToDictionary(d => d.ToLowerInvariant(), d => d);
        }

        private static string GetImage(string imageName)
        {
            return _images.SafeGet(imageName.ToLowerInvariant());
        }
        private static void IndexRoutes(string path)
        {
            _routes = Directory.GetFiles(Path.Combine(path, "mediawiki", "routes")).Select(Path.GetFileNameWithoutExtension).ToDictionary(d => d.ToLowerInvariant(), d => d);
        }

        private static string GetRouteUrl(string klippa, string lednamn)
        {
            var i = _routes.SafeGet($"{klippa}-{lednamn}".ToLowerInvariant());
            return i != null ? $"../mediawiki/routes/{i}.txt" : null;
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
