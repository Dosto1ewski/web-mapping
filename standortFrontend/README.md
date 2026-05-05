# Frontend (standortfrontend)

Simple static frontend using Leaflet. Files:

- index.html: main HTML file that loads Leaflet and main.js
- main.js: vanilla JS that initializes the map and fetches features from the backend
- styles.css: minimal styling for the map container

To run locally, you can either serve this folder as static files or use npm:

python3 -m http.server 8000

or with npm (requires Node.js):

npm install
npm run start

Then open http://localhost:8000 in your browser.
