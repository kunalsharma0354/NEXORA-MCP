function fmtTime(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return String(iso);
  let h = d.getHours();
  const ampm = h >= 12 ? 'PM' : 'AM';
  h = h % 12 || 12;
  const mm = String(d.getMinutes()).padStart(2, '0');
  const ss = String(d.getSeconds()).padStart(2, '0');
  return `${h}:${mm}:${ss} ${ampm}`;
}

function badge(entry) {
  if (entry.status === 'pending') return { cls: 'pending', text: 'PROCESSING' };
  if (!entry.success) return { cls: 'error', text: 'ERROR' };
  return { cls: 'success', text: 'SUCCESS' };
}

export default function ActivityFeed({ activity }) {
  const list = Array.isArray(activity) ? activity : [];
  const ready = activity != null;

  return (
    <section className="feed-card card">
      <div className="feed-head">
        <span className="card-caption">RECENT&nbsp;ACTIVITY</span>
        {ready && list.length > 0 && <span className="count-chip">{list.length}&nbsp;CALLS</span>}
      </div>

      {!ready && (
        <div className="feed">
          {[0, 1, 2, 3].map((i) => (
            <div className="feed-item skeleton" key={i}>
              <div className="sk-line w40" /><div className="sk-line w70" /><div className="sk-line w25" />
            </div>
          ))}
        </div>
      )}

      {ready && list.length === 0 && (
        <div className="feed-empty">
          <div className="empty-icon">◎</div>
          <div className="empty-title">No tool calls yet</div>
          <div className="empty-sub">Activity appears here when OpenCode calls NEXORA.</div>
        </div>
      )}

      {ready && list.length > 0 && (
        <div className="feed">
          {list.map((e, i) => {
            const b = badge(e);
            return (
              <div className="feed-item" key={`${e.ts}-${i}`} style={{ animationDelay: `${i * 45}ms` }}>
                <span className={`left-bar ${b.cls}`} />
                <div className="feed-main">
                  <div className="feed-top">
                    <span className="tool-name">{e.tool || 'tool_call'}</span>
                    <span className="feed-time">{fmtTime(e.ts)}</span>
                  </div>
                  <div className="feed-input">{e.input || '—'}</div>
                  <div className="feed-bottom">
                    <span className={`badge ${b.cls}`}>{b.text}</span>
                    {e.model ? <span className="model-chip">{e.model}</span> : null}
                    {e.error ? <span className="err-note">{e.error}</span> : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
