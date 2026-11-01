using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NEXORA_MCP
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  SKILLS REGISTRY
    //
    //  Source of truth: <exe>\skills\<skill-id>\{manifest.json, SKILL.md}
    //  One loader is shared by the dashboard (WebDashboardForm) and the MCP
    //  server tools (SkillsTool) so both always read the same registry.
    //
    //  Security posture:
    //    · skill ids are validated against a strict pattern (no paths, no ../)
    //    · only files inside the registry root are read
    //    · manifest and instruction sizes are capped
    //    · malformed / duplicate / cyclic entries are reported, never executed
    //    · filesystem paths are never returned to callers
    // ═══════════════════════════════════════════════════════════════════════════

    public sealed class SkillRecord
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
        public string Category { get; init; } = "";
        public string Description { get; init; } = "";
        public string Purpose { get; init; } = "";
        public string Status { get; init; } = "available";
        public string Trust { get; init; } = "trusted";
        public string Author { get; init; } = "";
        public string Created { get; init; } = "";
        public string Updated { get; init; } = "";
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> RequiresTools { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Provides { get; init; } = Array.Empty<string>();
        public string Instructions { get; init; } = "";
        public bool HasInstructions { get; init; }
        public bool HasError { get; init; }
        public bool HasMissingDependencies { get; init; }
        public bool InCycle { get; init; }
        public string Source { get; init; } = "built-in";

        public string EffectiveStatus => HasError ? "invalid" : Status;
    }

    public sealed class SkillIssue
    {
        public string SkillId { get; init; } = "";
        public string Severity { get; init; } = "warning"; // warning | error
        public string Message { get; init; } = "";
    }

    public sealed class SkillsSnapshot
    {
        public List<SkillRecord> Skills { get; } = new();
        public List<SkillIssue> Issues { get; } = new();
        public DateTime LoadedAt { get; init; } = DateTime.Now;
    }

    public sealed class SkillsRegistry
    {
        public const int MaxManifestBytes = 64 * 1024;
        public const int MaxInstructionsBytes = 128 * 1024;
        public const int MaxSkills = 500;

        private static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{1,63}$", RegexOptions.Compiled);
        private static readonly Regex VersionPattern = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);

        private readonly object _gate = new();
        private SkillsSnapshot _snapshot = new();
        private DateTime _lastLoadUtc = DateTime.MinValue;

        // Skills are a live folder: drop a new skill in while the server or the
        // dashboard is running and it becomes visible at the next lookup, at
        // most 10s later. Load() of 26 skills costs a few ms, so this is cheap.
        private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(10);

        public static SkillsRegistry Instance { get; } = new();

        public static string DefaultRoot => Path.Combine(AppContext.BaseDirectory, "skills");

        public SkillsSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    if (DateTime.UtcNow - _lastLoadUtc > StaleAfter)
                    {
                        try
                        {
                            _snapshot = Load(DefaultRoot);
                            _lastLoadUtc = DateTime.UtcNow;
                        }
                        catch
                        {
                            // Disk hiccup while re-reading — keep serving the
                            // previous snapshot rather than failing the call.
                        }
                    }
                    return _snapshot;
                }
            }
        }

        public void Refresh() => Refresh(DefaultRoot);

        public void Refresh(string root)
        {
            var snapshot = Load(root);
            lock (_gate)
            {
                _snapshot = snapshot;
                _lastLoadUtc = DateTime.UtcNow;
            }
        }

        /// <summary>Loads and fully validates a registry folder (safe to call with any path for tests).</summary>
        public static SkillsSnapshot Load(string root)
        {
            var snapshot = new SkillsSnapshot();

            if (!Directory.Exists(root))
            {
                snapshot.Issues.Add(new SkillIssue
                {
                    SkillId = "",
                    Severity = "error",
                    Message = "Skills registry folder is missing."
                });
                return snapshot;
            }

            var builders = new List<Builder>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList(); }
            catch (Exception ex)
            {
                snapshot.Issues.Add(new SkillIssue { SkillId = "", Severity = "error", Message = $"Registry folder cannot be read: {ex.Message}" });
                return snapshot;
            }

            foreach (var dir in dirs)
            {
                string folder = Path.GetFileName(dir);
                if (builders.Count >= MaxSkills)
                {
                    snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "warning", Message = $"Skill limit ({MaxSkills}) reached; remaining folders were not loaded." });
                    break;
                }

                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                {
                    snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = "manifest.json is missing." });
                    continue;
                }

                Builder? b = null;
                try
                {
                    var info = new FileInfo(manifestPath);
                    if (info.Length > MaxManifestBytes)
                    {
                        snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = "manifest.json exceeds the size limit." });
                        continue;
                    }

                    using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var e = doc.RootElement;
                    if (e.ValueKind != JsonValueKind.Object)
                    {
                        snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = "manifest.json must contain a JSON object." });
                        continue;
                    }

                    b = Builder.FromJson(folder, e, snapshot.Issues);
                    if (b == null) continue;

                    if (!seenIds.Add(b.Id))
                    {
                        snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "error", Message = "Duplicate skill id; this entry was ignored." });
                        continue;
                    }

                    // SKILL.md — instructions are optional, but reported when missing.
                    var skillPath = Path.Combine(dir, "SKILL.md");
                    if (File.Exists(skillPath))
                    {
                        var fi = new FileInfo(skillPath);
                        if (fi.Length > MaxInstructionsBytes)
                        {
                            b.HasError = true;
                            snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "error", Message = "SKILL.md exceeds the size limit; instructions were not loaded." });
                        }
                        else
                        {
                            string text = File.ReadAllText(skillPath);
                            if (text.Trim().Length == 0)
                            {
                                snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "warning", Message = "SKILL.md is empty." });
                            }
                            else
                            {
                                b.Instructions = text;
                                b.HasInstructions = true;
                            }
                        }
                    }
                    else
                    {
                        snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "warning", Message = "SKILL.md is missing; instructions are unavailable." });
                    }

                    builders.Add(b);
                }
                catch (JsonException ex)
                {
                    snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = $"manifest.json is not valid JSON: {ex.Message}" });
                }
                catch (IOException ex)
                {
                    snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = $"Skill files could not be read: {ex.Message}" });
                }
                catch (UnauthorizedAccessException)
                {
                    snapshot.Issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = "Skill files are not readable (access denied)." });
                }
            }

            var byId = builders.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var b in builders)
            {
                foreach (var dep in b.Dependencies)
                {
                    if (!byId.ContainsKey(dep))
                    {
                        b.MissingDependencies = true;
                        snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "warning", Message = $"Dependency \"{dep}\" is not registered." });
                    }
                }

                if (HasCycle(b.Id, b.Id, byId, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    b.InCycle = true;
                    b.HasError = true;
                    snapshot.Issues.Add(new SkillIssue { SkillId = b.Id, Severity = "error", Message = "Dependency cycle detected; this skill is disabled until the graph is fixed." });
                }
            }

            foreach (var b in builders.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                snapshot.Skills.Add(new SkillRecord
                {
                    Id = b.Id,
                    Name = b.Name,
                    Version = b.Version,
                    Category = b.Category,
                    Description = b.Description,
                    Purpose = b.Purpose,
                    Status = b.Status,
                    Trust = b.Trust,
                    Author = b.Author,
                    Created = b.Created,
                    Updated = b.Updated,
                    Tags = b.Tags,
                    Dependencies = b.Dependencies,
                    RequiresTools = b.RequiresTools,
                    Provides = b.Provides,
                    Instructions = b.Instructions,
                    HasInstructions = b.HasInstructions,
                    HasError = b.HasError,
                    HasMissingDependencies = b.MissingDependencies,
                    InCycle = b.InCycle
                });
            }

            return snapshot;
        }

        private static bool HasCycle(
            string start,
            string current,
            IReadOnlyDictionary<string, Builder> byId,
            HashSet<string> visited)
        {
            if (!byId.TryGetValue(current, out var node)) return false;
            foreach (var dep in node.Dependencies)
            {
                if (string.Equals(dep, start, StringComparison.OrdinalIgnoreCase)) return true;
                if (!visited.Add(dep)) continue;
                if (HasCycle(start, dep, byId, visited)) return true;
            }
            return false;
        }

        // ── query helpers (shared by MCP tools, dashboard and self-check) ───────

        public static IReadOnlyList<SkillRecord> Search(
            SkillsSnapshot snapshot,
            string? query = null,
            string? category = null,
            string? status = null,
            IEnumerable<string>? tags = null,
            int limit = 20)
        {
            IEnumerable<SkillRecord> items = snapshot.Skills;

            if (!string.IsNullOrWhiteSpace(category) &&
                !category.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(s => s.Category.Equals(category.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !status.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var wanted = status.Trim().ToLowerInvariant();
                items = items.Where(s => s.EffectiveStatus.Equals(wanted, StringComparison.OrdinalIgnoreCase));
            }

            var tagList = (tags ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim().ToLowerInvariant())
                .ToList();

            if (tagList.Count > 0)
            {
                items = items.Where(s => tagList.All(t =>
                    s.Tags.Any(x => x.Equals(t, StringComparison.OrdinalIgnoreCase))));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var tokens = query.ToLowerInvariant()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                items = items.Where(s =>
                {
                    string haystack = $"{s.Id} {s.Name} {s.Description} {s.Category} {string.Join(' ', s.Tags)}".ToLowerInvariant();
                    return tokens.All(t => haystack.Contains(t, StringComparison.Ordinal));
                });
            }

            int take = Math.Clamp(limit, 1, 100);
            return items
                .OrderBy(s => s.EffectiveStatus == "available" ? 0 : 1)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .ToList();
        }

        public static SkillRecord? Find(SkillsSnapshot snapshot, string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return snapshot.Skills.FirstOrDefault(s =>
                s.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recommends skills for a task. Dependencies are pulled in first and the
        /// result is cycle-safe and free of duplicates.
        /// </summary>
        public static List<(SkillRecord Skill, string Reason)> Plan(
            SkillsSnapshot snapshot,
            string task,
            int maxSkills = 5)
        {
            var result = new List<(SkillRecord, string)>();
            if (string.IsNullOrWhiteSpace(task)) return result;

            var tokens = task.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', ';', ':', '\n', '\t', '/', '(', ')', '-', '_' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 2)
                .Distinct()
                .ToList();

            var scored = new List<(SkillRecord Skill, int Score, List<string> Matches)>();
            foreach (var skill in snapshot.Skills)
            {
                if (skill.EffectiveStatus != "available") continue;

                int score = 0;
                var matches = new List<string>();

                foreach (var token in tokens)
                {
                    bool inTags = skill.Tags.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase));
                    bool inName = skill.Name.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                  skill.Id.Contains(token, StringComparison.OrdinalIgnoreCase);
                    bool inText = skill.Description.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                  skill.Purpose.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                                  skill.Category.Contains(token, StringComparison.OrdinalIgnoreCase);

                    if (inTags) { score += 3; matches.Add(token); }
                    else if (inName) { score += 2; matches.Add(token); }
                    else if (inText) { score += 1; matches.Add(token); }
                }

                if (score > 0 && matches.Count > 0)
                    scored.Add((skill, score, matches.Distinct().Take(5).ToList()));
            }

            var byId = snapshot.Skills.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddWithDependencies(SkillRecord skill, string reason, int depth = 0)
            {
                if (depth > 10 || result.Count >= Math.Clamp(maxSkills, 1, 12)) return;
                if (!added.Add(skill.Id)) return;

                foreach (var depId in skill.Dependencies)
                {
                    if (byId.TryGetValue(depId, out var dep) &&
                        dep.EffectiveStatus == "available" &&
                        !dep.InCycle)
                    {
                        AddWithDependencies(dep, $"required by {skill.Name}", depth + 1);
                    }
                }

                result.Add((skill, reason));
            }

            foreach (var item in scored.OrderByDescending(x => x.Score))
            {
                if (result.Count >= Math.Clamp(maxSkills, 1, 12)) break;
                AddWithDependencies(item.Skill, $"matched: {string.Join(", ", item.Matches)}");
            }

            return result;
        }

        // ── manifest parsing ───────────────────────────────────────────────────

        private sealed class Builder
        {
            public string Id = "";
            public string Name = "";
            public string Version = "1.0.0";
            public string Category = "General Development";
            public string Description = "";
            public string Purpose = "";
            public string Status = "available";
            public string Trust = "trusted";
            public string Author = "";
            public string Created = "";
            public string Updated = "";
            public List<string> Tags = new();
            public List<string> Dependencies = new();
            public List<string> RequiresTools = new();
            public List<string> Provides = new();
            public string Instructions = "";
            public bool HasInstructions;
            public bool HasError;
            public bool MissingDependencies;
            public bool InCycle;

            public static Builder? FromJson(string folder, JsonElement e, List<SkillIssue> issues)
            {
                string id = GetString(e, "id").Trim().ToLowerInvariant();
                if (id.Length == 0)
                {
                    issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = "manifest.json has no \"id\"." });
                    return null;
                }
                if (!IdPattern.IsMatch(id))
                {
                    issues.Add(new SkillIssue { SkillId = folder, Severity = "error", Message = $"Invalid skill id \"{id}\": use lowercase letters, digits and dashes." });
                    return null;
                }
                if (!string.Equals(id, folder, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new SkillIssue { SkillId = id, Severity = "warning", Message = $"Skill id and folder name differ (\"{folder}\")." });
                }

                string name = GetString(e, "name").Trim();
                if (name.Length == 0)
                {
                    issues.Add(new SkillIssue { SkillId = id, Severity = "error", Message = "manifest.json has no \"name\"." });
                    return null;
                }

                string description = GetString(e, "description").Trim();
                if (description.Length == 0)
                {
                    issues.Add(new SkillIssue { SkillId = id, Severity = "error", Message = "manifest.json has no \"description\"." });
                    return null;
                }

                string version = GetString(e, "version", "1.0.0").Trim();
                var b = new Builder
                {
                    Id = id,
                    Name = name,
                    Description = description,
                    Version = version,
                    Category = GetString(e, "category", "General Development").Trim(),
                    Purpose = GetString(e, "purpose").Trim(),
                    Author = GetString(e, "author").Trim(),
                    Created = GetString(e, "created").Trim(),
                    Updated = GetString(e, "updated").Trim(),
                    Tags = GetStringArray(e, "tags"),
                    Dependencies = GetStringArray(e, "dependencies").Select(x => x.ToLowerInvariant()).ToList(),
                    RequiresTools = GetStringArray(e, "requiresTools"),
                    Provides = GetStringArray(e, "provides")
                };

                if (b.Category.Length == 0) b.Category = "General Development";

                if (!VersionPattern.IsMatch(version))
                {
                    b.HasError = true;
                    issues.Add(new SkillIssue { SkillId = id, Severity = "error", Message = $"Unsupported version \"{version}\": expected MAJOR.MINOR.PATCH." });
                }

                string status = GetString(e, "status", "available").Trim().ToLowerInvariant();
                if (status is "available" or "disabled" or "review") b.Status = status;
                else
                {
                    issues.Add(new SkillIssue { SkillId = id, Severity = "warning", Message = $"Unknown status \"{status}\"; treated as available." });
                    b.Status = "available";
                }

                string trust = GetString(e, "trust", "trusted").Trim().ToLowerInvariant();
                if (trust is "trusted" or "untrusted") b.Trust = trust;
                else
                {
                    issues.Add(new SkillIssue { SkillId = id, Severity = "warning", Message = $"Unknown trust level \"{trust}\"; treated as untrusted." });
                    b.Trust = "untrusted";
                }

                return b;
            }
        }

        private static string GetString(JsonElement e, string name, string fallback = "")
        {
            if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? fallback;
            return fallback;
        }

        private static List<string> GetStringArray(JsonElement e, string name)
        {
            var list = new List<string>();
            if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return list;
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = (item.GetString() ?? "").Trim();
                    if (s.Length > 0) list.Add(s);
                }
            }
            return list;
        }
    }
}
