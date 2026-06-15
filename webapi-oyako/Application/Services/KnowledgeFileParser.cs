// Codex developer note: Parses supported user files into normalized text for the Oyako knowledge bank.
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HtmlAgilityPack;
using webapi_oyako.Domain.Models;
using webapi_oyako.Domain.Services;

namespace webapi_oyako.Application.Services;

// Converts uploaded files into pure text and safe previews.
public sealed partial class KnowledgeFileParser : IKnowledgeFileParser
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".docx",
        ".pdf",
        ".pptx",
        ".rtf",
        ".epub",
        ".htm",
        ".html",
        ".md"
    };

    // Parses one file stream using the best parser for its extension.
    public async Task<ParsedKnowledgeFile> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"{extension} dosya türü desteklenmiyor.");
        }

        await using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var text = extension switch
        {
            ".txt" or ".md" => DecodeText(bytes),
            ".htm" or ".html" => ExtractHtmlText(DecodeText(bytes)),
            ".docx" => ExtractZipXmlText(bytes, "word/"),
            ".pptx" => ExtractZipXmlText(bytes, "ppt/slides/"),
            ".epub" => ExtractEpubText(bytes),
            ".rtf" => ExtractRtfText(DecodeText(bytes)),
            ".pdf" => ExtractPdfText(bytes),
            _ => string.Empty
        };

        var normalized = NormalizeText(text);
        var parseStatus = normalized.Length == 0 ? "empty_content" : "parsed";
        var ocrStatus = extension == ".pdf" && normalized.Length < 40 ? "ocr_not_available" : "not_required";
        var contentHash = CalculateHash(normalized);
        var preview = BuildPreview(normalized, Path.GetFileNameWithoutExtension(fileName));
        return new ParsedKnowledgeFile(
            Path.GetFileName(fileName),
            extension,
            normalized,
            preview,
            parseStatus,
            ocrStatus,
            CalculateHash(bytes),
            contentHash,
            bytes.LongLength);
    }

    // Decodes text files with UTF-8 fallback.
    private static string DecodeText(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    // Extracts visible text from HTML documents.
    private static string ExtractHtmlText(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);
        foreach (var node in document.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//nav|//header|//footer") ?? Enumerable.Empty<HtmlNode>())
        {
            node.Remove();
        }

        return WebUtility.HtmlDecode(document.DocumentNode.InnerText);
    }

    // Extracts text from DOCX/PPTX XML parts inside a zip container.
    private static string ExtractZipXmlText(byte[] bytes, string entryPrefix)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase) && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var xml = reader.ReadToEnd();
            builder.Append(' ').Append(ExtractXmlText(xml));
        }

        return builder.ToString();
    }

    // Extracts chapter text from EPUB XHTML/HTML files.
    private static string ExtractEpubText(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        var builder = new StringBuilder();
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                     || entry.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                     || entry.FullName.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            builder.Append(' ').Append(ExtractHtmlText(reader.ReadToEnd()));
        }

        return builder.ToString();
    }

    // Extracts text values from XML while tolerating imperfect Office XML.
    private static string ExtractXmlText(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            return string.Join(" ", document.DescendantNodes().OfType<XText>().Select(text => text.Value));
        }
        catch
        {
            return XmlTagRegex().Replace(xml, " ");
        }
    }

    // Performs a best-effort RTF text extraction without executing RTF controls.
    private static string ExtractRtfText(string rtf)
    {
        var text = RtfHexRegex().Replace(rtf, match =>
        {
            var value = Convert.ToInt32(match.Groups["hex"].Value, 16);
            return ((char)value).ToString();
        });
        text = RtfControlRegex().Replace(text, " ");
        text = text.Replace('{', ' ').Replace('}', ' ');
        return text;
    }

    // Performs a best-effort text-layer PDF extraction from literal text operators.
    private static string ExtractPdfText(byte[] bytes)
    {
        var raw = Encoding.Latin1.GetString(bytes);
        var builder = new StringBuilder();
        foreach (var content in ExtractPdfTextCandidates(bytes, raw))
        {
            AppendPdfTextOperators(builder, content);
        }

        return builder.ToString();
    }

    // Extracts raw and decompressed PDF content streams before text operators are parsed.
    private static IEnumerable<string> ExtractPdfTextCandidates(byte[] bytes, string raw)
    {
        var yielded = false;
        foreach (Match match in PdfFlateStreamRegex().Matches(raw))
        {
            var streamBytes = Encoding.Latin1.GetBytes(match.Groups["data"].Value);
            var inflated = InflatePdfStream(streamBytes);
            if (string.IsNullOrWhiteSpace(inflated))
            {
                continue;
            }

            yielded = true;
            yield return inflated;
        }

        foreach (Match match in PdfPlainStreamRegex().Matches(raw))
        {
            if (match.Groups["dictionary"].Value.Contains("/FlateDecode", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            yielded = true;
            yield return match.Groups["data"].Value;
        }

        if (!yielded)
        {
            yield return raw;
        }
    }

    // Inflates a zlib/deflate PDF stream without leaking binary stream data into the final text.
    private static string InflatePdfStream(byte[] streamBytes)
    {
        try
        {
            using var input = new MemoryStream(streamBytes);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return Encoding.Latin1.GetString(output.ToArray());
        }
        catch
        {
            try
            {
                using var input = new MemoryStream(streamBytes);
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return Encoding.Latin1.GetString(output.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    // Appends PDF text-showing operator payloads while filtering binary-looking strings.
    private static void AppendPdfTextOperators(StringBuilder builder, string content)
    {
        foreach (Match match in PdfArrayTextRegex().Matches(content))
        {
            foreach (Match item in PdfLiteralStringRegex().Matches(match.Groups["items"].Value))
            {
                AppendReadablePdfString(builder, item.Groups["text"].Value);
            }
        }

        foreach (Match match in PdfTextRegex().Matches(content))
        {
            AppendReadablePdfString(builder, match.Groups["text"].Value);
        }
    }

    // Decodes and appends one PDF literal string when it looks like human-readable text.
    private static void AppendReadablePdfString(StringBuilder builder, string encoded)
    {
        var decoded = DecodePdfLiteralString(encoded);
        if (IsReadableText(decoded))
        {
            builder.Append(' ').Append(decoded);
        }
    }

    // Decodes common PDF literal-string escapes.
    private static string DecodePdfLiteralString(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '\\' || index == value.Length - 1)
            {
                builder.Append(current);
                continue;
            }

            var next = value[++index];
            switch (next)
            {
                case 'n':
                    builder.Append(' ');
                    break;
                case 'r':
                    builder.Append(' ');
                    break;
                case 't':
                    builder.Append(' ');
                    break;
                case 'b':
                    builder.Append(' ');
                    break;
                case 'f':
                    builder.Append(' ');
                    break;
                case '(':
                case ')':
                case '\\':
                    builder.Append(next);
                    break;
                default:
                    if (next is >= '0' and <= '7')
                    {
                        var octal = next.ToString();
                        for (var offset = 0; offset < 2 && index + 1 < value.Length && value[index + 1] is >= '0' and <= '7'; offset++)
                        {
                            octal += value[++index];
                        }

                        builder.Append((char)Convert.ToInt32(octal, 8));
                    }
                    else
                    {
                        builder.Append(next);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    // Filters accidental binary matches so compressed PDF bytes do not become knowledge content.
    private static bool IsReadableText(string value)
    {
        var text = value.Trim();
        if (text.Length < 2)
        {
            return false;
        }

        var printable = text.Count(character => !char.IsControl(character) || char.IsWhiteSpace(character));
        var useful = text.Count(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character));
        return printable >= text.Length * 0.95 && useful >= text.Length * 0.75;
    }

    // Normalizes extracted content into pure text.
    private static string NormalizeText(string text)
    {
        var decoded = WebUtility.HtmlDecode(text);
        var controlCharactersRemoved = ControlCharacterRegex().Replace(decoded, " ");
        var withoutControlCharacters = new string(controlCharactersRemoved
            .Select(character => char.IsControl(character) && !char.IsWhiteSpace(character) ? ' ' : character)
            .ToArray());
        return WhitespaceRegex().Replace(withoutControlCharacters, " ").Trim();
    }

    // Builds a compact three-line preview equivalent.
    private static string BuildPreview(string text, string title)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return $"\"{title}\" dosyası için kullanılabilir metin çıkarılamadı.";
        }

        return text.Length <= 360 ? text : $"{text[..360].TrimEnd()}...";
    }

    // Hashes raw bytes.
    private static string CalculateHash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    // Hashes normalized text.
    private static string CalculateHash(string text)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex XmlTagRegex();

    [GeneratedRegex(@"\\'[ ]?(?<hex>[0-9a-fA-F]{2})")]
    private static partial Regex RtfHexRegex();

    [GeneratedRegex(@"\\[a-zA-Z]+-?\d* ?|\\.|[\r\n]+")]
    private static partial Regex RtfControlRegex();

    [GeneratedRegex(@"(?<dictionary><<(?:(?!>>).)*?/Filter\s*/FlateDecode(?:(?!>>).)*?>>)\s*stream\r?\n(?<data>.*?)\r?\nendstream", RegexOptions.Singleline)]
    private static partial Regex PdfFlateStreamRegex();

    [GeneratedRegex(@"(?<dictionary><<(?:(?!>>).)*?>>)\s*stream\r?\n(?<data>.*?)\r?\nendstream", RegexOptions.Singleline)]
    private static partial Regex PdfPlainStreamRegex();

    [GeneratedRegex(@"\[(?<items>.*?)\]\s*TJ", RegexOptions.Singleline)]
    private static partial Regex PdfArrayTextRegex();

    [GeneratedRegex(@"\((?<text>(?:\\.|[^\\)])*)\)", RegexOptions.Singleline)]
    private static partial Regex PdfLiteralStringRegex();

    [GeneratedRegex(@"\((?<text>(?:\\.|[^\\)])*)\)\s*(?:Tj|'|"")", RegexOptions.Singleline)]
    private static partial Regex PdfTextRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex("[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]")]
    private static partial Regex ControlCharacterRegex();
}
