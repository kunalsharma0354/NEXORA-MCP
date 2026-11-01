using System.Text.Json;

namespace NEXORA_MCP
{
    /// <summary>
    /// `NEXORA MCP.exe --skills-check`
    ///
    /// Executes the real registry + the real MCP tool methods against the shipped
    /// skills folder, then exercises validation against a throwaway malformed
    /// registry. Prints PASS/FAIL per check and exits non-zero on failure.
    /// The repo has no test framework, so this is the executable verification.
    /// </summary>
    internal static class SkillsCheck
    {
        public static int Run()
        {
            // WinExe has no console of its own: attach to the launching shell and
            // tee every line into a log file so results are always inspectable.
            try { AttachConsole(-1); } catch { /* no parent console */ }

            string logPath = Path.Combine(Path.GetTempPath(), "nexora-skills-check.log");
            var buffer = new StringWriter();
            var previous = Console.Out;
            Console.SetOut(new TeeWriter(previous, buffer));

            int failures = 0;

            void Check(string name, bool ok, string detail = "")
            {
                Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? " — " + detail : "")}");
                if (!ok) failures++;
            }

            var root = SkillsRegistry.DefaultRoot;
            Console.WriteLine($"registry: {root}");
            Console.WriteLine();

            var snap = SkillsRegistry.Load(root);

            Check("registry loads skills", snap.Skills.Count > 0, $"{snap.Skills.Count} skills");
            var errors = snap.Issues.Where(i => i.Severity == "error").ToList();
            Check("shipped registry has no errors", errors.Count == 0,
                errors.Count == 0 ? "" : string.Join(" | ", errors.Select(e => $"{e.SkillId}: {e.Message}")));

            Check("all shipped skills have instructions",
                snap.Skills.All(s => s.HasInstructions),
                string.Join(",", snap.Skills.Where(s => !s.HasInstructions).Select(s => s.Id)));

            Check("all dependencies resolve",
                snap.Skills.All(s => !s.HasMissingDependencies),
                string.Join(",", snap.Skills.Where(s => s.HasMissingDependencies).Select(s => s.Id)));

            Check("no dependency cycles", snap.Skills.All(s => !s.InCycle));

            // ── search / filters ───────────────────────────────────────────────
            var search = SkillsRegistry.Search(snap, "security");
            Check("search(\"security\") returns matches", search.Count > 0, $"{search.Count} result(s)");

            var category = snap.Skills[0].Category;
            var byCategory = SkillsRegistry.Search(snap, null, category);
            Check($"category filter \"{category}\"",
                byCategory.Count > 0 && byCategory.All(s => s.Category == category),
                $"{byCategory.Count} result(s)");

            var available = SkillsRegistry.Search(snap, null, null, "available", null, 100);
            Check("status filter available", available.Count > 0 && available.All(s => s.EffectiveStatus == "available"),
                $"{available.Count} result(s)");

            var tag = snap.Skills.SelectMany(s => s.Tags).FirstOrDefault() ?? "";
            var byTag = SkillsRegistry.Search(snap, null, null, null, new[] { tag }, 100);
            Check($"tag filter \"{tag}\"", byTag.Count > 0, $"{byTag.Count} result(s)");

            // ── MCP tool surface ───────────────────────────────────────────────
            var loadable = snap.Skills.FirstOrDefault(s => s.HasInstructions);
            if (loadable != null)
            {
                var (ok, json) = TryJson(SkillsTool.Load(loadable.Id));
                Check("nexora_skills_load returns instructions",
                    ok && json.RootElement.GetProperty("ok").GetBoolean() &&
                    json.RootElement.GetProperty("instructions").GetString()!.Length > 100,
                    loadable.Id);
            }
            else
            {
                Check("nexora_skills_load returns instructions", false, "no skill with instructions");
            }

            var unknown = TryJson(SkillsTool.Load("no-such-skill"));
            Check("nexora_skills_load rejects unknown id",
                unknown.ok && !unknown.json.RootElement.GetProperty("ok").GetBoolean());

            var traversal = TryJson(SkillsTool.Load("../../windows/win.ini"));
            Check("nexora_skills_load rejects traversal id",
                traversal.ok && !traversal.json.RootElement.GetProperty("ok").GetBoolean());

            var details = TryJson(SkillsTool.Details(skill_id: snap.Skills[0].Id));
            Check("nexora_skills_details returns metadata",
                details.ok && details.json.RootElement.GetProperty("ok").GetBoolean());

            var plan = TryJson(SkillsTool.Plan("build a responsive website with accessible forms and tests", 5));
            int planCount = plan.ok ? plan.json.RootElement.GetProperty("count").GetInt32() : 0;
            Check("nexora_skills_plan recommends skills", planCount > 0, $"{planCount} skill(s)");

            // dependency ordering: every dependency of a planned skill appears earlier
            bool ordered = true;
            if (plan.ok)
            {
                var ids = plan.json.RootElement.GetProperty("skills").EnumerateArray()
                    .Select(x => x.GetProperty("id").GetString() ?? "").ToList();
                foreach (var id in ids)
                {
                    var skill = snap.Skills.FirstOrDefault(s => s.Id == id);
                    if (skill == null) continue;
                    foreach (var dep in skill.Dependencies)
                    {
                        if (ids.Contains(dep) && ids.IndexOf(dep) > ids.IndexOf(id)) ordered = false;
                    }
                }
            }
            Check("plan orders dependencies first", ordered);

            var toolSearch = TryJson(SkillsTool.Search("testing"));
            Check("nexora_skills_search returns compact results",
                toolSearch.ok && toolSearch.json.RootElement.GetProperty("count").GetInt32() > 0);

            // ── malformed registry handling ────────────────────────────────────
            string temp = Path.Combine(Path.GetTempPath(), "nexora-skills-check-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temp);
                WriteSkill(temp, "valid-skill", "Valid Skill",
                    """
                    { "id": "valid-skill", "name": "Valid Skill", "version": "1.0.0",
                      "category": "General Development", "description": "A valid test skill.",
                      "tags": ["test"], "dependencies": [] }
                    """, "# Valid Skill\n\nDo the thing.\n");

                WriteSkill(temp, "duplicate", "Duplicate",
                    """
                    { "id": "valid-skill", "name": "Duplicate", "version": "1.0.0",
                      "category": "General Development", "description": "Duplicate id." }
                    """, "# Dup\n");

                Directory.CreateDirectory(Path.Combine(temp, "broken-json"));
                File.WriteAllText(Path.Combine(temp, "broken-json", "manifest.json"), "{ not json ,,,");

                Directory.CreateDirectory(Path.Combine(temp, "no-instructions"));
                File.WriteAllText(Path.Combine(temp, "no-instructions", "manifest.json"),
                    """
                    { "id": "no-instructions", "name": "No Instructions", "version": "1.0.0",
                      "category": "General Development", "description": "Missing SKILL.md." }
                    """);

                WriteSkill(temp, "cycle-a", "Cycle A",
                    """
                    { "id": "cycle-a", "name": "Cycle A", "version": "1.0.0",
                      "category": "General Development", "description": "Cyclic.", "dependencies": ["cycle-b"] }
                    """, "# A\n");
                WriteSkill(temp, "cycle-b", "Cycle B",
                    """
                    { "id": "cycle-b", "name": "Cycle B", "version": "1.0.0",
                      "category": "General Development", "description": "Cyclic.", "dependencies": ["cycle-a"] }
                    """, "# B\n");

                Directory.CreateDirectory(Path.Combine(temp, "traversal"));
                File.WriteAllText(Path.Combine(temp, "traversal", "manifest.json"),
                    """
                    { "id": "../evil", "name": "Evil", "version": "1.0.0",
                      "category": "General Development", "description": "Path traversal attempt." }
                    """);

                Directory.CreateDirectory(Path.Combine(temp, "bad-version"));
                WriteSkill(temp, "bad-version", "Bad Version",
                    """
                    { "id": "bad-version", "name": "Bad Version", "version": "v1",
                      "category": "General Development", "description": "Unsupported version." }
                    """, "# Bad\n");

                var bad = SkillsRegistry.Load(temp);

                Check("temp registry keeps the valid skill", bad.Skills.Any(s => s.Id == "valid-skill"));
                Check("duplicate id is rejected", bad.Issues.Any(i => i.Message.Contains("Duplicate", StringComparison.OrdinalIgnoreCase)));
                Check("invalid JSON is reported", bad.Issues.Any(i => i.Message.Contains("not valid JSON", StringComparison.OrdinalIgnoreCase)));
                Check("missing SKILL.md is reported", bad.Issues.Any(i => i.SkillId == "no-instructions" && i.Message.Contains("SKILL.md", StringComparison.OrdinalIgnoreCase)));
                Check("dependency cycle is detected", bad.Skills.Where(s => s.InCycle).Select(s => s.Id).OrderBy(x => x).SequenceEqual(new[] { "cycle-a", "cycle-b" }),
                    string.Join(",", bad.Skills.Where(s => s.InCycle).Select(s => s.Id)));
                Check("path-traversal id is rejected", bad.Issues.Any(i => i.Message.Contains("Invalid skill id", StringComparison.OrdinalIgnoreCase)));
                Check("unsupported version is rejected",
                    bad.Skills.Any(s => s.Id == "bad-version" && s.HasError));
                Check("cyclic skills are not loadable",
                    bad.Skills.Where(s => s.InCycle).All(s => !s.HasInstructions || s.HasError) &&
                    TryJson(SkillsTool.Load("cycle-a")).json.RootElement.GetProperty("ok").GetBoolean() == false);
                Check("valid skill in a broken registry still loads",
                    SkillsRegistry.Search(bad, "valid").Count == 1);
            }
            finally
            {
                try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
            }

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
            Console.WriteLine($"log: {logPath}");

            Console.SetOut(previous);
            try { File.WriteAllText(logPath, buffer.ToString()); } catch { /* best effort */ }

            return failures == 0 ? 0 : 1;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        private sealed class TeeWriter : TextWriter
        {
            private readonly TextWriter _first;
            private readonly TextWriter _second;

            public TeeWriter(TextWriter first, TextWriter second)
            {
                _first = first;
                _second = second;
            }

            public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

            public override void Write(char value)
            {
                _first.Write(value);
                _second.Write(value);
            }

            public override void Write(string? value)
            {
                _first.Write(value);
                _second.Write(value);
            }

            public override void WriteLine(string? value)
            {
                _first.WriteLine(value);
                _second.WriteLine(value);
            }

            public override void Flush()
            {
                _first.Flush();
                _second.Flush();
            }
        }

        private static void WriteSkill(string root, string folder, string name, string manifest, string instructions)
        {
            var dir = Path.Combine(root, folder);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), manifest);
            File.WriteAllText(Path.Combine(dir, "SKILL.md"), instructions);
        }

        private static (bool ok, JsonDocument json) TryJson(string raw)
        {
            try { return (true, JsonDocument.Parse(raw)); }
            catch { return (false, JsonDocument.Parse("{\"ok\":false}")); }
        }
    }
}
