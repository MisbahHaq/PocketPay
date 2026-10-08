import { createContext, useCallback, useContext, useEffect, useState } from "react";
import api from "../services/api";

export interface OrgInfo {
  organizationId: number;
  name: string;
}

export interface User {
  userId: number;
  userName: string;
  userEmail: string;
  status: string;
  organization: OrgInfo | null;
  roles: string[];
  permissions: string[];
}

export interface AuthResponse {
  Token: string;
  Username: string;
  Email: string;
  ExpiresIn: string;
  Roles: string[];
}

interface MeResponse {
  UserId: number;
  UserName: string;
  UserEmail: string;
  Status: string;
  Organization: { OrganizationId: number; Name: string } | null;
  Roles: string[];
  Permissions: string[];
}

const toUser = (me: MeResponse): User => ({
  userId: me.UserId,
  userName: me.UserName,
  userEmail: me.UserEmail,
  status: me.Status,
  organization: me.Organization
    ? { organizationId: me.Organization.OrganizationId, name: me.Organization.Name }
    : null,
  roles: me.Roles ?? [],
  permissions: me.Permissions ?? [],
});

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string) => Promise<void>;
  adoptSession: (token: string) => Promise<void>;
  refreshMe: () => Promise<void>;
  has: (permission: string) => boolean;
  logout: () => void;
  loading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const [token, setToken] = useState<string | null>(localStorage.getItem("token"));
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  const fetchMe = useCallback(async () => {
    const res = await api.get<MeResponse>("/auth/me");
    setUser(toUser(res.data));
  }, []);

  useEffect(() => {
    const load = async () => {
      if (!token) {
        setUser(null);
        setLoading(false);
        return;
      }
      try {
        await fetchMe();
      } catch {
        localStorage.removeItem("token");
        setToken(null);
        setUser(null);
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [token, fetchMe]);

  const login = async (username: string, password: string) => {
    const res = await api.post<AuthResponse>("/auth/login", {
      UserName: username,
      Password: password,
    });
    localStorage.setItem("token", res.data.Token);
    setToken(res.data.Token);
    await fetchMe();
  };

  const register = async (username: string, email: string, password: string) => {
    await api.post("/auth/register", {
      UserName: username,
      Email: email,
      Password: password,
    });
    await login(username, password);
  };

  //store a token returned by the invitation-accept flow
  const adoptSession = async (newToken: string) => {
    localStorage.setItem("token", newToken);
    setToken(newToken);
    await fetchMe();
  };

  const refreshMe = async () => {
    await fetchMe();
  };

  const logout = () => {
    localStorage.removeItem("token");
    setToken(null);
    setUser(null);
  };

  const has = (permission: string) => user?.permissions.includes(permission) ?? false;

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        login,
        register,
        adoptSession,
        refreshMe,
        has,
        logout,
        loading,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
};
