import { useEffect, useState } from 'react';

// ─── C# ⇄ JS bridge contract ────────────────────────────────────────────
// C# → JS (PostWebMessageAsJson):  { type:'nx:data', payload:{...} }
// JS → C# (postMessage):           { type:'nx:cmd', cmd:'...', ...extra }
//
// payload shape:
// {
//   state: 'active'|'processing'|'inactive',
//   detail: string,
//   stats: { totalCalls, successRate, latencyMs },
//   models: [{ key, name, maxTokens }],
//   selectedModelKey: string,
//   activity: [{ ts, tool, status, success, input, output, model, error }],
//   session: { username, hwid },
//   browser: { running, url }
// }

export function hasBridge() {
  return typeof window !== 'undefined' && typeof window.chrome !== 'undefined' && !!window.chrome.webview;
}

export function postCommand(cmd, extra = {}) {
  try {
    if (hasBridge()) window.chrome.webview.postMessage({ type: 'nx:cmd', cmd, ...extra });
  } catch (err) {
    // No bridge — nothing to command. Ignore (browser-dev mode).
  }
}

// Demo data so the page also looks right when opened in a normal browser.
const DEMO = {
  state: 'active',
  detail: 'Demo sample — WebView bridge not connected. Last call: 10:42:05 PM · Server is idle',
  stats: { totalCalls: 47, successRate: 94, latencyMs: 612 },
  models: [
    { key: 'nvidia/nemotron-3-ultra-550b-a55b', name: 'Nemotron-3 Ultra 550B', maxTokens: 16384 },
    { key: 'nvidia/llama-3.1-nemotron-70b', name: 'Llama 3.1 Nemotron 70B', maxTokens: 16384 }
  ],
  selectedModelKey: 'nvidia/nemotron-3-ultra-550b-a55b',
  activity: [
    { ts: new Date(Date.now() - 2 * 60000).toISOString(), tool: 'nexora_browser_navigate', status: 'done', success: true, input: 'https://github.com/kunalsharma', output: 'ok', model: 'nemotron-3-ultra' },
    { ts: new Date(Date.now() - 5 * 60000).toISOString(), tool: 'nexora_enhance_prompt', status: 'pending', success: false, input: 'bhai dashboard ka ui next.js me banao...', output: '', model: 'nemotron-3-ultra' },
    { ts: new Date(Date.now() - 9 * 60000).toISOString(), tool: 'nexora_browser_get_content', status: 'done', success: true, input: 'format=text, selector=body', output: '8241 chars', model: '' },
    { ts: new Date(Date.now() - 14 * 60000).toISOString(), tool: 'nexora_browser_click_at', status: 'done', success: false, error: 'Stale coordinates — re-screenshot required', input: '(612,344) left x1', output: '', model: '' },
    { ts: new Date(Date.now() - 21 * 60000).toISOString(), tool: 'nexora_browser_fill', status: 'done', success: true, input: 'input[name=q] = hello world', output: 'filled', model: '' }
  ],
  session: { username: 'demo', hwid: 'A1B2C3D4' },
  browser: { running: false, url: 'about:blank' }
};

export function useNxData() {
  const [data, setData] = useState(null);

  useEffect(() => {
    if (!hasBridge()) {
      const t = setTimeout(() => setData(DEMO), 250);
      return () => clearTimeout(t);
    }
    const bridge = window.chrome.webview;
    const onMessage = (e) => {
      try {
        const msg = e.data;
        if (msg && msg.type === 'nx:data' && msg.payload) setData(msg.payload);
      } catch (err) {
        // Malformed message — ignore, next tick will refresh anyway.
      }
    };
    bridge.addEventListener('message', onMessage);
    postCommand('ui.ready');
    return () => bridge.removeEventListener('message', onMessage);
  }, []);

  return data;
}

// ─── Skills overlay (on-demand from the SKILLS topbar button) ───────────
// { type:'nx:skills',             payload:{ count, issueCount, skills:[...] } }
// { type:'nx:skill-instructions', payload:{ id, ok, name, instructions } }
export function useNxSkills() {
  const [state, setState] = useState({ open: false, loading: false, list: null, instructions: null });

  useEffect(() => {
    if (!hasBridge()) return undefined;
    const bridge = window.chrome.webview;
    const onMessage = (e) => {
      try {
        const msg = e.data;
        if (!msg || !msg.payload) return;
        if (msg.type === 'nx:skills') {
          setState({
            open: true,
            loading: false,
            list: msg.payload,
            instructions: null
          });
        } else if (msg.type === 'nx:skill-instructions') {
          setState((prev) => ({ ...prev, instructions: msg.payload }));
        }
      } catch (err) {
        // Malformed message — ignore.
      }
    };
    bridge.addEventListener('message', onMessage);
    return () => bridge.removeEventListener('message', onMessage);
  }, []);

  const requestSkills = () => {
    setState((prev) => ({ ...prev, open: true, loading: true, instructions: null }));
    postCommand('skills.list');
  };

  const close = () => setState({ open: false, loading: false, list: null, instructions: null });

  const requestInstructions = (id) => postCommand('skills.load', { id });

  const clearInstructions = () => setState((prev) => ({ ...prev, instructions: null }));

  return { ...state, requestSkills, close, requestInstructions, clearInstructions };
}
