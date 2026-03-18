import { getStoredToken } from '../store/AuthContext';

const API_BASE = '/api';

async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<{ code: number; message: string; data: T }> {
  const token = getStoredToken();
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...options.headers as Record<string, string>
  };
  if (token) (headers as Record<string, string>)['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  const json = await res.json();
  if (res.status === 401) {
    localStorage.removeItem('helloorder_token');
    localStorage.removeItem('helloorder_user');
    window.location.href = '/login';
  }
  return json;
}

async function requestForm<T>(path: string, formData: FormData, method = 'POST'): Promise<{ code: number; message: string; data: T }> {
  const token = getStoredToken();
  const headers: HeadersInit = {};
  if (token) (headers as Record<string, string>)['Authorization'] = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { method, body: formData, headers });
  const json = await res.json();
  if (res.status === 401) {
    localStorage.removeItem('helloorder_token');
    localStorage.removeItem('helloorder_user');
    window.location.href = '/login';
  }
  return json;
}

export const api = {
  get: <T>(path: string) => request<T>(path, { method: 'GET' }),
  post: <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: JSON.stringify(body) }),
  postForm: <T>(path: string, formData: FormData) => requestForm<T>(path, formData),
  put: <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' })
};
