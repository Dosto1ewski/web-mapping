export type RouteProfile = 'foot' | 'bike' | 'car';

export interface RouteResult {
  geometry: [number, number][];
  distanceM: number;
  timeMs: number;
}

export class GraphHopperError extends Error {
  status?: number;
  constructor(message: string, status?: number) {
    super(message);
    this.status = status;
  }
}

const THROTTLE_MS = 5000;
let lastRequestAt = 0;

export async function fetchRoute(
  token: string,
  from: { lat: number; lng: number },
  to: { lat: number; lng: number },
  profile: RouteProfile,
): Promise<RouteResult> {
  if (!token) {
    throw new GraphHopperError('GraphHopper API-Token in Einstellungen setzen.');
  }
  const now = Date.now();
  const wait = lastRequestAt + THROTTLE_MS - now;
  if (wait > 0) {
    throw new GraphHopperError(`Bitte ${Math.ceil(wait / 1000)}s warten, bevor erneut geroutet wird.`);
  }
  lastRequestAt = now;

  const url = `https://graphhopper.com/api/1/route?key=${encodeURIComponent(token)}`;
  const body = {
    points: [
      [from.lng, from.lat],
      [to.lng, to.lat],
    ],
    profile,
    locale: 'de',
    instructions: false,
    calc_points: true,
    points_encoded: false,
  };

  let resp: Response;
  try {
    resp = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
  } catch {
    throw new GraphHopperError('Netzwerkfehler beim Routing.');
  }

  if (!resp.ok) {
    let message = `Routing fehlgeschlagen (${resp.status}).`;
    try {
      const err = await resp.json();
      if (err?.message) message = err.message;
    } catch {}
    if (resp.status === 401) message = 'Ungültiger GraphHopper-Token.';
    if (resp.status === 429) message = 'GraphHopper-Quota erreicht — später erneut versuchen.';
    throw new GraphHopperError(message, resp.status);
  }

  const data = await resp.json();
  const path = data?.paths?.[0];
  const coords = path?.points?.coordinates;
  if (!path || !Array.isArray(coords) || coords.length < 2) {
    throw new GraphHopperError('Keine Route gefunden.');
  }
  return {
    geometry: coords.map((c: [number, number]) => [c[1], c[0]] as [number, number]),
    distanceM: Number(path.distance) || 0,
    timeMs: Number(path.time) || 0,
  };
}

export function formatDistance(m: number): string {
  if (m < 1000) return `${Math.round(m)} m`;
  return `${(m / 1000).toFixed(1)} km`;
}

export function formatDuration(ms: number): string {
  const totalMin = Math.round(ms / 60000);
  if (totalMin < 60) return `${totalMin} min`;
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${h}h ${m.toString().padStart(2, '0')}min`;
}
