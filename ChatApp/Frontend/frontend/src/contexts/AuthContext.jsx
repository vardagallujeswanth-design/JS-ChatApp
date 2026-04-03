import { createContext, useContext, useState, useEffect } from 'react';
import { authApi, usersApi } from '../services/api.js';
import { stopConnection } from '../services/signalr.js';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const logout = async () => {
    const rt = localStorage.getItem('refreshToken');
    if (rt) authApi.logout(rt).catch(() => {});
    await stopConnection();
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    setUser(null);
  };

  useEffect(() => {
    const initAuth = async () => {
      const token = localStorage.getItem('accessToken');
      if (token) {
        try {
          const userData = await usersApi.getMe();
          setUser(userData);
        } catch {
          localStorage.removeItem('accessToken');
          localStorage.removeItem('refreshToken');
        }
      }
      setLoading(false);
    };

    initAuth();

    // Listen for forced logout (token expired + refresh failed)
    const onLogout = () => logout();
    window.addEventListener('auth:logout', onLogout);
    return () => window.removeEventListener('auth:logout', onLogout);
  }, []);

  const login = async (phoneNumber, password) => {
    const data = await authApi.login({ phoneNumber, password });
    if (data.twoFactorRequired) {
      return { twoFactorRequired: true };
    }
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    setUser(data.user);
    return data.user;
  };

  const loginTwoFactor = async (phoneNumber, password, code) => {
    const data = await authApi.loginTwoFactor({ phoneNumber, password, code });
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    setUser(data.user);
    return data.user;
  };

  const register = async (username, phoneNumber, password, displayName) => {
    const data = await authApi.register({ username, phoneNumber, password, displayName });
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    setUser(data.user);
    return data.user;
  };

  const updateUser = (updates) => setUser((prev) => ({ ...prev, ...updates }));

  return (
    <AuthContext.Provider value={{ user, loading, login, register, logout, updateUser }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export const useAuth = () => useContext(AuthContext);