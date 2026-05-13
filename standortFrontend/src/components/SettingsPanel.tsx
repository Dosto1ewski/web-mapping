import { useRef, useState, useEffect } from 'react';
import type { Settings } from '../state/settings';

interface Props {
  settings: Settings;
  onUpdate: (patch: Partial<Settings>) => void;
}

function clamp(val: number, min: number, max: number) {
  return Math.max(min, Math.min(max, val));
}

export default function SettingsPanel({ settings, onUpdate }: Props) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    function handleOutsideClick(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handleOutsideClick);
    return () => document.removeEventListener('mousedown', handleOutsideClick);
  }, [open]);

  return (
    <div ref={ref} className="settings-panel">
      <button
        className="settings-btn"
        onClick={() => setOpen((v) => !v)}
        title="Einstellungen"
        aria-label="Einstellungen öffnen"
      >
        <GearIcon />
      </button>
      {open && (
        <div className="settings-dropdown">
          <h4>Einstellungen</h4>
          <label className="settings-row">
            Standorte abrufen (s)
            <input
              type="number"
              min={1}
              max={300}
              value={settings.locationFetchSec}
              onChange={(e) =>
                onUpdate({ locationFetchSec: clamp(Number(e.target.value), 1, 300) })
              }
            />
          </label>
          <label className="settings-row">
            Marker abrufen (s)
            <input
              type="number"
              min={5}
              max={300}
              value={settings.markerFetchSec}
              onChange={(e) =>
                onUpdate({ markerFetchSec: clamp(Number(e.target.value), 5, 300) })
              }
            />
          </label>
          <label className="settings-row">
            Standort senden (s)
            <input
              type="number"
              min={1}
              max={300}
              value={settings.locationUpdateSec}
              onChange={(e) =>
                onUpdate({ locationUpdateSec: clamp(Number(e.target.value), 1, 300) })
              }
            />
          </label>
          <label className="settings-row">
            Namensschilder anzeigen
            <input
              type="checkbox"
              checked={settings.showNametags}
              onChange={(e) => onUpdate({ showNametags: e.target.checked })}
            />
          </label>
        </div>
      )}
    </div>
  );
}

function GearIcon() {
  return (
    <svg
      width="18"
      height="18"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.68 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.68a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
    </svg>
  );
}
