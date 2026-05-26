import { useCallback, useState } from 'react';
import {
  updateLocation,
  updateMemberSettings,
  deleteMemberHistory,
  ApiException,
} from '../api/client';
import { useGeolocation } from '../hooks/useGeolocation';
import type { Session } from '../state/session';

const MAX_HISTORY_DURATION_MIN = 2880;

interface Props {
  session: Session;
  cryptoKey: CryptoKey | null;
  onLeave: () => void;
  onSessionInvalidated: (staleToken?: string) => void;
  onStatus: (msg: string) => void;
  placingMarker: boolean;
  onTogglePlacingMarker: () => void;
  locationUpdateIntervalMs: number;
  placingLocation: boolean;
  onTogglePlacingLocation: () => void;
  locationDragPos: { lat: number; lng: number };
  onOwnLocation: (loc: { lat: number; lng: number }) => void;
  onHistoryDurationChange: (minutes: number) => void;
  collapsed?: boolean;
}

export default function MemberControls({
  session,
  cryptoKey,
  onLeave,
  onSessionInvalidated,
  onStatus,
  placingMarker,
  onTogglePlacingMarker,
  locationUpdateIntervalMs,
  placingLocation,
  onTogglePlacingLocation,
  locationDragPos,
  onOwnLocation,
  onHistoryDurationChange,
  collapsed = false,
}: Readonly<Props>) {
  const [historyInput, setHistoryInput] = useState(String(session.historyDurationMinutes));

  const sendLocation = useCallback(
    async (coords: { latitude: number; longitude: number; accuracy: number }) => {
      if (!cryptoKey) return;
      onOwnLocation({ lat: coords.latitude, lng: coords.longitude });
      try {
        await updateLocation(
          session.groupId,
          session.memberId,
          session.memberToken,
          cryptoKey,
          { lat: coords.latitude, lng: coords.longitude, accuracyMeters: coords.accuracy ?? 0 },
          new Date().toISOString(),
        );
        onStatus('Standort geteilt.');
      } catch (err) {
        if (err instanceof ApiException && err.status === 401) {
          onSessionInvalidated();
        } else {
          onStatus(err instanceof Error ? err.message : 'Standort konnte nicht gesendet werden.');
        }
      }
    },
    [session, cryptoKey, onStatus, onSessionInvalidated, onOwnLocation],
  );

  const { autoShare, setAutoShare, getCurrent } = useGeolocation(
    sendLocation,
    locationUpdateIntervalMs,
  );

  async function handleHistoryCommit() {
    const parsed = Number(historyInput);
    const clamped = Number.isFinite(parsed)
      ? Math.max(0, Math.min(MAX_HISTORY_DURATION_MIN, Math.round(parsed)))
      : session.historyDurationMinutes;
    setHistoryInput(String(clamped));
    if (clamped === session.historyDurationMinutes) return;
    try {
      await updateMemberSettings(session.groupId, session.memberId, session.memberToken, {
        historyDurationMinutes: clamped,
      });
      onHistoryDurationChange(clamped);
      onStatus(`Verlaufsdauer: ${clamped} Min.`);
    } catch (err) {
      if (err instanceof ApiException && err.status === 401) {
        onSessionInvalidated(session.memberToken);
      } else {
        onStatus(err instanceof Error ? err.message : 'Verlaufsdauer konnte nicht gespeichert werden.');
      }
    }
  }

  async function handleDeleteHistory() {
    try {
      await deleteMemberHistory(session.groupId, session.memberId, session.memberToken);
      setAutoShare(false);
      onStatus('Verlauf gelöscht, Standortfreigabe deaktiviert.');
    } catch (err) {
      if (err instanceof ApiException && err.status === 401) {
        onSessionInvalidated(session.memberToken);
      } else {
        onStatus(err instanceof Error ? err.message : 'Verlauf konnte nicht gelöscht werden.');
      }
    }
  }

  const inviteCode = session.inviteCode;
  const inviteLink = inviteCode
    ? `${globalThis.location.origin}${globalThis.location.pathname}?invite=${encodeURIComponent(inviteCode)}`
    : '';

  async function handleCopyInviteLink() {
    if (!inviteLink) return;

    try {
      await navigator.clipboard.writeText(inviteLink);
      onStatus('Einladungslink kopiert.');
    } catch {
      onStatus(inviteLink);
    }
  }

  if (collapsed) {
    return (
      <div className="member-controls-icons">
        <div className="member-initial-badge">{session.displayName[0].toUpperCase()}</div>
        <button
          type="button"
          className="icon-action-btn"
          onClick={getCurrent}
          title="Standort teilen"
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2L4 9h5v7h6V9h5L12 2z" />
          </svg>
        </button>
        <button
          type="button"
          className={`icon-action-btn${placingMarker ? ' placing-active' : ''}`}
          onClick={onTogglePlacingMarker}
          title={placingMarker ? 'Marker abbrechen' : 'Marker setzen'}
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z" />
          </svg>
        </button>
        <button
          type="button"
          className="icon-action-btn leave-icon-btn"
          onClick={onLeave}
          title="Verlassen"
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
            <polyline points="16 17 21 12 16 7" />
            <line x1="21" y1="12" x2="9" y2="12" />
          </svg>
        </button>
      </div>
    );
  }

  async function handleConfirmManualLocation() {
    await sendLocation({ latitude: locationDragPos.lat, longitude: locationDragPos.lng, accuracy: 0 });
    onTogglePlacingLocation();
  }

  return (
    <div className="member-controls">
      <div className="member-name">{session.displayName}</div>
      {inviteCode && (
        <div className="invite-row">
          <span>
            Code: <strong>{inviteCode}</strong>
          </span>
          <div className="invite-actions">
            <a href={inviteLink} className="invite-link">
              Einladungslink
            </a>
            <button type="button" className="invite-copy-btn" onClick={handleCopyInviteLink}>
              Kopieren
            </button>
          </div>
        </div>
      )}
      <div className="control-row">
        <button onClick={getCurrent}>Standort teilen</button>
        <label className="auto-share-label">
          <input
            id="autoShare"
            type="checkbox"
            checked={autoShare}
            onChange={(e) => setAutoShare(e.target.checked)}
          />
          <span>Auto</span>
        </label>
      </div>
      <label className="control-row history-duration-row">
        <span>Verlaufsdauer (Min.)</span>
        <input
          id="historyDuration"
          type="number"
          min={0}
          max={MAX_HISTORY_DURATION_MIN}
          value={historyInput}
          onChange={(e) => setHistoryInput(e.target.value)}
          onBlur={handleHistoryCommit}
          title="0 = kein Verlauf, max. 2880 (48 h)"
        />
      </label>
      <button type="button" className="secondary-btn" onClick={handleDeleteHistory}>
        Verlauf löschen
      </button>
      <button
        type="button"
        className={`secondary-btn${placingMarker ? ' placing-active' : ''}`}
        onClick={onTogglePlacingMarker}
      >
        {placingMarker ? 'Abbrechen' : '📍 Marker setzen'}
      </button>
      <button
        type="button"
        className={`secondary-btn${placingLocation ? ' placing-active' : ''}`}
        onClick={onTogglePlacingLocation}
      >
        {placingLocation ? 'Abbrechen' : 'Manuell setzen'}
      </button>
      {placingLocation && (
        <button type="button" className="secondary-btn" onClick={handleConfirmManualLocation}>
          Standort bestätigen
        </button>
      )}
      <button className="leave-btn" onClick={onLeave}>
        Verlassen
      </button>
    </div>
  );
}
