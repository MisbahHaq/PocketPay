import { useRef, useState } from "react";
export function useIdempotencyKey() {
  const keyRef = useRef<string | null>(null);
  const [key, setKey] = useState<string | null>(null);
  const ensure = () => { if (!keyRef.current) { keyRef.current = crypto.randomUUID(); setKey(keyRef.current); } return keyRef.current; };
  const reset = () => { keyRef.current = null; setKey(null); };
  return { key, ensure, reset };
}
