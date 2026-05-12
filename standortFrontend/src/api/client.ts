import type {
  CreateGroupRequest,
  CreateGroupResponse,
  JoinGroupRequest,
  JoinGroupResponse,
  UpdateLocationRequest,
  GroupLocationsResponse,
  CreateMarkerRequest,
  MarkerDto,
  ApiError,
} from './types';
import { API_BASE } from './constants';

export class ApiException extends Error {
  readonly status: number;
  readonly body: ApiError;
  constructor(status: number, body: ApiError) {
    super(body.message);
    this.status = status;
    this.body = body;
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (res.ok) return res.json() as Promise<T>;
  let body: ApiError;
  try {
    body = await res.json();
  } catch {
    body = { error: 'unknown', message: `HTTP ${res.status}` };
  }
  throw new ApiException(res.status, body);
}

export async function createGroup(req: CreateGroupRequest): Promise<CreateGroupResponse> {
  const res = await fetch(`${API_BASE}/api/groups`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  return handleResponse<CreateGroupResponse>(res);
}

export async function joinGroup(req: JoinGroupRequest): Promise<JoinGroupResponse> {
  const res = await fetch(`${API_BASE}/api/groups/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(req),
  });
  return handleResponse<JoinGroupResponse>(res);
}

export async function updateLocation(
  groupId: string,
  memberId: string,
  token: string,
  req: UpdateLocationRequest,
): Promise<void> {
  const res = await fetch(`${API_BASE}/api/groups/${groupId}/members/${memberId}/location`, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
    },
    body: JSON.stringify(req),
  });
  if (res.status === 204) return;
  let body: ApiError;
  try {
    body = await res.json();
  } catch {
    body = { error: 'unknown', message: `HTTP ${res.status}` };
  }
  throw new ApiException(res.status, body);
}

export async function getLocations(
  groupId: string,
  sinceVersion?: number,
): Promise<GroupLocationsResponse | null> {
  const qs = sinceVersion != null ? `?sinceVersion=${sinceVersion}` : '';
  const res = await fetch(`${API_BASE}/api/groups/${groupId}/locations${qs}`);
  if (res.status === 304) return null;
  return handleResponse<GroupLocationsResponse>(res);
}

export async function createMarker(
  groupId: string,
  memberId: string,
  token: string,
  req: CreateMarkerRequest,
): Promise<MarkerDto> {
  const res = await fetch(`${API_BASE}/api/groups/${groupId}/members/${memberId}/markers`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(req),
  });
  return handleResponse<MarkerDto>(res);
}

export async function getMarkers(groupId: string): Promise<MarkerDto[]> {
  const res = await fetch(`${API_BASE}/api/groups/${groupId}/markers`);
  return handleResponse<MarkerDto[]>(res);
}

export async function deleteMarker(
  groupId: string,
  memberId: string,
  token: string,
  markerId: string,
): Promise<void> {
  const res = await fetch(
    `${API_BASE}/api/groups/${groupId}/members/${memberId}/markers/${markerId}`,
    { method: 'DELETE', headers: { Authorization: `Bearer ${token}` } },
  );
  if (res.status === 204) return;
  let body: ApiError;
  try {
    body = await res.json();
  } catch {
    body = { error: 'unknown', message: `HTTP ${res.status}` };
  }
  throw new ApiException(res.status, body);
}
