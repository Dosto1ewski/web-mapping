import { useState, useRef, useCallback } from 'react';

export interface Coords {
  latitude: number;
  longitude: number;
  accuracy: number;
}

export function useGeolocation(onPosition: (coords: Coords) => void) {
  const [autoShare, setAutoShareState] = useState(false);
  const watchIdRef = useRef<number | null>(null);

  const getCurrent = useCallback(() => {
    if (!navigator.geolocation) return;
    navigator.geolocation.getCurrentPosition(
      (pos) => onPosition(pos.coords),
      () => {},
      { enableHighAccuracy: true },
    );
  }, [onPosition]);

  const setAutoShare = useCallback(
    (enabled: boolean) => {
      if (enabled) {
        if (!navigator.geolocation) return;
        watchIdRef.current = navigator.geolocation.watchPosition(
          (pos) => onPosition(pos.coords),
          () => {},
          { enableHighAccuracy: true, maximumAge: 5000 },
        );
        setAutoShareState(true);
      } else {
        if (watchIdRef.current != null) {
          navigator.geolocation.clearWatch(watchIdRef.current);
          watchIdRef.current = null;
        }
        setAutoShareState(false);
      }
    },
    [onPosition],
  );

  return { autoShare, setAutoShare, getCurrent };
}
