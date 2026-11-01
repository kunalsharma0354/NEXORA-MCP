// Server status card — animated radial glow when the server is
// active/processing (mirrors the old OnStatusCardPaint glow).
export default function StatusCard({ state, detail }) {
  const s = state ?? 'connecting';
  const stateText = {
    active: 'ACTIVE',
    processing: 'PROCESSING',
    inactive: 'INACTIVE',
    connecting: 'CHECKING'
  }[s] ?? 'CHECKING';

  return (
    <section className={`status-card card glow-${s}`}>
      <div className="glow-blob" />
      <div className="status-inner">
        <div className="card-caption">M&nbsp;C&nbsp;P&nbsp;&nbsp;S&nbsp;E&nbsp;R&nbsp;V&nbsp;E&nbsp;R</div>
        <div className="status-state">{stateText}</div>
        <div className="status-detail">{detail ?? 'Connecting to MCP activity feed…'}</div>
      </div>
      <div className="accent-line" />
    </section>
  );
}
