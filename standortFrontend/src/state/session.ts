import { useState, useCallback } from 'react';

export interface Session {
  groupId: string;
  memberId: string;
  memberToken: string;
  displayName: string;
}

const KEYS: (keyof Session)[] = ['groupId', 'memberId', 'memberToken', 'displayName'];

function loadSession(): Session | null {
  const groupId = localStorage.getItem('groupId');
  const memberId = localStorage.getItem('memberId');
  const memberToken = localStorage.getItem('memberToken');
  const displayName = localStorage.getItem('displayName');
  if (groupId && memberId && memberToken && displayName) {
    return { groupId, memberId, memberToken, displayName };
  }
  return null;
}

function saveSession(s: Session) {
  for (const k of KEYS) localStorage.setItem(k, s[k]);
}

function clearSession() {
  for (const k of KEYS) localStorage.removeItem(k);
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
