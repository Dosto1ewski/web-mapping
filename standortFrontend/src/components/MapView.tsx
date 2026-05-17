import { useEffect, useRef, Fragment } from 'react';
import L from 'leaflet';
import { MapContainer, TileLayer, Marker, Popup, Polyline, Tooltip, useMap, useMapEvents } from 'react-leaflet';
import type { MemberLocationDto, MarkerDto } from '../api/types';
import type { Session } from '../state/session';
import type { RouteProfile } from '../api/graphhopper';

import iconUrl from 'leaflet/dist/images/marker-icon.png';
import iconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png';
import shadowUrl from 'leaflet/dist/images/marker-shadow.png';

L.Icon.Default.mergeOptions({ iconUrl, iconRetinaUrl, shadowUrl });

const USER_COLORS = ['#e74c3c', '#3498db', '#2ecc71', '#f39c12', '#9b59b6', '#1abc9c', '#e67e22', '#e91e63'];

function getUserColor(memberId: string): string {
  let hash = 0;
  for (let i = 0; i < memberId.length; i++) {
    hash = (hash * 31 + memberId.charCodeAt(i)) & 0xffffffff;
  }
  return USER_COLORS[Math.abs(hash) % USER_COLORS.length];
}

function segmentBearing(from: [number, number], to: [number, number]): number {
  // from/to are [lat, lng]; atan2(Δlng, Δlat) maps directly to CSS rotate() (north=0°, east=90°)
  return Math.atan2(to[1] - from[1], to[0] - from[0]) * (180 / Math.PI);
}

function haversineMeters(a: [number, number], b: [number, number]): number {
  const R = 6371000;
  const dLat = ((b[0] - a[0]) * Math.PI) / 180;
  const dLng = ((b[1] - a[1]) * Math.PI) / 180;
  const lat1 = (a[0] * Math.PI) / 180;
  const lat2 = (b[0] * Math.PI) / 180;
  const x =
    Math.sin(dLat / 2) ** 2 +
    Math.cos(lat1) * Math.cos(lat2) * Math.sin(dLng / 2) ** 2;
  return 2 * R * Math.asin(Math.sqrt(x));
}

function trailArrows(
  trail: [number, number][],
  spacingM: number,
): { pos: [number, number]; angle: number; key: string }[] {
  const result: { pos: [number, number]; angle: number; key: string }[] = [];
  for (let i = 0; i < trail.length - 1; i++) {
    const from = trail[i];
    const to = trail[i + 1];
    const dist = haversineMeters(from, to);
    const angle = segmentBearing(from, to);
    const count = Math.max(1, Math.round(dist / spacingM));
    for (let j = 0; j < count; j++) {
      const t = (j + 0.5) / count;
      result.push({
        pos: [from[0] + (to[0] - from[0]) * t, from[1] + (to[1] - from[1]) * t],
        angle,
        key: `${i}-${j}`,
      });
    }
  }
  return result;
}

function createMemberIcon(color: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<div style="
      width:20px;height:20px;
      border-radius:50%;
      background:${color};
      border:3px solid white;
      box-shadow:0 2px 6px rgba(0,0,0,0.5);
    "></div>`,
    iconSize: [20, 20],
    iconAnchor: [10, 10],
    popupAnchor: [0, -14],
  });
}

function createArrowIcon(color: string, angleDeg: number): L.DivIcon {
  return L.divIcon({
    className: 'trail-arrow',
    html: `<svg width="14" height="14" viewBox="0 0 12 12" style="transform:rotate(${angleDeg}deg);display:block;overflow:visible;background:none;border:none;">
      <polygon points="6,0 12,12 6,9 0,12" fill="${color}" stroke="white" stroke-width="0.8"/>
    </svg>`,
    iconSize: [14, 14],
    iconAnchor: [7, 7],
  });
}

function createMarkerIcon(marker: MarkerDto): L.DivIcon {
  const raw = marker.icon ?? 'default';
  const kind = String(raw).toLowerCase().trim();

  let mapped = kind;
  if (['tree'].includes(kind)) mapped = 'tree';
  else if (['book', 'bildung', 'library'].includes(kind)) mapped = 'book';
  else if (['sekt', 'bottle', 'champagne', 'bar'].includes(kind)) mapped = 'champagne';

  const baseColor = marker.color ?? null;
  const markerBaseSvg = (size = 25, fill = baseColor ?? '#2a81d6') => `
    <svg viewBox="0 0 25 41" width="${size}" height="${Math.round(size * 1.64)}" style="display:block;">
      <path d="M12.5 0C7 0 3 4 3 9c0 6 9.5 18 9.5 18s9.5-12 9.5-18c0-5-4-9-9.5-9z" fill="${fill}" stroke="#fff" stroke-width="1.2"/>
      <circle cx="12.5" cy="11" r="3.2" fill="#fff" opacity="0.12"/>
    </svg>
  `;

  const leafletBaseImg = `<img src="${iconUrl}" srcset="${iconRetinaUrl} 2x" style="width:25px;height:41px;display:block;"/>`;

  if (mapped === 'tree' || mapped === 'book' || mapped === 'champagne') {
    const overlayPath = mapped === 'tree' ? '/tree.png' : mapped === 'book' ? '/book.png' : '/champagne.png';
    return L.divIcon({
      className: `marker-${mapped}-icon`,
      html: `<img src="${overlayPath}" alt="${mapped}" style="width:25px;height:41px;display:block;"/>`,
      iconSize: [25, 41],
      iconAnchor: [12, 41],
      popupAnchor: [0, -34],
    });
  }

  if (mapped === 'default' || mapped === '') {
    if (baseColor) {
      return L.divIcon({
        className: 'marker-default-icon',
        html: `<div style="width:25px;height:41px;">${markerBaseSvg(25, baseColor)}</div>`,
        iconSize: [25, 41],
        iconAnchor: [12, 41],
        popupAnchor: [0, -34],
      });
    }
    return L.divIcon({
      className: 'marker-default-icon',
      html: leafletBaseImg,
      iconSize: [25, 41],
      iconAnchor: [12, 41],
      popupAnchor: [0, -34],
    });
  }

  if (baseColor) {
    return L.divIcon({
      className: 'marker-default-icon',
      html: `<div style="width:25px;height:41px;">${markerBaseSvg(25, baseColor)}</div>`,
      iconSize: [25, 41],
      iconAnchor: [12, 41],
      popupAnchor: [0, -34],
    });
  }

  return L.divIcon({
    className: 'marker-default-icon',
    html: leafletBaseImg,
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [0, -34],
  });
}

function MapRefCapture({ onMapReady }: { onMapReady: (map: L.Map) => void }) {
  const map = useMap();
  useEffect(() => { onMapReady(map); }, [map, onMapReady]);
  return null;
}

function FitBounds({ members }: { members: MemberLocationDto[] }) {
  const map = useMap();
  const hasFit = useRef(false);
  useEffect(() => {
    if (hasFit.current) return;
    const points = members
      .filter((m) => m.currentLocation != null)
      .map((m) => [m.currentLocation!.lat, m.currentLocation!.lng] as [number, number]);
    if (points.length > 0) {
      map.fitBounds(L.latLngBounds(points), { maxZoom: 16, padding: [40, 40] });
      hasFit.current = true;
    }
  }, [members, map]);
  return null;
}

function MapClickHandler({
  placingMarker,
  onMapClick,
}: {
  placingMarker: boolean;
  onMapClick: (lat: number, lng: number) => void;
}) {
  const map = useMapEvents({
    click(e) {
      if (placingMarker) onMapClick(e.latlng.lat, e.latlng.lng);
    },
  });

  useEffect(() => {
    const container = map.getContainer();
    container.style.cursor = placingMarker ? 'crosshair' : '';
  }, [placingMarker, map]);

  return null;
}

interface Props {
  readonly members: MemberLocationDto[];
  readonly markers: MarkerDto[];
  readonly session: Session | null;
  readonly placingMarker: boolean;
  readonly onMapClick: (lat: number, lng: number) => void;
  readonly onDeleteMarker: (markerId: string) => void;
  readonly placingLocation: boolean;
  readonly locationDragPos: { lat: number; lng: number };
  readonly onLocationDragEnd: (pos: { lat: number; lng: number }) => void;
  readonly showNametags: boolean;
  readonly ownLocation: { lat: number; lng: number } | null;
  readonly activeRouteMarkerId: string | null;
  readonly activeRouteProfile: RouteProfile | null;
  readonly routeGeometry: [number, number][] | null;
  readonly onRouteRequest: (markerId: string, profile: RouteProfile) => void;
}

function createOwnIcon(color: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<div style="
      width:22px;height:22px;
      border-radius:50%;
      background:${color};
      border:3px solid white;
      box-shadow:0 0 0 3px ${color}55, 0 2px 8px rgba(0,0,0,0.5);
    "></div>`,
    iconSize: [22, 22],
    iconAnchor: [11, 11],
    popupAnchor: [0, -15],
  });
}

export default function MapView({
  members,
  markers,
  session,
  placingMarker,
  onMapClick,
  onDeleteMarker,
  placingLocation,
  locationDragPos,
  onLocationDragEnd,
  showNametags,
  ownLocation,
  activeRouteMarkerId,
  activeRouteProfile,
  routeGeometry,
  onRouteRequest,
}: Props) {
  const ownMemberId = session?.memberId ?? null;
  const mapRef = useRef<L.Map | null>(null);

  function handleLocateMe() {
    const map = mapRef.current;
    if (!map) return;
    if (ownLocation) {
      map.flyTo([ownLocation.lat, ownLocation.lng], Math.max(map.getZoom(), 16), { animate: true, duration: 1 });
    } else if (navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (pos) => map.flyTo([pos.coords.latitude, pos.coords.longitude], Math.max(map.getZoom(), 16), { animate: true, duration: 1 }),
        () => {},
        { enableHighAccuracy: true },
      );
    }
  }

  return (
    <>
    <MapContainer center={[49.0069, 8.4037]} zoom={13} className="leaflet-map">
      <TileLayer
        url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        maxZoom={19}
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap contributors</a>'
      />
      <MapRefCapture onMapReady={(m) => { mapRef.current = m; }} />
      <FitBounds members={members} />
      <MapClickHandler placingMarker={placingMarker} onMapClick={onMapClick} />

      {members.map((member) => {
        if (member.memberId === ownMemberId) return null;
        if (!member.currentLocation) return null;
        const pos: [number, number] = [member.currentLocation.lat, member.currentLocation.lng];
        const color = getUserColor(member.memberId);
        const trail: [number, number][] = [
          ...member.recentHistory.map((p): [number, number] => [p.lat, p.lng]),
          pos,
        ];
        const arrows = trailArrows(trail, 30);
        return (
          <Fragment key={member.memberId}>
            <Marker position={pos} icon={createMemberIcon(color)}>
              <Popup>{member.displayName}</Popup>
              {showNametags && (
                <Tooltip permanent direction="right" offset={[12, 0]} className="member-nametag">
                  {member.displayName}
                </Tooltip>
              )}
            </Marker>
            {trail.length > 1 && (
              <Polyline
                positions={trail}
                pathOptions={{ color, opacity: 0.35, weight: 2, dashArray: '4 7' }}
              />
            )}
            {arrows.map(({ pos: arrowPos, angle, key }) => (
              <Marker
                key={key}
                position={arrowPos}
                icon={createArrowIcon(color, angle)}
                zIndexOffset={-100}
                interactive={false}
              />
            ))}
          </Fragment>
        );
      })}

      {ownLocation && session && (() => {
        const pos: [number, number] = [ownLocation.lat, ownLocation.lng];
        const color = getUserColor(session.memberId);
        return (
          <Marker key="own-live" position={pos} icon={createOwnIcon(color)}>
            <Popup>{session.displayName} (du)</Popup>
            {showNametags && (
              <Tooltip permanent direction="right" offset={[13, 0]} className="member-nametag">
                {session.displayName}
              </Tooltip>
            )}
          </Marker>
        );
      })()}

      {markers.map((marker) => {
         
        console.debug('Marker loaded:', marker.markerId, 'icon=', marker.icon, 'color=', marker.color);
        const pos: [number, number] = [marker.lat, marker.lng];
        const icon = createMarkerIcon(marker);
        const isActive = marker.markerId === activeRouteMarkerId;
        return (
          <Marker key={marker.markerId} position={pos} icon={icon}>
            <Popup>
              <div className="marker-popup">
                <strong>{marker.name}</strong>
                {marker.notes && <p className="marker-popup-notes">{marker.notes}</p>}
                {session && (
                  <>
                    <div className="route-profile-row">
                      {(['foot', 'bike', 'car'] as RouteProfile[]).map((p) => (
                        <button
                          key={p}
                          type="button"
                          className={`route-profile-btn${isActive && activeRouteProfile === p ? ' selected' : ''}`}
                          onClick={() => onRouteRequest(marker.markerId, p)}
                          title={profileLabel(p)}
                        >
                          <ProfileIcon profile={p} />
                        </button>
                      ))}
                    </div>
                    <button
                      className="marker-delete-btn"
                      onClick={() => onDeleteMarker(marker.markerId)}
                      title="Marker löschen"
                    >
                      <TrashIcon />
                    </button>
                  </>
                )}
              </div>
            </Popup>
            {showNametags && (
              <Tooltip permanent direction="top" offset={[0, -12]} className="member-nametag">
                {marker.name}
              </Tooltip>
            )}
          </Marker>
        );
      })}

      {routeGeometry && routeGeometry.length > 1 && (
        <Polyline
          positions={routeGeometry}
          pathOptions={{ color: '#1d7af3', weight: 5, opacity: 0.85 }}
        />
      )}

      {placingLocation && (
        <Marker
          position={[locationDragPos.lat, locationDragPos.lng]}
          draggable={true}
          eventHandlers={{
            dragend(e) {
              const { lat, lng } = (e.target as L.Marker).getLatLng();
              onLocationDragEnd({ lat, lng });
            },
          }}
        >
          <Popup>Ziehe den Marker an deinen Standort</Popup>
        </Marker>
      )}
    </MapContainer>
    <button
      className="locate-me-btn"
      onClick={handleLocateMe}
      title="Zu meinem Standort springen"
      aria-label="Zu meinem Standort springen"
    >
      <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/>
      </svg>
    </button>
    </>
  );
}

function profileLabel(p: RouteProfile): string {
  if (p === 'foot') return 'Zu Fuß';
  if (p === 'bike') return 'Fahrrad';
  return 'Auto';
}

function ProfileIcon({ profile }: { readonly profile: RouteProfile }) {
  if (profile === 'foot') {
    return (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="13" cy="4" r="2" />
        <path d="M11 21l1-7-3-2-1 4-3 1" />
        <path d="M12 14l3 3 4 1" />
        <path d="M9 9l3-3 4 2 3-1" />
      </svg>
    );
  }
  if (profile === 'bike') {
    return (
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
        <circle cx="5.5" cy="17.5" r="3.5" />
        <circle cx="18.5" cy="17.5" r="3.5" />
        <path d="M15 6l-3 6h6l-3-6z" />
        <path d="M9 6h3l3 6-5 0L5.5 17.5" />
        <path d="M14 6h2" />
      </svg>
    );
  }
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M5 17H3v-4l2-5h14l2 5v4h-2" />
      <circle cx="7" cy="17" r="2" />
      <circle cx="17" cy="17" r="2" />
      <path d="M5 13h14" />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg
      width="14"
      height="14"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <polyline points="3 6 5 6 21 6" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
      <path d="M10 11v6M14 11v6" />
      <path d="M9 6V4a1 1 0 0 1 1-1h4a1 1 0 0 1 1 1v2" />
    </svg>
  );
}
