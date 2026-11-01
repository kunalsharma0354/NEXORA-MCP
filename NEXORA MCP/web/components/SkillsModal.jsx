import { useEffect, useMemo, useState } from 'react';
import { hasBridge, useNxSkills } from '../lib/bridge';

// ─── Skills overlay ─────────────────────────────────────────────────────
// Topbar SKILLS button → C# registry snapshot → modal listing every skill
// in the shipped registry: search, category groups, status pills, expandable
// metadata, and the full SKILL.md instructions for each skill.
export default function SkillsModal() {
  const { open, loading, list, instructions, close, requestInstructions, clearInstructions } = useNxSkills();
  const [query, setQuery] = useState('');
  const [expanded, setExpanded] = useState(null);

  const groups = useMemo(() => filterAndGroup(list?.skills, query), [list, query]);

  // Reset view state whenever a fresh listing arrives.
  useEffect(() => {
    if (open && list) {
      setQuery('');
      setExpanded(null);
    }
  }, [open, list]);

  // ESC dismisses the instructions viewer first, then the modal itself.
  useEffect(() => {
    if (!open) return undefined;
    const onKey = (e) => {
      if (e.key !== 'Escape') return;
      e.preventDefault();
      e.stopPropagation();
      if (instructions) clearInstructions();
      else close();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [open, instructions, close, clearInstructions]);

  if (!open) return null;

  return (
    <div className="skills-overlay" role="dialog" aria-modal="true">
      <div className="skills-backdrop" onClick={close} />
      <div className="skills-panel">
        <header className="skills-head nx-nodrag">
          <div className="skills-head-main">
            <h2>SKILLS</h2>
            <span className="mono skills-count">
              {list
                ? `${list.count} loaded${list.issueCount ? ` · ${list.issueCount} issue(s)` : ''}`
                : 'loading registry…'}
            </span>
          </div>
          <button className="win-btn nx-nodrag" title="Close" onClick={close}>
            <svg width="10" height="10" viewBox="0 0 10 10"><path d="M1 1l8 8M9 1l-8 8" stroke="currentColor" strokeWidth="1.4" /></svg>
          </button>
        </header>

        {list && (
          <div className="skills-search nx-nodrag">
            <input
              autoFocus
              type="text"
              placeholder="Filter by name, id, tag or purpose…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              spellCheck="false"
            />
          </div>
        )}

        <div className="skills-body">
          {!hasBridge() && (
            <div className="skills-empty mono">No WebView bridge — open the dashboard from the NEXORA app.</div>
          )}
          {hasBridge() && loading && !list && <div className="skills-empty mono">Loading skills…</div>}
          {hasBridge() && list && groups.length === 0 && (
            <div className="skills-empty mono">No skills match the filter.</div>
          )}

          {/* Instruction viewer pinned above the list so the context stays visible */}
          {instructions && (
            <div className="skill-instructions">
              <div className="skill-instructions-head">
                <h3>
                  {instructions.ok ? 'INSTRUCTIONS' : 'UNAVAILABLE'}
                  <span className="mono muted"> {instructions.name || instructions.id}</span>
                </h3>
                <button className="ghost-btn nx-nodrag" onClick={clearInstructions}>BACK TO LIST</button>
              </div>
              <pre className={`mono ${instructions.ok ? '' : 'dim'}`}>
                {instructions.ok ? instructions.instructions : 'This skill has no readable instructions (missing SKILL.md or invalid manifest).'}
              </pre>
            </div>
          )}

          {groups.map((g) => (
            <section key={g.category} className="skills-group">
              <h3 className="skills-group-title">
                {g.category}
                <span className="mono muted">{g.items.length}</span>
              </h3>
              {g.items.map((s) => (
                <SkillRow
                  key={s.id}
                  skill={s}
                  expanded={expanded === s.id}
                  showInline={!!(instructions && instructions.ok && instructions.id === s.id)}
                  instructionText={instructions && instructions.id === s.id ? instructions.instructions : null}
                  onToggle={() => setExpanded(expanded === s.id ? null : s.id)}
                  onView={() => (showInlineFor(instructions, s.id) ? clearInstructions() : requestInstructions(s.id))}
                />
              ))}
            </section>
          ))}
        </div>
      </div>
    </div>
  );
}

function showInlineFor(instructions, id) {
  return !!(instructions && instructions.ok && instructions.id === id);
}

function SkillRow({ skill, expanded, showInline, instructionText, onToggle, onView }) {
  const status = skill.status === 'available' ? 'ok' : skill.status;

  return (
    <div className={`skill-row ${expanded ? 'open' : ''}`}>
      <button className="skill-row-toggle" onClick={onToggle} aria-expanded={expanded}>
        <span className={`chev ${expanded ? 'rot' : ''}`}>›</span>
        <span className="skill-name">{skill.name}</span>
        <span className="mono skill-id">{skill.id}</span>
        <span className={`skill-status pill-${status}`}>{status.toUpperCase()}</span>
      </button>

      {expanded && (
        <div className="skill-meta">
          <div className="skill-line"><span className="val">{skill.description || '—'}</span></div>
          {skill.purpose ? <Row label="purpose" value={skill.purpose} /> : null}
          <TextList label="tags" items={skill.tags} />
          <TextList label="deps" items={skill.dependencies} />
          <TextList label="tools" items={skill.requiresTools} />

          <div className="skill-foot">
            <span className="mono muted">v{skill.version}</span>
            {!skill.hasInstructions && <span className="skill-warn mono">no SKILL.md</span>}
            <button className="ghost-btn nx-nodrag" onClick={onView}>
              {showInline ? 'HIDE INSTRUCTIONS' : 'VIEW INSTRUCTIONS'}
            </button>
          </div>

          {showInline && <pre className="skill-inline mono">{instructionText}</pre>}
        </div>
      )}
    </div>
  );
}

function Row({ label, value }) {
  if (!value) return null;
  return (
    <div className="skill-line">
      <span className="lbl">{label}</span>
      <span className="val">{value}</span>
    </div>
  );
}

function TextList({ label, items }) {
  if (!items || items.length === 0) return null;
  return (
    <div className="skill-tags">
      <span className="lbl">{label}</span>
      <span className="keys mono">{items.length ? items.join(', ') : '—'}</span>
    </div>
  );
}

function filterAndGroup(skills, query) {
  if (!skills) return [];
  const q = (query || '').trim().toLowerCase();
  const groups = new Map();
  for (const s of skills) {
    if (q && !matches(s, q)) continue;
    let g = groups.get(s.category);
    if (!g) groups.set(s.category, (g = []));
    g.push(s);
  }
  return [...groups.entries()]
    .map(([category, items]) => ({ category, items }))
    .sort((a, b) => a.category.localeCompare(b.category));
}

function matches(s, q) {
  return (
    s.id?.toLowerCase().includes(q) ||
    s.name?.toLowerCase().includes(q) ||
    s.description?.toLowerCase().includes(q) ||
    s.purpose?.toLowerCase().includes(q) ||
    s.category?.toLowerCase().includes(q) ||
    s.tags?.some((t) => t.toLowerCase().includes(q))
  );
}
