import { useState, useRef, useCallback, useEffect } from 'react';

export interface Coords {
  latitude: number;
  longitude: number;
  accuracy: number;
}

export function useGeolocation(onPosition: (coords: Coords) => void, autoShareIntervalMs = 5000) {
  const [autoShare, setAutoShareState] = useState(false);
  const intervalIdRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const autoShareRef = useRef(false);
  const onPositionRef = useRef(onPosition);
  useEffect(() => {
    onPositionRef.current = onPosition;
  }, [onPosition]);

  const getCurrent = useCallback(() => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => onPositionRef.current(pos.coords),
      () => {},
      { enableHighAccuracy: true },
    );
  }, []);

  const startInterval = useCallback(() => {
    if (intervalIdRef.current != null) clearInterval(intervalIdRef.current);
    if (!navigator.geolocation) return;
    getCurrent();
    intervalIdRef.current = setInterval(getCurrent, autoShareIntervalMs);
  }, [getCurrent, autoShareIntervalMs]);

  // Restart when interval changes while auto-share is active
  useEffect(() => {
    if (autoShareRef.current) startInterval();
  }, [startInterval]);

  const setAutoShare = useCallback(
    (enabled: boolean) => {
      autoShareRef.current = enabled;
      setAutoShareState(enabled);
      if (enabled) {
        startInterval();
      } else {
        if (intervalIdRef.current != null) {
          clearInterval(intervalIdRef.current);
          intervalIdRef.current = null;
        }
      }
    },
    [startInterval],
  );

  return { autoShare, setAutoShare, getCurrent };
}
