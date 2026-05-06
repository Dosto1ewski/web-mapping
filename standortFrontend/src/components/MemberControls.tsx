import { useCallback } from 'react';
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
      <button className="leave-btn" onClick={onLeave}>
        Verlassen
      </button>
    </div>
  );
}
