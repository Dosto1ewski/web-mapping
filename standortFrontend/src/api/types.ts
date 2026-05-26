// ── Requests ────────────────────────────────────────────────────────────────

export interface CreateGroupRequest {
  name: string;
  createdByDisplayName: string;
  inviteCodeHash: string;
}

export interface CreateGroupResponse {
  groupId: string;
  memberId: string;
  memberToken: string;
  displayName: string;
  historyDurationMinutes: number;
}

export interface JoinGroupRequest {
  inviteCode: string;
  displayName: string;
  takeover?: boolean;
}

export interface NameInUseError {
  error: 'name_in_use';
  message: string;
  displayName: string;
  lastSeen: string;
  hasLocation: boolean;
}

export interface JoinGroupResponse {
  groupId: string;
  memberId: string;
  memberToken: string;
  displayName: string;
  historyDurationMinutes: number;
}

export interface UpdateMemberSettingsRequest {
  historyDurationMinutes: number;
}

// Wire format sent to / received from the backend (encrypted blobs)
export interface UpdateLocationRequest {
  encryptedLocation: string;
  recordedAt: string;
}

export interface CreateMarkerRequest {
  name: string;
  encryptedLocation: string;
  encryptedNotes: string | null;
  color?: string | null;
  icon?: 'tree' | 'book' | 'champagne' | 'default' | null;
}

// ── Wire response types (encrypted) ─────────────────────────────────────────

export interface WireGeoPointDto {
  encryptedLocation: string;
  recordedAt: string;
}

export interface WireMemberLocationDto {
  memberId: string;
  displayName: string;
  currentLocation: WireGeoPointDto | null;
  recentHistory: WireGeoPointDto[];
}

export interface WireGroupLocationsResponse {
  groupId: string;
  version: number;
  members: WireMemberLocationDto[];
}

export interface WireMarkerDto {
  markerId: string;
  name: string;
  encryptedLocation: string;
  encryptedNotes: string | null;
  color: string | null;
  createdByMemberId: string;
  createdAt: string;
  icon?: 'tree' | 'book' | 'champagne' | 'default' | null;
}

// ── Decoded (display) types ─────────────────────────────────────────────────

export interface GeoPointDto {
  lat: number;
  lng: number;
  accuracyMeters: number;
  recordedAt: string;
}

export interface MemberLocationDto {
  memberId: string;
  displayName: string;
  currentLocation: GeoPointDto | null;
  recentHistory: GeoPointDto[];
}

export interface GroupLocationsResponse {
  groupId: string;
  version: number;
  members: MemberLocationDto[];
}

export interface MarkerDto {
  markerId: string;
  name: string;
  lat: number;
  lng: number;
  color: string | null;
  notes: string | null;
  createdByMemberId: string;
  createdAt: string;
  icon?: 'tree' | 'book' | 'champagne' | 'default' | null;
}

// ── Misc ─────────────────────────────────────────────────────────────────────

export interface ApiError {
  error: string;
  message: string;
  fields?: { field: string; message: string }[];
}
