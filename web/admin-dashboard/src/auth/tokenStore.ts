let accessToken: string | null = null;
let refreshToken: string | null = null;

type Listener = () => void;
const listeners = new Set<Listener>();

export const tokenStore = {
  getAccess: () => accessToken,
  getRefresh: () => refreshToken,
  set: (a: string, r: string) => {
    accessToken = a;
    refreshToken = r;
    listeners.forEach((l) => l());
  },
  clear: () => {
    accessToken = null;
    refreshToken = null;
    listeners.forEach((l) => l());
  },
  subscribe: (listener: Listener) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },
};
