import { useEffect, useRef } from 'react';

export function useSSE<T>(url: string, onMessage: (data: T) => void) {
  const eventSourceRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const es = new EventSource(url);
    eventSourceRef.current = es;
    es.onmessage = (event) => {
      try {
        onMessage(JSON.parse(event.data));
      } catch {
        // ignore parse errors
      }
    };
    es.onerror = () => es.close();
    return () => es.close();
  }, [url, onMessage]);
}
