"use client";
import { useEffect, useRef } from "react";

const BASE_URL = "http://localhost:5238";

interface StreamHandlers {
  onPing: () => void;
  onStep: (title: string) => void;
}

export function useProductStream(productId: string, handlers: StreamHandlers) {
  const handlersRef = useRef(handlers);
  handlersRef.current = handlers;

  useEffect(() => {
    let es: EventSource | null = null;
    let unmounted = false;

    function connect() {
      if (unmounted) return;
      es = new EventSource(`${BASE_URL}/api/products/${productId}/events`);

      es.addEventListener("ping", () => {
        if (!unmounted) handlersRef.current.onPing();
      });

      es.addEventListener("step", (e: Event) => {
        if (!unmounted) handlersRef.current.onStep((e as MessageEvent).data as string);
      });

      // heartbeat events are intentionally ignored — they're keep-alives only

      es.onerror = () => {
        // EventSource auto-reconnects after 3s (browser built-in)
        // No manual action needed; the browser handles reconnection
      };
    }

    connect();

    return () => {
      unmounted = true;
      es?.close();
    };
  }, [productId]);
}
