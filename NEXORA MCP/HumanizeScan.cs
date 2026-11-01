using System.Text.RegularExpressions;

namespace NEXORA_MCP
{
    /// <summary>
    /// Local QA gate for humanized text — C# port of the key checks from
    /// ToHuman's public ai-tells scanner, plus the ZeroGPT-validated casual
    /// recipe invariants. Runs instantly in-process; no network.
    /// Returns a list of human-readable issues (empty = clean).
    /// </summary>
    public static class HumanizeScan
    {
        private static readonly string[] SlopWords =
        {
            "delve", "tapestry", "testament", "pivotal", "meticulous", "seamless",
            "leverage", "foster", "robust", "holistic", "paradigm", "synergy",
            "transformative", "groundbreaking", "cutting-edge", "game-changer",
            "ever-evolving", "fast-paced", "top-notch", "world-class", "hassle-free",
            "user-friendly", "actionable", "impactful", "insightful", "aforementioned",
            "skyrocket", "innovative", "unlock", "navigate", "glacial", "curveball",
            "resilience", "resilient", "empowering", "moreover", "furthermore",
            "universal deadline", "moves the needle", "one-size-fits-all",
            "needle", "spiral"
        };

        private static readonly Regex[] ClicheTemplates =
        {
            new(@"\bthe (real )?(key|trick|secret)\b(\?|\s+is\b)", RegexOptions.IgnoreCase),
            new(@"\bnot just\b.{0,60}\bbut\b", RegexOptions.IgnoreCase),
            new(@"\bnot only\b.{0,60}\bbut also\b", RegexOptions.IgnoreCase),
            new(@"\bwhether you'?re\b", RegexOptions.IgnoreCase),
            new(@"\bmore (important|relevant) than ever\b", RegexOptions.IgnoreCase),
            new(@"\bcomes down to\b", RegexOptions.IgnoreCase),
            new(@"\bat the end of the day\b", RegexOptions.IgnoreCase),
            new(@"\bwhen it comes to\b", RegexOptions.IgnoreCase),
            new(@"\bno (master|universal|single|fixed|real|shared)\b.{0,14}\b(calendar|schedule|clock|deadline|timeline|finish line|stopwatch|map)\b", RegexOptions.IgnoreCase),
            new(@"\bbrains? (are|is) wired differently\b", RegexOptions.IgnoreCase),
            new(@"\bjourney\b", RegexOptions.IgnoreCase),
            new(@"\bfull(er)? circle\b", RegexOptions.IgnoreCase),
            new(@"\bthe (needle|line|bar)\b.{0,20}\bmoves\b", RegexOptions.IgnoreCase)
        };

        /// <summary>Runs all local checks. Returns short issue strings.
        /// sourceWords = word count of the original input, for the length
        /// drift check (0 disables it).</summary>
        public static List<string> Check(string text, int sourceWords = 0)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return issues;

            var lower = text.ToLowerInvariant();

            // Length drift: rewrite must not balloon (recipe rule: <=1.2x).
            var words = Regex.Matches(text, @"\S+").Count;
            if (sourceWords > 0 && words > Math.Max(1.2 * sourceWords, sourceWords + 10))
                issues.Add($"Text ballooned to {words} words, original was about {sourceWords} — cut it back to roughly the same length");

            var hitWords = SlopWords.Where(w => lower.Contains(w)).ToList();
            if (hitWords.Count > 0)
                issues.Add("AI-favorite word(s) present: " + string.Join(", ", hitWords));

            foreach (var rx in ClicheTemplates)
            {
                var m = rx.Match(text);
                if (m.Success)
                    issues.Add($"Cliché template present: \"{m.Value}\"");
            }

            if (text.Contains('—') || text.Contains('–'))
                issues.Add("Dash character present");
            if (text.Contains(';'))
                issues.Add("Semicolon present");

            // three-item parallel list  ("X, Y, and Z")
            if (Regex.IsMatch(text, @"\b\w[\w\s]{0,24},\s+\w[\w\s]{0,24},\s+and\s+\w", RegexOptions.IgnoreCase))
                issues.Add("Three-item list (X, Y, and Z) present");

            // sentence-length uniformity: coefficient of variation
            var sentences = Regex.Split(text.Trim(), @"(?<=[.!?])\s+")
                .Where(s => s.Trim().Length > 0).ToArray();
            if (sentences.Length >= 4)
            {
                var counts = sentences.Select(s => s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
                    .ToArray();
                var mean = counts.Average();
                var sd = Math.Sqrt(counts.Average(c => (c - mean) * (c - mean)));
                var cv = mean > 0 ? sd / mean : 0;
                if (cv < 0.30)
                    issues.Add($"Sentence rhythm too uniform (CV {cv:0.00} < 0.30)");
            }

            // repeated consecutive sentence openers
            for (var i = 1; i < sentences.Length; i++)
            {
                var w1 = Regex.Split(sentences[i - 1].Trim(), @"\b")[0];
                var w2 = Regex.Split(sentences[i].Trim(), @"\b")[0];
                if (!string.IsNullOrWhiteSpace(w1) &&
                    string.Equals(w1, w2, StringComparison.OrdinalIgnoreCase))
                    issues.Add($"Sentences {i} and {i + 1} start with the same word (\"{w1}\")");
            }

            return issues;
        }
    }
}
