import { useEffect, Fragment } from 'react';
import L from 'leaflet';
import { MapContainer, TileLayer, Marker, Popup, useMap, useMapEvents } from 'react-leaflet';
import type { MemberLocationDto, MarkerDto } from '../api/types';
import type { Session } from '../state/session';

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
  return Math.atan2(to[1] - from[1], to[0] - from[0]) * (180 / Math.PI) - 90;
}

function createArrowIcon(color: string, angleDeg: number): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<svg width="14" height="14" viewBox="0 0 12 12" style="transform:rotate(${angleDeg}deg);display:block;overflow:visible;">
      <polygon points="6,0 12,12 6,9 0,12" fill="${color}" stroke="white" stroke-width="0.8"/>
    </svg>`,
    iconSize: [14, 14],
    iconAnchor: [7, 7],
  });
}

function createColoredIcon(color: string): L.DivIcon {
  return L.divIcon({
    className: '',
    html: `<div style="
      width:18px;height:18px;
      border-radius:50% 50% 50% 0;
      background:${color};
      border:2px solid white;
      box-shadow:0 1px 4px rgba(0,0,0,0.45);
      transform:rotate(-45deg)
    "></div>`,
    iconSize: [18, 18],
    iconAnchor: [9, 18],
    popupAnchor: [0, -20],
  });
}

function FitBounds({ members }: { members: MemberLocationDto[] }) {
  const map = useMap();
  useEffect(() => {
    const points = members
      .filter((m) => m.currentLocation != null)
      .map((m) => [m.currentLocation!.lat, m.currentLocation!.lng] as [number, number]);
    if (points.length > 0) {
      map.fitBounds(L.latLngBounds(points), { maxZoom: 16, padding: [40, 40] });
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
  members: MemberLocationDto[];
  markers: MarkerDto[];
  session: Session | null;
  placingMarker: boolean;
  onMapClick: (lat: number, lng: number) => void;
  onDeleteMarker: (markerId: string) => void;
  placingLocation: boolean;
  locationDragPos: { lat: number; lng: number };
  onLocationDragEnd: (pos: { lat: number; lng: number }) => void;
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
}: Props) {
  return (
    <MapContainer center={[49.0069, 8.4037]} zoom={13} className="leaflet-map">
      <TileLayer
        url="https://tile.openstreetmap.org/{z}/{x}/{y}.png"
        maxZoom={19}
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap contributors</a>'
      />
      <FitBounds members={members} />
      <MapClickHandler placingMarker={placingMarker} onMapClick={onMapClick} />

      {members.map((member) => {
        if (!member.currentLocation) return null;
        const pos: [number, number] = [member.currentLocation.lat, member.currentLocation.lng];
        const color = getUserColor(member.memberId);
        const trail: [number, number][] = [
          ...member.recentHistory.map((p): [number, number] => [p.lat, p.lng]),
          pos,
        ];
        const arrows = trail.slice(0, -1).map((from, i) => {
          const to = trail[i + 1];
          const mid: [number, number] = [(from[0] + to[0]) / 2, (from[1] + to[1]) / 2];
          return { mid, angle: segmentBearing(from, to), key: i };
        });
        return (
          <Fragment key={member.memberId}>
            <Marker position={pos}>
              <Popup>{member.displayName}</Popup>
            </Marker>
            {arrows.map(({ mid, angle, key }) => (
              <Marker
                key={key}
                position={mid}
                icon={createArrowIcon(color, angle)}
                zIndexOffset={-100}
                interactive={false}
              />
            ))}
          </Fragment>
        );
      })}

      {markers.map((marker) => {
        const pos: [number, number] = [marker.lat, marker.lng];
        const icon = createColoredIcon(marker.color ?? '#9ca3af');
        return (
          <Marker key={marker.markerId} position={pos} icon={icon}>
            <Popup>
              <div className="marker-popup">
                <strong>{marker.name}</strong>
                {marker.notes && <p className="marker-popup-notes">{marker.notes}</p>}
                {session && (
                  <button
                    className="marker-delete-btn"
                    onClick={() => onDeleteMarker(marker.markerId)}
                    title="Marker löschen"
                  >
                    <TrashIcon />
                  </button>
                )}
              </div>
            </Popup>
          </Marker>
        );
      })}

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
