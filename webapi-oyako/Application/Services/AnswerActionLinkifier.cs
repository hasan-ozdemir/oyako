// Codex developer note: Explains the purpose and flow of webapi-oyako/Application/Services/AnswerActionLinkifier.cs for maintainers.
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using webapi_oyako.Domain.Services;

// Groups this source file inside the corresponding Oyako architectural namespace.
namespace webapi_oyako.Application.Services;

// Implements deterministic answer link enrichment without relying on prompt-specific conditions.
public sealed partial class AnswerActionLinkifier : IAnswerActionLinkifier
{
    // Stores the synthetic root id used while parsing HTML fragments.
    private const string RootId = "oyako-answer-linkifier-root";

    // Converts contact-like plain text inside assistant HTML into safe actionable anchor elements.
    public string Linkify(string html)
    {
        // Guards the following branch so the workflow handles empty assistant answers deliberately.
        if (string.IsNullOrWhiteSpace(html))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return html;
        }

        // Creates an HTML document so linkification can skip existing anchors and code blocks safely.
        var document = new HtmlDocument();
        document.LoadHtml($"<div id=\"{RootId}\">{html}</div>");

        // Finds the synthetic root that owns the assistant answer fragment.
        var root = document.GetElementbyId(RootId);
        // Guards the following branch so malformed fragments fall back to the original HTML.
        if (root is null)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return html;
        }

        // Links structured address containers before generic text-node processing handles simpler inline values.
        LinkStructuredAddressContainers(root);

        // Materializes text nodes before replacing them so traversal is not invalidated.
        var textNodes = root
            .Descendants()
            .Where(node => node.NodeType == HtmlNodeType.Text && !IsInsideSkippedElement(node))
            .ToList();

        // Iterates through each plain text node and replaces eligible values with anchors.
        foreach (var textNode in textNodes)
        {
            // Rewrites the current text node only when at least one safe candidate exists.
            ReplaceTextNodeWithLinks(textNode);
        }

        // Returns the enriched HTML fragment without the synthetic wrapper.
        return string.Concat(root.ChildNodes.Select(node => node.OuterHtml));
    }

    // Replaces one text node with a sequence of text and anchor nodes.
    private static void ReplaceTextNodeWithLinks(HtmlNode textNode)
    {
        // Decodes HTML entities so regexes operate on the visible answer text.
        var text = HtmlEntity.DeEntitize(textNode.InnerText);
        // Finds all candidate contact/action values in the current text node.
        var candidates = SelectNonOverlappingCandidates(FindCandidates(text));
        ReplaceTextNodeWithCandidates(textNode, candidates);
    }

    // Replaces one text node using the provided candidate spans.
    private static void ReplaceTextNodeWithCandidates(HtmlNode textNode, IReadOnlyList<LinkCandidate> candidates)
    {
        // Guards the following branch so untouched text nodes remain stable.
        if (candidates.Count == 0)
        {
            // Returns to the caller because there is nothing to replace.
            return;
        }

        // Creates a fragment that will replace the original text node.
        var text = HtmlEntity.DeEntitize(textNode.InnerText);
        var parent = textNode.ParentNode;
        var cursor = 0;
        // Iterates through selected candidates in visual order.
        foreach (var candidate in candidates)
        {
            // Preserves text that appears before the current link candidate.
            if (candidate.Start > cursor)
            {
                parent.InsertBefore(textNode.OwnerDocument.CreateTextNode(text[cursor..candidate.Start]), textNode);
            }

            // Inserts the anchor that represents the detected actionable value.
            parent.InsertBefore(CreateAnchor(textNode.OwnerDocument, candidate), textNode);
            cursor = candidate.Start + candidate.Length;
        }

        // Preserves any remaining text after the final candidate.
        if (cursor < text.Length)
        {
            parent.InsertBefore(textNode.OwnerDocument.CreateTextNode(text[cursor..]), textNode);
        }

        // Removes the original text node after its replacement nodes have been inserted.
        parent.RemoveChild(textNode);
    }

    // Links address values when markdown formatting split the label and the value into separate HTML text nodes.
    private static void LinkStructuredAddressContainers(HtmlNode root)
    {
        // Iterates through compact answer containers where contact labels are commonly rendered.
        foreach (var container in root.Descendants().Where(node => node.Name.Equals("li", StringComparison.OrdinalIgnoreCase) || node.Name.Equals("p", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            // Guards the following branch so containers that already include links are not rewritten again.
            if (container.Descendants("a").Any())
            {
                continue;
            }

            var textSegments = BuildTextSegments(container);
            // Guards the following branch so empty containers are ignored.
            if (textSegments.Count == 0)
            {
                continue;
            }

            var fullText = string.Concat(textSegments.Select(segment => segment.Text));
            var match = AddressLineRegex().Match(fullText);
            // Guards the following branch so only explicit address/location labels are processed.
            if (!match.Success)
            {
                continue;
            }

            var valueGroup = match.Groups["value"];
            var leadingTrim = valueGroup.Value.Length - valueGroup.Value.TrimStart().Length;
            var visible = TrimTrailingPunctuation(valueGroup.Value.Trim());
            // Guards the following branch so vague or malformed values are not converted into map links.
            if (visible.Length < 8 || !LooksLikeAddressOrLocation(visible))
            {
                continue;
            }

            var valueStart = valueGroup.Index + leadingTrim;
            var segment = textSegments.FirstOrDefault(segment => valueStart >= segment.Start && valueStart + visible.Length <= segment.Start + segment.Text.Length);
            // Guards the following branch so cross-node values are left untouched instead of being partially linked.
            if (segment is null)
            {
                continue;
            }

            var href = BuildGoogleMapsSearchUrl(visible);
            var localStart = valueStart - segment.Start;
            ReplaceTextNodeWithCandidates(
                segment.Node,
                new[] { new LinkCandidate(localStart, visible.Length, visible, href, $"Haritada aç: {visible}", true, 40) });
        }
    }

    // Builds visible text segments for one HTML container.
    private static IReadOnlyList<TextSegment> BuildTextSegments(HtmlNode container)
    {
        // Creates the collection that maps full container text positions back to source text nodes.
        var segments = new List<TextSegment>();
        var cursor = 0;
        foreach (var textNode in container.Descendants().Where(node => node.NodeType == HtmlNodeType.Text && !IsInsideSkippedElement(node)))
        {
            var text = HtmlEntity.DeEntitize(textNode.InnerText);
            segments.Add(new TextSegment(textNode, cursor, text));
            cursor += text.Length;
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return segments;
    }

    // Creates one safe anchor element for a selected link candidate.
    private static HtmlNode CreateAnchor(HtmlDocument document, LinkCandidate candidate)
    {
        // Creates the anchor node that the sanitizer will validate before it reaches the frontend.
        var anchor = document.CreateElement("a");
        anchor.SetAttributeValue("href", candidate.Href);
        anchor.SetAttributeValue("class", "oyako-action-link");
        anchor.SetAttributeValue("aria-label", candidate.AriaLabel);
        // Guards the following branch so only browser-safe external links open a new page.
        if (candidate.OpensInNewWindow)
        {
            anchor.SetAttributeValue("target", "_blank");
            anchor.SetAttributeValue("rel", "noopener noreferrer");
        }

        anchor.AppendChild(document.CreateTextNode(candidate.VisibleText));
        // Returns the computed result to the caller and completes this branch of the workflow.
        return anchor;
    }

    // Builds all raw link candidates from one visible text segment.
    private static IReadOnlyList<LinkCandidate> FindCandidates(string text)
    {
        // Creates the collection that will hold every recognized actionable candidate.
        var candidates = new List<LinkCandidate>();
        AddObfuscatedEmailCandidates(text, candidates);
        AddEmailCandidates(text, candidates);
        AddUrlCandidates(text, candidates);
        AddAddressCandidates(text, candidates);
        AddCoordinateCandidates(text, candidates);
        AddPhoneCandidates(text, candidates);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return candidates;
    }

    // Adds email addresses written with human-readable obfuscation.
    private static void AddObfuscatedEmailCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through obfuscated email matches and normalizes them into mailto links.
        foreach (Match match in ObfuscatedEmailRegex().Matches(text))
        {
            var visible = match.Value.Trim();
            var domain = DotTokenRegex()
                .Replace(match.Groups["domain"].Value, ".")
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            var email = $"{match.Groups["local"].Value}@{domain}.{match.Groups["tld"].Value}";
            AddCandidate(candidates, match.Index, match.Length, visible, $"mailto:{email}", $"E-posta gönder: {email}", false, 10);
        }
    }

    // Adds ordinary email addresses.
    private static void AddEmailCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through email matches and creates mailto links.
        foreach (Match match in EmailRegex().Matches(text))
        {
            var visible = match.Groups["email"].Value.Trim();
            AddCandidate(candidates, match.Index, match.Length, visible, $"mailto:{visible}", $"E-posta gönder: {visible}", false, 20);
        }
    }

    // Adds web URLs, bare domains, and explicit safe social/platform URLs.
    private static void AddUrlCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through URL-like matches and normalizes missing schemes to HTTPS.
        foreach (Match match in UrlRegex().Matches(text))
        {
            var trimmed = TrimTrailingPunctuation(match.Value);
            // Guards the following branch so punctuation remains outside the anchor.
            if (trimmed.Length == 0)
            {
                continue;
            }

            var visible = text.Substring(match.Index, trimmed.Length);
            var href = visible.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || visible.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    ? visible
                    : $"https://{visible}";
            AddCandidate(candidates, match.Index, trimmed.Length, visible, href, $"Bağlantıyı aç: {visible}", true, 30);
        }
    }

    // Adds postal addresses and named locations as Google Maps links.
    private static void AddAddressCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through labeled address/location lines and links the value portion.
        foreach (Match match in AddressLineRegex().Matches(text))
        {
            var valueGroup = match.Groups["value"];
            var visible = TrimTrailingPunctuation(valueGroup.Value.Trim());
            // Guards the following branch so vague labeled sentences are not over-linked.
            if (visible.Length < 8 || !LooksLikeAddressOrLocation(visible))
            {
                continue;
            }

            var href = BuildGoogleMapsSearchUrl(visible);
            AddCandidate(candidates, valueGroup.Index, visible.Length, visible, href, $"Haritada aç: {visible}", true, 40);
        }
    }

    // Adds latitude/longitude pairs as Google Maps links.
    private static void AddCoordinateCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through coordinate pairs and links them to a map search.
        foreach (Match match in CoordinateRegex().Matches(text))
        {
            var visible = match.Value.Trim();
            var href = BuildGoogleMapsSearchUrl(visible);
            AddCandidate(candidates, match.Index, match.Length, visible, href, $"Haritada aç: {visible}", true, 50);
        }
    }

    // Adds phone, SMS, fax, and explicit WhatsApp phone candidates.
    private static void AddPhoneCandidates(string text, List<LinkCandidate> candidates)
    {
        // Iterates through dialable-looking values and chooses the correct action based on nearby context.
        foreach (Match match in PhoneRegex().Matches(text))
        {
            var visible = match.Value.Trim();
            var digits = DigitsOnly(visible);
            // Guards the following branch so non-phone numeric values are left as text.
            if (!IsDialableLength(digits) || !HasPhoneSignal(text, match.Index, visible))
            {
                continue;
            }

            var isWhatsApp = HasContext(text, match.Index, WhatsAppContextRegex());
            var isSms = HasContext(text, match.Index, SmsContextRegex());
            var href = isWhatsApp
                ? $"https://wa.me/{NormalizePhoneForWhatsApp(visible)}"
                : isSms
                    ? $"sms:{NormalizePhoneForTel(visible)}"
                    : $"tel:{NormalizePhoneForTel(visible)}";
            var label = isWhatsApp
                ? $"WhatsApp ile yaz: {visible}"
                : isSms
                    ? $"SMS gönder: {visible}"
                    : $"Telefonla ara: {visible}";
            AddCandidate(candidates, match.Index, match.Length, visible, href, label, isWhatsApp, 60);
        }
    }

    // Adds a candidate when it has a valid span and target.
    private static void AddCandidate(
        List<LinkCandidate> candidates,
        int start,
        int length,
        string visibleText,
        string href,
        string ariaLabel,
        bool opensInNewWindow,
        int priority)
    {
        // Guards the following branch so invalid candidate data never reaches the sanitizer.
        if (start < 0 || length <= 0 || string.IsNullOrWhiteSpace(visibleText) || string.IsNullOrWhiteSpace(href))
        {
            return;
        }

        candidates.Add(new LinkCandidate(start, length, visibleText, href, ariaLabel, opensInNewWindow, priority));
    }

    // Selects deterministic non-overlapping candidates, preferring earlier and higher-priority matches.
    private static IReadOnlyList<LinkCandidate> SelectNonOverlappingCandidates(IReadOnlyList<LinkCandidate> candidates)
    {
        // Creates a stable list of accepted spans.
        var selected = new List<LinkCandidate>();
        // Iterates in a deterministic order so overlapping detections resolve consistently.
        foreach (var candidate in candidates.OrderBy(candidate => candidate.Start).ThenBy(candidate => candidate.Priority).ThenByDescending(candidate => candidate.Length))
        {
            // Guards the following branch so existing selected spans are not double-linked.
            if (selected.Any(existing => SpansOverlap(existing, candidate)))
            {
                continue;
            }

            selected.Add(candidate);
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return selected.OrderBy(candidate => candidate.Start).ToArray();
    }

    // Determines whether two candidate spans intersect.
    private static bool SpansOverlap(LinkCandidate left, LinkCandidate right)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return left.Start < right.Start + right.Length && right.Start < left.Start + left.Length;
    }

    // Determines whether a text node is inside an element that should never be linkified.
    private static bool IsInsideSkippedElement(HtmlNode node)
    {
        // Iterates through ancestors to respect semantic and security boundaries.
        for (var current = node.ParentNode; current is not null; current = current.ParentNode)
        {
            var name = current.Name.ToLowerInvariant();
            // Guards the following branch so existing anchors and code samples remain untouched.
            if (name is "a" or "code" or "pre" or "script" or "style")
            {
                // Returns the computed result to the caller and completes this branch of the workflow.
                return true;
            }
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return false;
    }

    // Determines whether a numeric value has enough context to be treated as a phone/action number.
    private static bool HasPhoneSignal(string text, int index, string visible)
    {
        // Uses nearby labels or visible dialing separators to avoid linking arbitrary IDs.
        return HasContext(text, index, PhoneContextRegex())
            || visible.Contains('+', StringComparison.Ordinal)
            || visible.Any(ch => ch is '(' or ')' or ' ' or '-' or '.');
    }

    // Determines whether text near the candidate contains the requested context.
    private static bool HasContext(string text, int index, Regex contextRegex)
    {
        // Takes a small window around the candidate so labels like Telefon or WhatsApp influence only nearby numbers.
        var start = Math.Max(0, index - 40);
        var length = Math.Min(text.Length - start, 96);
        var context = text.Substring(start, length);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return contextRegex.IsMatch(context);
    }

    // Determines whether a label value is address-like enough to link to Maps.
    private static bool LooksLikeAddressOrLocation(string value)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return AddressMarkerRegex().IsMatch(value) || CoordinateRegex().IsMatch(value);
    }

    // Builds a universal Google Maps search URL for an address or coordinate query.
    private static string BuildGoogleMapsSearchUrl(string query)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}";
    }

    // Normalizes a phone value for the tel and sms URI schemes.
    private static string NormalizePhoneForTel(string visible)
    {
        var digits = DigitsOnly(visible);
        // Guards the following branch so international 00 prefixes become globally dialable.
        if (digits.StartsWith("00", StringComparison.Ordinal) && digits.Length > 4)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return $"+{digits[2..]}";
        }

        // Guards the following branch so explicit plus-prefixed values preserve their international form.
        if (visible.Contains('+', StringComparison.Ordinal) && digits.Length > 0)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return $"+{digits}";
        }

        // Guards the following branch so Turkish local numbers become E.164-like values.
        if (digits.StartsWith('0') && digits.Length == 11)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return $"+90{digits[1..]}";
        }

        // Guards the following branch so Turkish national numbers without leading zero become dialable.
        if (digits.Length == 10 && "23458".Contains(digits[0]))
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return $"+90{digits}";
        }

        // Guards the following branch so already international Turkish values are plus-prefixed.
        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length == 12)
        {
            // Returns the computed result to the caller and completes this branch of the workflow.
            return $"+{digits}";
        }

        // Returns the computed result to the caller and completes this branch of the workflow.
        return digits;
    }

    // Normalizes a phone value for WhatsApp wa.me URLs.
    private static string NormalizePhoneForWhatsApp(string visible)
    {
        var tel = NormalizePhoneForTel(visible);
        // Returns the computed result to the caller and completes this branch of the workflow.
        return tel.TrimStart('+');
    }

    // Extracts only digits from a user-visible phone number.
    private static string DigitsOnly(string value)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return new string(value.Where(char.IsDigit).ToArray());
    }

    // Determines whether a numeric value is within realistic dialable URI bounds.
    private static bool IsDialableLength(string digits)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return digits.Length is >= 7 and <= 15;
    }

    // Removes punctuation that normally terminates a sentence rather than belonging to a link.
    private static string TrimTrailingPunctuation(string value)
    {
        // Returns the computed result to the caller and completes this branch of the workflow.
        return value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']');
    }

    // Defines the candidate data needed to replace one text segment with an anchor.
    private sealed record LinkCandidate(
        int Start,
        int Length,
        string VisibleText,
        string Href,
        string AriaLabel,
        bool OpensInNewWindow,
        int Priority);

    // Defines a visible text segment inside an HTML container.
    private sealed record TextSegment(HtmlNode Node, int Start, string Text);

    [GeneratedRegex(@"(?<local>[A-Z0-9._%+-]+)\s*(?:\[at\]|\(at\)|\bat\b)\s*(?<domain>[A-Z0-9-]+(?:\s*(?:\[dot\]|\(dot\)|\bdot\b)\s*[A-Z0-9-]+)*)\s*(?:\[dot\]|\(dot\)|\bdot\b)\s*(?<tld>[A-Z]{2,})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for obfuscated e-mail addresses.
    private static partial Regex ObfuscatedEmailRegex();

    [GeneratedRegex(@"\s*(?:\[dot\]|\(dot\)|\bdot\b)\s*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for normalizing obfuscated domain separators.
    private static partial Regex DotTokenRegex();

    [GeneratedRegex(@"(?<![\w.+-])(?<email>[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,})(?![\w.-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for ordinary e-mail addresses.
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<![@\w])(?<url>(?:https?://|www\.)[^\s<>()]+|(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+(?:com\.tr|org\.tr|net\.tr|gov\.tr|edu\.tr|com|net|org|io|ai|co|biz|info|tr)(?:/[^\s<>()]*)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for web URLs and bare domains.
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"(?im)(?<![\p{L}\p{N}])(?:Adres|Konum|Lokasyon|Ofis|Merkez|Şube|Sube|Yerleşke|Yerleske)(?:\s*\([^)]+\))?\s*:\s*(?<value>[^\r\n<]{8,220})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for labeled postal addresses and locations.
    private static partial Regex AddressLineRegex();

    [GeneratedRegex(@"(?<!\d)(?<lat>-?(?:[0-8]?\d(?:\.\d+)?|90(?:\.0+)?))\s*,\s*(?<lng>-?(?:1[0-7]\d(?:\.\d+)?|[0-9]?\d(?:\.\d+)?|180(?:\.0+)?))(?!\d)", RegexOptions.CultureInvariant)]
    // Provides the reusable regex for latitude and longitude pairs.
    private static partial Regex CoordinateRegex();

    [GeneratedRegex(@"(?<![\w])(?:\+?\d{1,3}[\s.-]?)?(?:\(?0?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{2}[\s.-]?\d{2}|444[\s.-]?\d[\s.-]?\d{3})(?![\w])", RegexOptions.CultureInvariant)]
    // Provides the reusable regex for dialable phone-like values.
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(?:Telefon|Tel|GSM|Mobil|Cep|Çağrı|Cagri|Faks|Fax|WhatsApp|Whatsapp|SMS|Mesaj)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for phone-adjacent labels.
    private static partial Regex PhoneContextRegex();

    [GeneratedRegex(@"\b(?:WhatsApp|Whatsapp)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for WhatsApp-adjacent labels.
    private static partial Regex WhatsAppContextRegex();

    [GeneratedRegex(@"\b(?:SMS|Mesaj)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for SMS-adjacent labels.
    private static partial Regex SmsContextRegex();

    [GeneratedRegex(@"\b(?:Mah\.?|Mahallesi|Cad\.?|Caddesi|Sok\.?|Sokağı|Sk\.?|Bulvar|Blv\.?|No\s*:|Kat\s*:|İstanbul|Istanbul|Ankara|İzmir|Izmir|Türkiye|Turkey|Plaza|Blok|Daire)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    // Provides the reusable regex for address-like marker terms.
    private static partial Regex AddressMarkerRegex();
}
