export interface CreateGroupRequest {
  name: string;
  createdByDisplayName: string;
}

export interface CreateGroupResponse {
  groupId: string;
  inviteCode: string;
  memberId: string;
  memberToken: string;
  displayName: string;
}

export interface JoinGroupRequest {
  inviteCode: string;
  displayName: string;
}

export interface JoinGroupResponse {
  groupId: string;
  memberId: string;
  memberToken: string;
  displayName: string;
}

export interface UpdateLocationRequest {
  lat: number;
  lng: number;
  accuracyMeters: number;
  recordedAt: string;
}

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

export interface ApiError {
  error: string;
  message: string;
  fields?: { field: string; message: string }[];
}

export interface CreateMarkerRequest {
  name: string;
  lat: number;
  lng: number;
  color?: string | null;
  notes?: string | null;
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
}
