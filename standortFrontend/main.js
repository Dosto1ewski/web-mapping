// Minimal, well-commented frontend for Standort using Leaflet and the existing backend API.

// Config
const API_BASE = '/api'; // adjust if backend is mounted elsewhere
const POLL_INTERVAL_MS = 5000;

// State (persisted credentials)
const state = {
  groupId: localStorage.getItem('groupId'),
  memberId: localStorage.getItem('memberId'),
  memberToken: localStorage.getItem('memberToken'),
  displayName: localStorage.getItem('displayName'),
  version: null,
};

// Map initialization
const map = L.map('map').setView([51.505, -0.09], 13);
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '&copy; OpenStreetMap contributors'
}).addTo(map);

// Marker layer group
const markers = L.layerGroup().addTo(map);
const polylines = L.layerGroup().addTo(map);

// UI elements
const createBtn = document.getElementById('create-btn');
const joinBtn = document.getElementById('join-btn');
const locBtn = document.getElementById('loc-btn');
const autoShare = document.getElementById('auto-share');
const inviteLinkEl = document.getElementById('invite-link');
const statusEl = document.getElementById('status');
const memberControls = document.getElementById('member-controls');
const leaveBtn = document.getElementById('leave-btn');

function setStatus(msg) { statusEl.textContent = msg; }

function updateMemberControls() {
  if (state.groupId && state.memberId && state.memberToken) {
    memberControls.style.display = 'block';
    inviteLinkEl.textContent = `${location.origin}/?invite=${state.groupId}`;
    inviteLinkEl.href = `${location.origin}/?invite=${state.groupId}`;
  } else {
    memberControls.style.display = 'none';
  }
}

updateMemberControls();

// Helper: store credentials
function saveCredentials() {
  localStorage.setItem('groupId', state.groupId || '');
  localStorage.setItem('memberId', state.memberId || '');
  localStorage.setItem('memberToken', state.memberToken || '');
  localStorage.setItem('displayName', state.displayName || '');
}

function clearCredentials() {
  state.groupId = state.memberId = state.memberToken = state.displayName = null;
  state.version = null;
  saveCredentials();
  updateMemberControls();
  markers.clearLayers();
  polylines.clearLayers();
}

// Create group
createBtn.addEventListener('click', async () => {
  const name = document.getElementById('group-name').value.trim();
  const creator = document.getElementById('creator-name').value.trim();
  if (!name || !creator) { setStatus('Group name and creator name required.'); return; }
  setStatus('Creating group...');
  try {
    const res = await fetch(`${API_BASE}/groups`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ name, createdByDisplayName: creator })
    });
    if (!res.ok) throw new Error(`Create failed: ${res.status}`);
    const body = await res.json();
    state.groupId = body.groupId;
    state.memberId = body.memberId;
    state.memberToken = body.memberToken;
    state.displayName = body.displayName;
    saveCredentials();
    updateMemberControls();
    setStatus('Group created. Invite code: ' + body.inviteCode);
    pollLoop();
  } catch (err) {
    console.error(err);
    setStatus('Failed to create group.');
  }
});

// Join group
joinBtn.addEventListener('click', async () => {
  const code = document.getElementById('invite-code').value.trim();
  const name = document.getElementById('join-name').value.trim();
  if (!code || !name) { setStatus('Invite code and display name required.'); return; }
  setStatus('Joining group...');
  try {
    const res = await fetch(`${API_BASE}/groups/join`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ inviteCode: code, displayName: name })
    });
    if (!res.ok) throw new Error(`Join failed: ${res.status}`);
    const body = await res.json();
    state.groupId = body.groupId;
    state.memberId = body.memberId;
    state.memberToken = body.memberToken;
    state.displayName = body.displayName;
    saveCredentials();
    updateMemberControls();
    setStatus('Joined group.');
    pollLoop();
  } catch (err) {
    console.error(err);
    setStatus('Failed to join group.');
  }
});

// Leave group
leaveBtn.addEventListener('click', () => {
  clearCredentials();
  setStatus('Left group.');
});

// Share current geolocation now
locBtn.addEventListener('click', async () => {
  if (!state.groupId || !state.memberId || !state.memberToken) { setStatus('Not in a group.'); return; }
  if (!navigator.geolocation) { setStatus('Geolocation not supported.'); return; }
  setStatus('Getting location...');
  navigator.geolocation.getCurrentPosition(async pos => {
    await sendLocation(pos.coords);
  }, err => { setStatus('Failed to get location: ' + err.message); });
});

// Map click to set location and optionally send
map.on('click', async (e) => {
  if (!state.groupId || !state.memberId || !state.memberToken) return;
  const lat = e.latlng.lat;
  const lng = e.latlng.lng;
  const fakeCoords = { latitude: lat, longitude: lng, accuracy: 5 };
  setStatus('Map click: sending location');
  await sendLocation(fakeCoords, true);
});

// Send location to backend
async function sendLocation(coords, fromMap=false) {
  const body = {
    lat: coords.latitude,
    lng: coords.longitude,
    accuracyMeters: coords.accuracy ?? 0,
    recordedAt: new Date().toISOString()
  };
  try {
    const res = await fetch(`${API_BASE}/groups/${state.groupId}/members/${state.memberId}/location`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer ' + state.memberToken
      },
      body: JSON.stringify(body)
    });
    if (res.status === 204) {
      setStatus('Location shared.');
    } else {
      const txt = await res.text();
      setStatus('Failed to share location: ' + res.status + ' ' + txt);
    }
  } catch (err) {
    console.error(err);
    setStatus('Failed to share location.');
  }
}

// Polling loop for group locations
let pollTimer = null;
async function pollOnce() {
  if (!state.groupId) return;
  try {
    const qp = state.version != null ? `?sinceVersion=${state.version}` : '';
    const res = await fetch(`${API_BASE}/groups/${state.groupId}/locations${qp}`);
    if (res.status === 304) return;
    if (!res.ok) throw new Error('Poll failed: ' + res.status);
    const data = await res.json();
    state.version = data.version;
    renderGroupLocations(data);
  } catch (err) {
    console.error('Polling error', err);
  }
}

function pollLoop() {
  if (pollTimer) return; // already running
  pollTimer = setInterval(pollOnce, POLL_INTERVAL_MS);
  pollOnce();
}

// Render group members and polylines
function renderGroupLocations(data) {
  markers.clearLayers();
  polylines.clearLayers();
  const bounds = [];
  data.members.forEach(member => {
    if (member.currentLocation) {
      const latlng = [member.currentLocation.lat, member.currentLocation.lng];
      bounds.push(latlng);
      const m = L.marker(latlng).bindPopup(`${member.displayName}`);
      markers.addLayer(m);

      // Draw polyline for recentHistory + current, if any
      const path = member.recentHistory.map(p => [p.lat, p.lng]);
      if (member.currentLocation) path.push([member.currentLocation.lat, member.currentLocation.lng]);
      if (path.length >= 2) {
        const pl = L.polyline(path, { color: 'blue' });
        polylines.addLayer(pl);
      }
    }
  });
  if (bounds.length) {
    map.fitBounds(bounds, { maxZoom: 16, padding: [40, 40] });
  }
}

// Auto-share via watchPosition
let watchId = null;
autoShare.addEventListener('change', (e) => {
  if (autoShare.checked) {
    if (!navigator.geolocation) { setStatus('Geolocation not supported.'); autoShare.checked = false; return; }
    watchId = navigator.geolocation.watchPosition(async pos => {
      await sendLocation(pos.coords);
    }, err => setStatus('Geolocation watch error: ' + err.message), { maximumAge: 5000 });
  } else {
    if (watchId != null) navigator.geolocation.clearWatch(watchId);
    watchId = null;
  }
});

// On load: if invite param present, prefill join
window.addEventListener('load', () => {
  const url = new URL(location.href);
  const invite = url.searchParams.get('invite');
  if (invite) document.getElementById('invite-code').value = invite;

  // If we already have credentials, start polling
  if (state.groupId && state.memberId && state.memberToken) {
    updateMemberControls();
    pollLoop();
  }
});
