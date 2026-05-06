import { useState } from 'react';
import { createGroup, joinGroup, ApiException } from '../api/client';
import type { Session } from '../state/session';

interface Props {
  defaultInviteCode?: string;
  onJoined: (session: Session, inviteCode?: string) => void;
}

export default function Lobby({ defaultInviteCode, onJoined }: Props) {
  const [groupName, setGroupName] = useState('');
  const [creatorName, setCreatorName] = useState('');
  const [inviteCode, setInviteCode] = useState(defaultInviteCode ?? '');
  const [joinName, setJoinName] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(false);

  async function handleCreate() {
    if (!groupName.trim() || !creatorName.trim()) {
      setStatus('Gruppenname und Dein Name sind erforderlich.');
      return;
    }
    setLoading(true);
    setStatus('Gruppe wird erstellt...');
    try {
      const res = await createGroup({ name: groupName.trim(), createdByDisplayName: creatorName.trim() });
      onJoined(
        { groupId: res.groupId, memberId: res.memberId, memberToken: res.memberToken, displayName: res.displayName },
        res.inviteCode,
      );
    } catch (err) {
      setStatus(err instanceof ApiException ? err.message : 'Fehler beim Erstellen.');
    } finally {
      setLoading(false);
    }
  }

  async function handleJoin() {
    if (!inviteCode.trim() || !joinName.trim()) {
      setStatus('Einladungscode und Dein Name sind erforderlich.');
      return;
    }
    setLoading(true);
    setStatus('Beitreten...');
    try {
      const res = await joinGroup({ inviteCode: inviteCode.trim().toUpperCase(), displayName: joinName.trim() });
      onJoined({ groupId: res.groupId, memberId: res.memberId, memberToken: res.memberToken, displayName: res.displayName });
    } catch (err) {
      setStatus(err instanceof ApiException ? err.message : 'Fehler beim Beitreten.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="lobby">
      <h2>Standort</h2>

      <section className="lobby-section">
        <h3>Gruppe erstellen</h3>
        <input
          placeholder="Gruppenname"
          value={groupName}
          onChange={(e) => setGroupName(e.target.value)}
          disabled={loading}
        />
        <input
          placeholder="Dein Name"
          value={creatorName}
          onChange={(e) => setCreatorName(e.target.value)}
          disabled={loading}
        />
        <button onClick={handleCreate} disabled={loading}>
          Erstellen
        </button>
      </section>

      <section className="lobby-section">
        <h3>Gruppe beitreten</h3>
        <input
          placeholder="Einladungscode (XXXX-XXXX)"
          value={inviteCode}
          onChange={(e) => setInviteCode(e.target.value)}
          disabled={loading}
        />
        <input
          placeholder="Dein Name"
          value={joinName}
          onChange={(e) => setJoinName(e.target.value)}
          disabled={loading}
        />
        <button onClick={handleJoin} disabled={loading}>
          Beitreten
        </button>
      </section>

      {status && <p className="status">{status}</p>}
    </div>
  );
}
