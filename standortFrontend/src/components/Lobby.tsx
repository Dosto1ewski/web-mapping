import { useState } from 'react';
import { createGroup, joinGroup, ApiException } from '../api/client';
import { generateInviteCode, hashInviteCode } from '../crypto/groupCrypto';
import type { Session } from '../state/session';

interface Props {
  defaultInviteCode?: string;
  onJoined: (session: Session) => void;
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
      const code = generateInviteCode();
      const inviteCodeHash = await hashInviteCode(code);
      const res = await createGroup({
        name: groupName.trim(),
        createdByDisplayName: creatorName.trim(),
        inviteCodeHash,
      });
      onJoined({
        groupId: res.groupId,
        memberId: res.memberId,
        memberToken: res.memberToken,
        displayName: res.displayName,
        historyDurationMinutes: res.historyDurationMinutes,
        inviteCode: code,
      });
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
      const code = inviteCode.trim().toUpperCase();
      const res = await joinGroup({ inviteCode: code, displayName: joinName.trim() });
      onJoined({
        groupId: res.groupId,
        memberId: res.memberId,
        memberToken: res.memberToken,
        displayName: res.displayName,
        historyDurationMinutes: res.historyDurationMinutes,
        inviteCode: code,
      });
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
          placeholder="Einladungscode (XXXXXXXX-XXXXXXXX)"
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
