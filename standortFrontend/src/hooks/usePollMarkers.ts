import { useState, useEffect, useCallback } from 'react';
import { getMarkers } from '../api/client';
import type { MarkerDto } from '../api/types';

export function usePollMarkers(groupId: string | null, intervalMs = 15000) {
  const [markers, setMarkers] = useState<MarkerDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const poll = useCallback(async () => {
    if (!groupId) return;
    try {
      const data = await getMarkers(groupId);
      setMarkers(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Marker-Polling fehlgeschlagen');
    }
  }, [groupId]);

  useEffect(() => {
    if (!groupId) {
      setMarkers([]);
      return;
    }
    poll();
    const id = setInterval(poll, intervalMs);
    return () => clearInterval(id);
  }, [groupId, poll, intervalMs]);

  return { markers, error, refresh: poll };
}
