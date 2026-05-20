import { useState, useEffect, useCallback } from 'react';
import { getMarkers } from '../api/client';
import { decrypt } from '../crypto/groupCrypto';
import type { MarkerDto, WireMarkerDto } from '../api/types';

async function decodeMarker(m: WireMarkerDto, key: CryptoKey): Promise<MarkerDto> {
  const plain = await decrypt(key, m.encryptedLocation);
  const { lat, lng } = JSON.parse(plain) as { lat: number; lng: number };
  const notes = m.encryptedNotes ? await decrypt(key, m.encryptedNotes) : null;
  return {
    markerId: m.markerId,
    name: m.name,
    lat,
    lng,
    color: m.color,
    notes,
    createdByMemberId: m.createdByMemberId,
    createdAt: m.createdAt,
    icon: m.icon,
  };
}

export function usePollMarkers(groupId: string | null, intervalMs = 15000, cryptoKey: CryptoKey | null) {
  const [markers, setMarkers] = useState<MarkerDto[]>([]);
  const [error, setError] = useState<string | null>(null);

  const poll = useCallback(async () => {
    if (!groupId || !cryptoKey) return;
    try {
      const data = await getMarkers(groupId);
      const decoded = await Promise.all(data.map((m) => decodeMarker(m, cryptoKey)));
      setMarkers(decoded);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Marker-Polling fehlgeschlagen');
    }
  }, [groupId, cryptoKey]);

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
