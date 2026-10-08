import { createContext, useContext, useEffect, useState } from "react";
import api from "../services/api";

export interface User {
  userId: number;
  userName: string;
  userEmail: string;
}

export interface AuthResponse {
  token: string;
  username: string;
  email: string;
  expiresIn: string;
  roles: string[];
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, email: string, password: string) => Promise<void>;
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
        const res = await api.get<User>("/auth/me");
        setUser(res.data);
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
    localStorage.setItem("token", res.data.token);
    setToken(res.data.token);
    const me = await api.get<User>("/auth/me");
    setUser(me.data);
  };

  const register = async (username: string, email: string, password: string) => {
    await api.post("/auth/register", { UserName: username, Email: email, Password: password });
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