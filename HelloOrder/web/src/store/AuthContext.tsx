import React, { createContext, useContext, useState, useCallback } from 'react';

export interface UserInfo {
  userId: string;
  username: string;
  realName: string;
  roleCode?: string;
  permissions: string[];
}

interface AuthContextType {
  token: string | null;
  user: UserInfo | null;
  login: (username: string, password: string) => Promise<{ ok: boolean; message?: string }>;
  logout: () => void;
  setUser: (u: UserInfo | null) => void;
}

const TOKEN_KEY = 'helloorder_token';
const USER_KEY = 'helloorder_user';

const AuthContext = createContext<AuthContextType | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [token, setToken] = useState<string | null>(() => localStorage.getItem(TOKEN_KEY));
  const [user, setUserState] = useState<UserInfo | null>(() => {
    const s = localStorage.getItem(USER_KEY);
    if (!s) return null;
    try {
      return JSON.parse(s) as UserInfo;
    } catch {
      return null;
    }
  });

  const setUser = useCallback((u: UserInfo | null) => {
    setUserState(u);
    if (u) localStorage.setItem(USER_KEY, JSON.stringify(u));
    else localStorage.removeItem(USER_KEY);
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    const res = await fetch('/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    const json = await res.json();
    if (json.code !== 0 || !json.data) {
      return { ok: false, message: json.message || '登录失败' };
    }
    const d = json.data;
    setToken(d.token);
    localStorage.setItem(TOKEN_KEY, d.token);
    setUser({
      userId: d.userId,
      username: d.username,
      realName: d.realName,
      roleCode: d.roleCode,
      permissions: d.permissions || []
    });
    return { ok: true };
  }, [setUser]);

  const logout = useCallback(() => {
    setToken(null);
    setUser(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }, [setUser]);

  return (
    <AuthContext.Provider value={{ token, user, login, logout, setUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used within AuthProvider');
  return ctx;
}

export function getStoredToken() {
  return localStorage.getItem(TOKEN_KEY);
}
