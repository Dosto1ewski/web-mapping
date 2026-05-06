import { useEffect } from 'react';
import L from 'leaflet';
import { MapContainer, TileLayer, Marker, Popup, Polyline, useMap } from 'react-leaflet';
import type { MemberLocationDto } from '../api/types';

import iconUrl from 'leaflet/dist/images/marker-icon.png';
import iconRetinaUrl from 'leaflet/dist/images/marker-icon-2x.png';
import shadowUrl from 'leaflet/dist/images/marker-shadow.png';

L.Icon.Default.mergeOptions({ iconUrl, iconRetinaUrl, shadowUrl });

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

interface Props {
  members: MemberLocationDto[];
}

export default function MapView({ members }: Props) {
  return (
    <MapContainer center={[49.0069, 8.4037]} zoom={13} className="leaflet-map">
      <TileLayer
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        maxZoom={19}
        attribution="&copy; OpenStreetMap contributors"
      />
      <FitBounds members={members} />
      {members.map((member) => {
        if (!member.currentLocation) return null;
        const pos: [number, number] = [member.currentLocation.lat, member.currentLocation.lng];
        const trail: [number, number][] = [
          ...member.recentHistory.map((p): [number, number] => [p.lat, p.lng]),
          pos,
        ];
        return (
          <div key={member.memberId}>
            <Marker position={pos}>
              <Popup>{member.displayName}</Popup>
            </Marker>
            {trail.length >= 2 && <Polyline positions={trail} color="royalblue" weight={2} />}
          </div>
        );
      })}
    </MapContainer>
  );
}
