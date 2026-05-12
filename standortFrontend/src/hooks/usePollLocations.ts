import { useState, useEffect, useRef, useCallback } from 'react';
import { getLocations } from '../api/client';
import type { MemberLocationDto } from '../api/types';

export function usePollLocations(groupId: string | null, intervalMs = 5000) {
  const [members, setMembers] = useState<MemberLocationDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const versionRef = useRef<number | undefined>(undefined);

  const poll = useCallback(async () => {
    if (!groupId) return;
    try {
      const data = await getLocations(groupId, versionRef.current);
      if (data) {
        versionRef.current = data.version;
        setMembers(data.members);
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Polling failed');
    }
  }, [groupId]);

  useEffect(() => {
    if (!groupId) {
      setMembers([]);
      versionRef.current = undefined;
      return;
    }
    poll();
    const id = setInterval(poll, intervalMs);
    return () => clearInterval(id);
  }, [groupId, poll, intervalMs]);

  return { members, error };
}
