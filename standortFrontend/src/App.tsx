import { useState, useEffect } from 'react';
import 'leaflet/dist/leaflet.css';
import { useSession } from './state/session';
import { useSettings } from './state/settings';
import { usePollLocations } from './hooks/usePollLocations';
import { usePollMarkers } from './hooks/usePollMarkers';
import { createMarker, deleteMarker, ApiException } from './api/client';
import { fetchRoute, formatDistance, formatDuration, GraphHopperError } from './api/graphhopper';
import type { RouteProfile } from './api/graphhopper';
import Lobby from './components/Lobby';
import MapView from './components/MapView';
import MemberControls from './components/MemberControls';
import SettingsPanel from './components/SettingsPanel';
import MarkerDialog from './components/MarkerDialog';
import type { Session } from './state/session';

function RouteProfileGlyph({ profile }: Readonly<{ profile: RouteProfile }>) {
  let label = '🚗';
  if (profile === 'foot') label = '🚶';
  else if (profile === 'bike') label = '🚴';
  return <span className="route-overlay-glyph" aria-hidden="true">{label}</span>;
}

interface ActiveRoute {
  markerId: string;
  profile: RouteProfile;
  geometry: [number, number][];
  distanceM: number;
  timeMs: number;
}

export default function App() {
  const { session, setSession, clearSession } = useSession();
  const { settings, updateSettings } = useSettings();
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [status, setStatus] = useState('');
  const [placingMarker, setPlacingMarker] = useState(false);
  const [pendingCoords, setPendingCoords] = useState<{ lat: number; lng: number } | null>(null);
  const [placingLocation, setPlacingLocation] = useState(false);
  const [locationDragPos, setLocationDragPos] = useState<{ lat: number; lng: number }>({ lat: 49.0069, lng: 8.4037 });
  const [ownLocation, setOwnLocation] = useState<{ lat: number; lng: number } | null>(null);
  const [activeRoute, setActiveRoute] = useState<ActiveRoute | null>(null);
  const [panelCollapsed, setPanelCollapsed] = useState(false);

  const { members, error: locError } = usePollLocations(
    session?.groupId ?? null,
    settings.locationFetchSec * 1000,
  );
  const { markers, error: markerError, refresh: refreshMarkers } = usePollMarkers(
    session?.groupId ?? null,
    settings.markerFetchSec * 1000,
  );

  const defaultInviteCode = new URL(globalThis.location.href).searchParams.get('invite') ?? undefined;

  useEffect(() => {
    if (!locError) return;
    const id = globalThis.setTimeout(() => setStatus(locError), 0);
    return () => globalThis.clearTimeout(id);
  }, [locError]);

  useEffect(() => {
    if (!markerError) return;
    const id = globalThis.setTimeout(() => setStatus(markerError), 0);
    return () => globalThis.clearTimeout(id);
  }, [markerError]);

  function handleJoined(s: Session, code?: string) {
    setSession(s);
    if (code) setInviteCode(code);
    setStatus(code ? `Gruppe erstellt. Einladungscode: ${code}` : 'Beigetreten.');
  }

  function handleLeave() {
    clearSession();
    setInviteCode(null);
    setPlacingMarker(false);
    setPlacingLocation(false);
    setPendingCoords(null);
    setOwnLocation(null);
    setActiveRoute(null);
    setStatus('Gruppe verlassen.');
  }

  function handleSessionInvalidated() {
    clearSession();
    setInviteCode(null);
    setPlacingMarker(false);
    setPlacingLocation(false);
    setPendingCoords(null);
    setOwnLocation(null);
    setActiveRoute(null);
    setStatus('Sitzung abgelaufen — ein anderes Gerät hat sich mit diesem Namen angemeldet.');
  }

  function handleTogglePlacingLocation() {
    if (!placingLocation) {
      const own = members.find((m) => m.memberId === session?.memberId);
      if (own?.currentLocation) {
        setLocationDragPos({ lat: own.currentLocation.lat, lng: own.currentLocation.lng });
      }
    }
    setPlacingLocation((v) => !v);
  }

  function handleMapClick(lat: number, lng: number) {
    setPlacingMarker(false);
    setPendingCoords({ lat, lng });
  }

  async function handleCreateMarker(name: string, color: string | null, notes: string | null, icon?: 'tree' | 'book' | 'champagne' | 'default' | null) {
    if (!session || !pendingCoords) return;
    await createMarker(session.groupId, session.memberId, session.memberToken, {
      name,
      lat: pendingCoords.lat,
      lng: pendingCoords.lng,
      color,
      notes,
      icon: icon ?? null,
    });
    setPendingCoords(null);
    refreshMarkers();
  }

  useEffect(() => {
    if (activeRoute && !markers.some((m) => m.markerId === activeRoute.markerId)) {
      const id = globalThis.setTimeout(() => setActiveRoute(null), 0);
      return () => globalThis.clearTimeout(id);
    }
  }, [markers, activeRoute]);

  async function handleRouteRequest(markerId: string, profile: RouteProfile) {
    const target = markers.find((m) => m.markerId === markerId);
    if (!target) return;
    const start =
      ownLocation ??
      (session ? members.find((m) => m.memberId === session.memberId)?.currentLocation ?? null : null);
    if (!start) {
      setStatus('Eigenen Standort teilen, um Route zu berechnen.');
      return;
    }
    try {
      const result = await fetchRoute(
        settings.graphhopperToken,
        { lat: start.lat, lng: start.lng },
        { lat: target.lat, lng: target.lng },
        profile,
      );
      setActiveRoute({ markerId, profile, ...result });
      setStatus('');
    } catch (err) {
      if (err instanceof GraphHopperError) setStatus(err.message);
      else setStatus(err instanceof Error ? err.message : 'Routing fehlgeschlagen.');
    }
  }

  async function handleDeleteMarker(markerId: string) {
    if (!session) return;
    try {
      await deleteMarker(session.groupId, session.memberId, session.memberToken, markerId);
      refreshMarkers();
    } catch (err) {
      if (err instanceof ApiException && err.status === 401) {
        handleSessionInvalidated();
      } else {
        setStatus(err instanceof Error ? err.message : 'Löschen fehlgeschlagen.');
      }
    }
  }

  const nonSessionContent = panelCollapsed ? null : <Lobby defaultInviteCode={defaultInviteCode} onJoined={handleJoined} />;

  return (
    <div className="app">
      <div className={`panel${panelCollapsed ? ' panel--collapsed' : ''}`}>
        <div className="panel-body">
          {session ? (
            <MemberControls
              session={session}
              inviteCode={inviteCode}
              onLeave={handleLeave}
              onSessionInvalidated={handleSessionInvalidated}
              onStatus={setStatus}
              placingMarker={placingMarker}
              onTogglePlacingMarker={() => setPlacingMarker((v) => !v)}
              locationUpdateIntervalMs={settings.locationUpdateSec * 1000}
              placingLocation={placingLocation}
              onTogglePlacingLocation={handleTogglePlacingLocation}
              locationDragPos={locationDragPos}
              onOwnLocation={setOwnLocation}
              onHistoryDurationChange={(minutes) =>
                setSession({ ...session, historyDurationMinutes: minutes })
              }
              collapsed={panelCollapsed}
            />
          ) : nonSessionContent}
          {!panelCollapsed && status && <p className="status">{status}</p>}
        </div>
        <button
          className="panel-collapse-toggle"
          onClick={() => setPanelCollapsed((v) => !v)}
          title={panelCollapsed ? 'Panel öffnen' : 'Panel schließen'}
          aria-label={panelCollapsed ? 'Panel öffnen' : 'Panel schließen'}
        >
          <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
            {panelCollapsed
              ? <polyline points="9 18 15 12 9 6" />
              : <polyline points="15 18 9 12 15 6" />}
          </svg>
        </button>
      </div>

      {panelCollapsed && (
        <button
          className="panel-open-button"
          onClick={() => setPanelCollapsed(false)}
          title="Panel öffnen"
          aria-label="Panel öffnen"
        >
          ◀
        </button>
      )}

      <SettingsPanel settings={settings} onUpdate={updateSettings} />

      <MapView
        members={members}
        markers={markers}
        session={session ?? null}
        placingMarker={placingMarker}
        onMapClick={handleMapClick}
        onDeleteMarker={handleDeleteMarker}
        placingLocation={placingLocation}
        locationDragPos={locationDragPos}
        onLocationDragEnd={setLocationDragPos}
        showNametags={settings.showNametags}
        ownLocation={ownLocation}
        activeRouteMarkerId={activeRoute?.markerId ?? null}
        activeRouteProfile={activeRoute?.profile ?? null}
        routeGeometry={activeRoute?.geometry ?? null}
        onRouteRequest={handleRouteRequest}
      />

      {activeRoute && (
        <div className="route-overlay">
          <RouteProfileGlyph profile={activeRoute.profile} />
          <span className="route-overlay-stats">
            {formatDistance(activeRoute.distanceM)} · {formatDuration(activeRoute.timeMs)}
          </span>
          <button
            className="route-overlay-close"
            onClick={() => setActiveRoute(null)}
            title="Route schließen"
            aria-label="Route schließen"
          >
            ×
          </button>
        </div>
      )}

      {pendingCoords && (
        <MarkerDialog
          lat={pendingCoords.lat}
          lng={pendingCoords.lng}
          onConfirm={handleCreateMarker}
          onCancel={() => setPendingCoords(null)}
        />
      )}
    </div>
  );
}
