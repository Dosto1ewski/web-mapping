// js/main.js

const map = L.map('map').setView([51.505, -0.09], 13);

L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
  maxZoom: 19,
  attribution: '© OpenStreetMap contributors'
}).addTo(map);

// Sample marker with popup
const marker = L.marker([51.505, -0.09]).addTo(map);
marker.bindPopup('<b>Hello from Leaflet!</b><br>This is a sample marker.').openPopup();
