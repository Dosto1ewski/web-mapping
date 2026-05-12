import { useState } from 'react';
import type { FormEvent } from 'react';

export const MARKER_COLORS = [
  { hex: '#9ca3af', label: 'Grau' },
  { hex: '#3b82f6', label: 'Blau' },
  { hex: '#ef4444', label: 'Rot' },
  { hex: '#f97316', label: 'Orange' },
  { hex: '#22c55e', label: 'Grün' },
  { hex: '#a855f7', label: 'Lila' },
  { hex: '#06b6d4', label: 'Cyan' },
];

interface Props {
  lat: number;
  lng: number;
  onConfirm: (name: string, color: string, notes: string | null) => Promise<void>;
  onCancel: () => void;
}

export default function MarkerDialog({ lat, lng, onConfirm, onCancel }: Props) {
  const [name, setName] = useState('');
  const [color, setColor] = useState('#3b82f6');
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!name.trim()) {
      setError('Name ist erforderlich.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      await onConfirm(name.trim(), color, notes.trim() || null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Fehler beim Erstellen.');
      setLoading(false);
    }
  }

  return (
    <div
      className="marker-dialog-overlay"
      onMouseDown={(e) => e.target === e.currentTarget && onCancel()}
    >
      <div className="marker-dialog">
        <h3>Marker setzen</h3>
        <p className="marker-coords">
          {lat.toFixed(5)}, {lng.toFixed(5)}
        </p>
        <form onSubmit={handleSubmit}>
          <label>
            Name *
            <input
              autoFocus
              value={name}
              maxLength={100}
              onChange={(e) => setName(e.target.value)}
              placeholder="z.B. Treffpunkt"
            />
          </label>
          <div className="color-picker">
            <span>Farbe</span>
            <div className="color-swatches">
              {MARKER_COLORS.map((c) => (
                <button
                  key={c.hex}
                  type="button"
                  className={`color-swatch${color === c.hex ? ' selected' : ''}`}
                  style={{ background: c.hex }}
                  title={c.label}
                  onClick={() => setColor(c.hex)}
                />
              ))}
            </div>
          </div>
          <label>
            Notiz (optional)
            <textarea
              value={notes}
              maxLength={500}
              rows={2}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Beschreibung..."
            />
          </label>
          {error && <p className="dialog-error">{error}</p>}
          <div className="dialog-actions">
            <button
              type="button"
              className="secondary-btn dialog-cancel-btn"
              onClick={onCancel}
              disabled={loading}
            >
              Abbrechen
            </button>
            <button type="submit" disabled={loading}>
              {loading ? '...' : 'Erstellen'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
