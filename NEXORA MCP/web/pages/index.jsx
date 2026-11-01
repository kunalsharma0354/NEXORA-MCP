import { useEffect, useState } from 'react';
import Head from 'next/head';
import TopBar from '../components/TopBar';
import StatusCard from '../components/StatusCard';
import StatsRow from '../components/StatsRow';
import ActivityFeed from '../components/ActivityFeed';
import SkillsModal from '../components/SkillsModal';
import { useNxData } from '../lib/bridge';

export default function Home() {
  const data = useNxData();
  const ready = data != null;

  // .nx-nodrag marks controls that must NOT drag the window; mousedown anywhere
  // else in the topbar starts the native drag via the C# side.
  useEffect(() => {
    const onDown = (e) => {
      if (e.button !== 0) return;
      if (e.target.closest('.nx-nodrag')) return;
      if (!e.target.closest('.topbar')) return;
      const bridge = typeof window !== 'undefined' ? window.chrome?.webview : null;
      try { bridge?.postMessage({ type: 'nx:cmd', cmd: 'drag' }); } catch (err) { /* no bridge */ }
    };
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, []);

  return (
    <div className="shell">
      <Head>
        <title>NEXORA — Dashboard</title>
      </Head>

      <TopBar data={data} />

      <main className="content">
        <StatusCard state={data?.state} detail={data?.detail} />
        <StatsRow stats={data?.stats} ready={ready} />
        <ActivityFeed activity={data?.activity} />
      </main>

      <SkillsModal />

      <footer className="statusbar">
        <span className="mono muted">
          NEXORA MCP v1.0{data?.session?.hwid ? ` · ${data.session.hwid}` : ''}
        </span>
        <span className="muted statusbar-right">
          {data?.session?.username ? <>signed in as <b>@{data.session.username}</b></> : '…'}
        </span>
      </footer>
    </div>
  );
}
