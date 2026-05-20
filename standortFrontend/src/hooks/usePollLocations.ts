import { useState, useEffect, useRef, useCallback } from 'react';
import { getLocations } from '../api/client';
import { decrypt } from '../crypto/groupCrypto';
import type { MemberLocationDto, GeoPointDto, WireGeoPointDto } from '../api/types';

async function decodeGeoPoint(pt: WireGeoPointDto, key: CryptoKey): Promise<GeoPointDto> {
  const plain = await decrypt(key, pt.encryptedLocation);
  const { lat, lng, accuracyMeters } = JSON.parse(plain) as { lat: number; lng: number; accuracyMeters: number };
  return { lat, lng, accuracyMeters, recordedAt: pt.recordedAt };
}

export function usePollLocations(groupId: string | null, intervalMs = 5000, cryptoKey: CryptoKey | null) {
  const [members, setMembers] = useState<MemberLocationDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const versionRef = useRef<number | undefined>(undefined);

  const poll = useCallback(async () => {
    if (!groupId || !cryptoKey) return;
    try {
      const data = await getLocations(groupId, versionRef.current);
      if (data) {
        versionRef.current = data.version;
        const decoded: MemberLocationDto[] = await Promise.all(
          data.members.map(async (m) => ({
            memberId: m.memberId,
            displayName: m.displayName,
            currentLocation: m.currentLocation
              ? await decodeGeoPoint(m.currentLocation, cryptoKey)
              : null,
            recentHistory: await Promise.all(
              m.recentHistory.map((pt) => decodeGeoPoint(pt, cryptoKey)),
            ),
          })),
        );
        setMembers((prev) => {
          const merged = new Map(prev.map((m) => [m.memberId, m]));
          for (const m of decoded) merged.set(m.memberId, m);
          return Array.from(merged.values());
        });
        setError(null);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Polling failed');
    }
  }, [groupId, cryptoKey]);

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
