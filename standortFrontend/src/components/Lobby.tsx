import { useState } from 'react';
import { createGroup, joinGroup, ApiException } from '../api/client';
import { generateInviteCode, hashInviteCode } from '../crypto/groupCrypto';
import type { Session } from '../state/session';
import type { NameInUseError } from '../api/types';

interface Props {
  defaultInviteCode?: string;
  onJoined: (session: Session) => void;
}

interface NameConflict {
  inviteCode: string;
  displayName: string;
  lastSeen: string;
  hasLocation: boolean;
}

function formatRelative(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  if (!Number.isFinite(diffMs) || diffMs < 0) return 'gerade eben';
  const sec = Math.round(diffMs / 1000);
  if (sec < 60) return `vor ${sec} Sek.`;
  const min = Math.round(sec / 60);
  if (min < 60) return `vor ${min} Min.`;
  const hr = Math.round(min / 60);
  return `vor ${hr} Std.`;
}

export default function Lobby({ defaultInviteCode, onJoined }: Props) {
  const [groupName, setGroupName] = useState('');
  const [creatorName, setCreatorName] = useState('');
  const [inviteCode, setInviteCode] = useState(defaultInviteCode ?? '');
  const [joinName, setJoinName] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(false);
  const [conflict, setConflict] = useState<NameConflict | null>(null);

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

  async function performJoin(code: string, displayName: string, takeover: boolean) {
    const res = await joinGroup({ inviteCode: code, displayName, takeover });
    onJoined({
      groupId: res.groupId,
      memberId: res.memberId,
      memberToken: res.memberToken,
      displayName: res.displayName,
      historyDurationMinutes: res.historyDurationMinutes,
      inviteCode: code,
    });
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
      const name = joinName.trim();
      await performJoin(code, name, false);
    } catch (err) {
      if (err instanceof ApiException && err.status === 409 && err.body.error === 'name_in_use') {
        const c = err.body as unknown as NameInUseError;
        setConflict({
          inviteCode: inviteCode.trim().toUpperCase(),
          displayName: joinName.trim(),
          lastSeen: c.lastSeen,
          hasLocation: c.hasLocation,
        });
        setStatus('');
      } else {
        setStatus(err instanceof ApiException ? err.message : 'Fehler beim Beitreten.');
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleConfirmTakeover() {
    if (!conflict) return;
    setLoading(true);
    setStatus('Sitzung übernehmen...');
    try {
      await performJoin(conflict.inviteCode, conflict.displayName, true);
      setConflict(null);
    } catch (err) {
      setStatus(err instanceof ApiException ? err.message : 'Übernahme fehlgeschlagen.');
    } finally {
      setLoading(false);
    }
  }

  function handleCancelTakeover() {
    setConflict(null);
    setStatus('Bitte einen anderen Namen wählen.');
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

      {conflict && (
        <div className="modal-backdrop" role="dialog" aria-modal="true">
          <div className="modal-card">
            <h3>Name bereits aktiv</h3>
            <p>
              Der Name <strong>{conflict.displayName}</strong> ist in dieser Gruppe gerade aktiv
              (zuletzt {formatRelative(conflict.lastSeen)}{conflict.hasLocation ? ', mit geteiltem Standort' : ''}).
            </p>
            <p>Bist du das auf einem anderen Gerät?</p>
            <div className="modal-actions">
              <button type="button" onClick={handleConfirmTakeover} disabled={loading}>
                Ja, Sitzung übernehmen
              </button>
              <button type="button" className="secondary-btn" onClick={handleCancelTakeover} disabled={loading}>
                Anderen Namen wählen
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
