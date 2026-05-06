import { useCallback, useState } from 'react';
import type { FormEvent } from 'react';
import { updateLocation, ApiException } from '../api/client';
import { useGeolocation } from '../hooks/useGeolocation';
import type { Session } from '../state/session';

interface Props {
  session: Session;
  inviteCode: string | null;
  onLeave: () => void;
  onStatus: (msg: string) => void;
}

export default function MemberControls({ session, inviteCode, onLeave, onStatus }: Props) {
  const [manualOpen, setManualOpen] = useState(false);
  const [manualLat, setManualLat] = useState('49.0069');
  const [manualLng, setManualLng] = useState('8.4037');
  const [manualAccuracy, setManualAccuracy] = useState('25');

  const sendLocation = useCallback(
    async (coords: { latitude: number; longitude: number; accuracy: number }) => {
      try {
        await updateLocation(session.groupId, session.memberId, session.memberToken, {
          lat: coords.latitude,
          lng: coords.longitude,
          accuracyMeters: coords.accuracy ?? 0,
          recordedAt: new Date().toISOString(),
        });
        onStatus('Standort geteilt.');
      } catch (err) {
        if (err instanceof ApiException && err.status === 401) {
          onStatus('Token abgelaufen — bitte neu beitreten.');
        } else {
          onStatus(err instanceof Error ? err.message : 'Standort konnte nicht gesendet werden.');
        }
      }
    },
    [session, onStatus],
  );

  const { autoShare, setAutoShare, getCurrent } = useGeolocation(sendLocation);

  const inviteLink = inviteCode
    ? `${location.origin}/?invite=${inviteCode}`
    : `${location.origin}/?invite=`;

  async function handleManualSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();

    const latitude = Number(manualLat);
    const longitude = Number(manualLng);
    const accuracy = manualAccuracy.trim() === '' ? 0 : Number(manualAccuracy);

    if (
      !Number.isFinite(latitude) ||
      latitude < -90 ||
      latitude > 90 ||
      !Number.isFinite(longitude) ||
      longitude < -180 ||
      longitude > 180 ||
      !Number.isFinite(accuracy) ||
      accuracy < 0
    ) {
      onStatus('Bitte gueltige Koordinaten eingeben.');
      return;
    }

    await sendLocation({ latitude, longitude, accuracy });
  }

  return (
    <div className="member-controls">
      <div className="member-name">{session.displayName}</div>
      {inviteCode && (
        <div className="invite-row">
          <span>Code: </span>
          <a href={inviteLink} className="invite-link">
            {inviteCode}
          </a>
        </div>
      )}
      <div className="control-row">
        <button onClick={getCurrent}>Standort teilen</button>
        <label className="auto-share-label">
          <input
            type="checkbox"
            checked={autoShare}
            onChange={(e) => setAutoShare(e.target.checked)}
          />
          Auto
        </label>
      </div>
      <button type="button" className="secondary-btn" onClick={() => setManualOpen((v) => !v)}>
        Manuell setzen
      </button>
      {manualOpen && (
        <form className="manual-location-form" onSubmit={handleManualSubmit}>
          <label>
            Latitude
            <input
              inputMode="decimal"
              value={manualLat}
              onChange={(e) => setManualLat(e.target.value)}
            />
          </label>
          <label>
            Longitude
            <input
              inputMode="decimal"
              value={manualLng}
              onChange={(e) => setManualLng(e.target.value)}
            />
          </label>
          <label>
            Genauigkeit m
            <input
              inputMode="decimal"
              value={manualAccuracy}
              onChange={(e) => setManualAccuracy(e.target.value)}
            />
          </label>
          <button type="submit">Standort aktualisieren</button>
        </form>
      )}
      <button className="leave-btn" onClick={onLeave}>
        Verlassen
      </button>
    </div>
  );
}
