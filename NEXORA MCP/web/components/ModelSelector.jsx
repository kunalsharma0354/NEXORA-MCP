import { useEffect, useRef, useState } from 'react';
import { postCommand } from '../lib/bridge';

export default function ModelSelector({ data }) {
  const models = data?.models ?? [];
  const selected = models.find((m) => m.key === data?.selectedModelKey) ?? models[0] ?? null;
  const [open, setOpen] = useState(false);
  const boxRef = useRef(null);

  // Close when clicking anywhere outside the selector box.
  useEffect(() => {
    if (!open) return;
    const onDoc = (e) => {
      if (boxRef.current && !boxRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', onDoc);
    return () => document.removeEventListener('mousedown', onDoc);
  }, [open]);

  const pick = (key) => {
    setOpen(false);
    postCommand('model.select', { key });
  };

  return (
    <div className="model-selector nx-nodrag" ref={boxRef}>
      <button
        className={`selector-trigger ${open ? 'open' : ''}`}
        onClick={() => (models.length > 0 ? setOpen(!open) : null)}
        title={selected ? `${selected.name}\n${selected.key}` : 'Select model'}
      >
        <span className="selector-label">{selected ? selected.name : 'Select model…'}</span>
        <svg className={`chevron ${open ? 'up' : ''}`} width="10" height="10" viewBox="0 0 10 10">
          <path d="M1.5 3.5L5 7l3.5-3.5" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
      </button>

      {open && (
        <div className="selector-drop">
          {models.map((m) => (
            <button
              key={m.key}
              className={`selector-item ${m.key === selected?.key ? 'selected' : ''}`}
              onClick={() => pick(m.key)}
            >
              <div className="item-inner">
                <div className="item-name">{m.name}</div>
                <div className="item-key">{m.key}</div>
              </div>
            </button>
          ))}
          {models.length === 0 && <div className="selector-empty">No models available</div>}
        </div>
      )}
    </div>
  );
}
