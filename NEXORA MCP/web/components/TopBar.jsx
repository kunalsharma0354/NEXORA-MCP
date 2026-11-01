import { useEffect, useState } from 'react';
import ModelSelector from './ModelSelector';
import { postCommand } from '../lib/bridge';

// NO-DRAG zones are marked with .nx-nodrag; everything else in the bar
// starts the native window drag via the C# bridge.
export default function TopBar({ data }) {
  const state = data?.state ?? 'connecting';  return (
    <header className="topbar">
      <div className="topbar-inner">
        <div className="brand">
          <span className={`brand-dot ${state}`} />
          <span className="brand-name">NEXORA</span>
          <span className="brand-sub">MCP&nbsp;DASHBOARD</span>
        </div>

        <div className="topbar-right">
          <div className={`status-pill ${state}`}>
            <span className="pulse-dot" />
            <span className="pulse-ring" />
            <span className="status-text">{state.toUpperCase()}</span>
          </div>

          <ModelSelector data={data} />

          <button className="ghost-btn nx-nodrag" onClick={() => postCommand('browser.toggle')}>
            BROWSER
          </button>
          <button className="ghost-btn nx-nodrag" onClick={() => postCommand('skills.list')}>
            SKILLS
          </button>
          <button className="ghost-btn nx-nodrag" onClick={() => postCommand('logout')}>
            LOGOUT
          </button>

          <div className="win-divider nx-nodrag" />

          <button className="win-btn nx-nodrag" title="Minimize" onClick={() => postCommand('minimize')}>
            <svg width="10" height="10" viewBox="0 0 10 10"><path d="M1 5h8" stroke="currentColor" strokeWidth="1.4" /></svg>
          </button>
          <button className="win-btn win-close nx-nodrag" title="Close" onClick={() => postCommand('close')}>
            <svg width="10" height="10" viewBox="0 0 10 10"><path d="M1 1l8 8M9 1l-8 8" stroke="currentColor" strokeWidth="1.4" /></svg>
          </button>
        </div>
      </div>
    </header>
  );
}
