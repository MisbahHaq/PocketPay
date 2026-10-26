let accessToken: string | null = null;
let refreshToken: string | null = null;

export const tokenStore = {
  getAccess: () => accessToken,
  getRefresh: () => refreshToken,
  set: (a: string, r: string) => { accessToken = a; refreshToken = r; },
  clear: () => { accessToken = null; refreshToken = null; }
};
