import { createContext, useContext, useEffect, useState } from "react";
import api from "../services/api";

export interface User {
  userId: number;
  userName: string;
  userEmail: string;
  roles: string[];
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
  Roles: string[];
}

const toUser = (me: MeResponse): User => ({
  userId: me.UserId,
  userName: me.UserName,
  userEmail: me.UserEmail,
  roles: me.Roles ?? [],
});

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string, role: string) => Promise<void>;
  logout: () => void;
  loading: boolean;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider = ({ children }: { children: React.ReactNode }) => {
  const [token, setToken] = useState<string | null>(localStorage.getItem("token"));
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchMe = async () => {
      if (!token) {
        setLoading(false);
        return;
      }
      try {
        const res = await api.get<MeResponse>("/auth/me");
        setUser(toUser(res.data));
      } catch {
        localStorage.removeItem("token");
        setToken(null);
        setUser(null);
      } finally {
        setLoading(false);
      }
    };
    fetchMe();
  }, [token]);

  const login = async (username: string, password: string) => {
    const res = await api.post<AuthResponse>("/auth/login", { UserName: username, Password: password });
    localStorage.setItem("token", res.data.Token);
    setToken(res.data.Token);
    const me = await api.get<MeResponse>("/auth/me");
    setUser(toUser(me.data));
  };

  const register = async (username: string, email: string, password: string, role: string) => {
    await api.post("/auth/register", { UserName: username, Email: email, Password: password, Role: role });
  };

  const logout = () => {
    localStorage.removeItem("token");
    setToken(null);
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, token, login, register, logout, loading }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
};