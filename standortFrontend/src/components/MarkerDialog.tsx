import { useState } from 'react';
import type { FormEvent } from 'react';
import { MARKER_COLORS, MARKER_ICONS } from './markerConstants';

interface Props {
  readonly lat: number;
  readonly lng: number;
  readonly onConfirm: (name: string, color: string | null, notes: string | null, icon?: typeof MARKER_ICONS[number]['id']) => Promise<void>;
  readonly onCancel: () => void;
}

function iconGlyph(id: typeof MARKER_ICONS[number]['id']) {
  if (id === 'tree') return '<img src="/tree.png" alt="Baum" style="width:18px;height:18px;vertical-align:middle;"/>';
  if (id === 'book') return '<img src="/book.png" alt="Buch" style="width:18px;height:18px;vertical-align:middle;"/>';
  if (id === 'champagne') return '<img src="/champagne.png" alt="Sekt" style="width:18px;height:18px;vertical-align:middle;"/>';
  return '📍';
}

export default function MarkerDialog({ lat, lng, onConfirm, onCancel }: Props) {
  const [name, setName] = useState('');
  const [color, setColor] = useState<string | null>('#3b82f6');
  const [icon, setIcon] = useState<typeof MARKER_ICONS[number]['id']>('default');
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
      const sendColor = icon === 'default' ? color : null;
      await onConfirm(name.trim(), sendColor, notes.trim() || null, icon ?? 'default');
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
            <span>Icon</span>
            <ul className="color-swatches" style={{display:'flex',gap:6,listStyle:'none',padding:0,margin:0}}>
              {MARKER_ICONS.map((ic) => {
                const glyph = iconGlyph(ic.id);
                return (
                  <li key={ic.id}>
                    <button
                      type="button"
                      className={`color-swatch${icon === ic.id ? ' selected' : ''}`}
                      title={ic.label}
                      onClick={() => {
                        setIcon(ic.id);
                        if (ic.id === 'default') {
                          if (color === null) setColor('#3b82f6');
                        } else {
                          setColor(null);
                        }
                      }}
                      disabled={loading}
                    >
                      <span aria-hidden style={{fontSize:16,lineHeight:1}} dangerouslySetInnerHTML={{ __html: glyph }} />
                    </button>
                  </li>
                );
              })}
            </ul>
          </div>

          {icon === 'default' && (
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
                    disabled={loading}
                  />
                ))}
              </div>
            </div>
          )}

          <label>
            <span>Notiz (optional)</span>
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
