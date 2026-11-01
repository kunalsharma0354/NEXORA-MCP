import { useEffect, useRef, useState } from 'react';

// Smooth count-up (rAF, ease-out cubic) — matches the old StatCard 800ms.
function useCountUp(target, duration = 800) {
  const [value, setValue] = useState(0);
  const currentRef = useRef(0);

  useEffect(() => {
    const from = currentRef.current;
    const to = Number(target) || 0;
    if (from === to) {
      setValue(to);
      return undefined;
    }
    const t0 = performance.now();
    let raf = 0;
    const step = (t) => {
      const p = Math.min(1, (t - t0) / duration);
      const eased = 1 - Math.pow(1 - p, 3);
      const v = Math.round(from + (to - from) * eased);
      setValue(v);
      if (p < 1) raf = requestAnimationFrame(step);
    };
    raf = requestAnimationFrame(step);
    return () => cancelAnimationFrame(raf);
  }, [target, duration]);

  useEffect(() => {
    currentRef.current = value;
  }, [value]);

  return value;
}

export default function StatsRow({ stats, ready }) {
  return (
    <section className="stats-row">
      <div className="stat-card card">
        <div className="stat-title">TOTAL&nbsp;TOOL&nbsp;CALLS</div>
        <div className="stat-value">
          {ready ? <CountUp value={stats?.totalCalls ?? 0} /> : <span className="skeleton-num" />}
        </div>
        <div className="stat-foot"><span className="stat-note">all-time recorded calls</span></div>
        <div className="stat-bar"><div className="stat-bar-fill" style={{ width: Math.min(100, (stats?.totalCalls ?? 0) * 2) + '%', background: '#F2F2F2' }} /></div>
      </div>

      <div className="stat-card card">
        <div className="stat-title">SUCCESS&nbsp;RATE</div>
        <div className="stat-row">
          <div className="stat-value success">{ready ? <CountUp value={stats?.successRate ?? 0} /> : <span className="skeleton-num" />}<span className="stat-suffix">%</span></div>
          <Ring value={ready ? (stats?.successRate ?? 0) : 0} ready={ready} />
        </div>
        <div className="stat-foot"><span className="stat-note">of completed tool calls</span></div>
      </div>

      <div className="stat-card card">
        <div className="stat-title">AVG&nbsp;LATENCY</div>
        <div className="stat-value warning">
          {ready ? <CountUp value={stats?.latencyMs ?? 0} /> : <span className="skeleton-num" />}
          <span className="stat-suffix">ms</span>
        </div>
        <div className="stat-foot"><span className="stat-note">estimated · last 5 calls</span></div>
        <div className="stat-bar"><div className="stat-bar-fill" style={{ width: Math.min(100, (stats?.latencyMs ?? 0) / 20) + '%', background: 'var(--warning)' }} /></div>
      </div>
    </section>
  );
}

function CountUp({ value }) {
  const n = useCountUp(value);
  return <>{n}</>;
}

// Conic-gradient progress ring (replaces the old drawn ring).
function Ring({ value, ready }) {
  const deg = Math.round((Math.min(100, Math.max(0, value)) / 100) * 360);
  return (
    <div
      className="ring"
      style={ready ? { background: `conic-gradient(var(--success) ${deg}deg, #232323 0deg)` } : { background: '#232323' }}
    >
      <div className="ring-hole">
        <span>{ready ? `${Math.round(value)}` : '–'}</span>
      </div>
    </div>
  );
}
