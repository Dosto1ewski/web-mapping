import { useState, useCallback } from 'react';

export interface Session {
  groupId: string;
  memberId: string;
  memberToken: string;
  displayName: string;
  historyDurationMinutes: number;
  inviteCode: string;
}

const DEFAULT_HISTORY_DURATION_MIN = 15;

function loadSession(): Session | null {
  const groupId = localStorage.getItem('groupId');
  const memberId = localStorage.getItem('memberId');
  const memberToken = localStorage.getItem('memberToken');
  const displayName = localStorage.getItem('displayName');
  const inviteCode = localStorage.getItem('inviteCode');
  if (groupId && memberId && memberToken && displayName && inviteCode) {
    const storedRaw = localStorage.getItem('historyDurationMinutes');
    const stored = storedRaw !== null ? Number(storedRaw) : NaN;
    const historyDurationMinutes = Number.isFinite(stored) && stored >= 0
      ? stored
      : DEFAULT_HISTORY_DURATION_MIN;
    return { groupId, memberId, memberToken, displayName, historyDurationMinutes, inviteCode };
  }
  return null;
}

function saveSession(s: Session) {
  localStorage.setItem('groupId', s.groupId);
  localStorage.setItem('memberId', s.memberId);
  localStorage.setItem('memberToken', s.memberToken);
  localStorage.setItem('displayName', s.displayName);
  localStorage.setItem('historyDurationMinutes', String(s.historyDurationMinutes));
  localStorage.setItem('inviteCode', s.inviteCode);
}

function clearSession() {
  for (const k of ['groupId', 'memberId', 'memberToken', 'displayName', 'historyDurationMinutes', 'inviteCode']) {
    localStorage.removeItem(k);
  }
}

export function useSession() {
  const [session, setSessionState] = useState<Session | null>(loadSession);

  const setSession = useCallback((s: Session) => {
    saveSession(s);
    setSessionState(s);
  }, []);

  const clearSessionFn = useCallback(() => {
    clearSession();
    setSessionState(null);
  }, []);

  return { session, setSession, clearSession: clearSessionFn };
}
