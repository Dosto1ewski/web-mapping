import { useState, useEffect } from 'react';
import 'leaflet/dist/leaflet.css';
import { useSession } from './state/session';
import { useSettings } from './state/settings';
import { usePollLocations } from './hooks/usePollLocations';
import { usePollMarkers } from './hooks/usePollMarkers';
import { createMarker, deleteMarker, ApiException } from './api/client';
import Lobby from './components/Lobby';
import MapView from './components/MapView';
import MemberControls from './components/MemberControls';
import SettingsPanel from './components/SettingsPanel';
import MarkerDialog from './components/MarkerDialog';
import type { Session } from './state/session';

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

  const { members, error: locError } = usePollLocations(
    session?.groupId ?? null,
    settings.locationFetchSec * 1000,
  );
  const { markers, error: markerError, refresh: refreshMarkers } = usePollMarkers(
    session?.groupId ?? null,
    settings.markerFetchSec * 1000,
  );

  const defaultInviteCode = new URL(location.href).searchParams.get('invite') ?? undefined;

  useEffect(() => {
    if (locError) setStatus(locError);
  }, [locError]);

  useEffect(() => {
    if (markerError) setStatus(markerError);
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
    setStatus('Gruppe verlassen.');
  }

  function handleSessionInvalidated() {
    clearSession();
    setInviteCode(null);
    setPlacingMarker(false);
    setPlacingLocation(false);
    setPendingCoords(null);
    setOwnLocation(null);
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

  async function handleCreateMarker(name: string, color: string, notes: string | null) {
    if (!session || !pendingCoords) return;
    await createMarker(session.groupId, session.memberId, session.memberToken, {
      name,
      lat: pendingCoords.lat,
      lng: pendingCoords.lng,
      color,
      notes,
    });
    setPendingCoords(null);
    refreshMarkers();
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

  return (
    <div className="app">
      <div className="panel">
        {!session ? (
          <Lobby defaultInviteCode={defaultInviteCode} onJoined={handleJoined} />
        ) : (
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
          />
        )}
        {status && <p className="status">{status}</p>}
      </div>

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
      />

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
