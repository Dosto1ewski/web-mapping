import { useState, useEffect } from 'react';
import 'leaflet/dist/leaflet.css';
import { useSession } from './state/session';
import { usePollLocations } from './hooks/usePollLocations';
import Lobby from './components/Lobby';
import MapView from './components/MapView';
import MemberControls from './components/MemberControls';
import type { Session } from './state/session';

export default function App() {
  const { session, setSession, clearSession } = useSession();
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [status, setStatus] = useState('');
  const { members, error } = usePollLocations(session?.groupId ?? null);

  const defaultInviteCode = new URL(location.href).searchParams.get('invite') ?? undefined;

  useEffect(() => {
    if (error) setStatus(error);
  }, [error]);

  function handleJoined(s: Session, code?: string) {
    setSession(s);
    if (code) setInviteCode(code);
    setStatus(code ? `Gruppe erstellt. Einladungscode: ${code}` : 'Beigetreten.');
  }

  function handleLeave() {
    clearSession();
    setInviteCode(null);
    setStatus('Gruppe verlassen.');
  }

  return (
    <div className="app">
      <div className="panel">
        {!session ? (
          <Lobby defaultInviteCode={defaultInviteCode} onJoined={handleJoined} />
        ) : (
          <MemberControls
            session={session}
            inviteCode={inviteCode}
            onLeave={handleLeave}
            onStatus={setStatus}
          />
        )}
        {status && <p className="status">{status}</p>}
      </div>
      <MapView members={members} />
    </div>
  );
}
