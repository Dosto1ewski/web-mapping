import { useState, useEffect } from 'react';

export interface Settings {
  locationFetchSec: number;
  markerFetchSec: number;
  locationUpdateSec: number;
  showNametags: boolean;
}

const DEFAULTS: Settings = {
  locationFetchSec: 20,
  markerFetchSec: 20,
  locationUpdateSec: 20,
  showNametags: true,
};

const STORAGE_KEY = 'standort_settings';

export function useSettings() {
  const [settings, setSettingsState] = useState<Settings>(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) return { ...DEFAULTS, ...JSON.parse(stored) };
    } catch {}
    return DEFAULTS;
  });

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  }, [settings]);

  const updateSettings = (patch: Partial<Settings>) =>
    setSettingsState((s) => ({ ...s, ...patch }));

  return { settings, updateSettings };
}
