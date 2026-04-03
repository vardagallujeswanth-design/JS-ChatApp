const BASE = 'http://localhost:5215/api';

function token() { return localStorage.getItem('accessToken') || ''; }

async function req(path, opts = {}) {
  const res = await fetch(`${BASE}${path}`, {
    ...opts,
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token()}`,
      ...(opts.headers || {}),
    },
  });

  // Auto-refresh on 401
  if (res.status === 401) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      return req(path, opts); // retry once
    }
    window.dispatchEvent(new Event('auth:logout'));
    throw new Error('Session expired');
  }

  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Request failed' }));
    throw new Error(err.message || 'Request failed');
  }
  return res.json();
}

async function tryRefresh() {
  const rt = localStorage.getItem('refreshToken');
  if (!rt) return false;
  try {
    const res = await fetch(`${BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: rt }),
    });
    if (!res.ok) return false;
    const data = await res.json();
    localStorage.setItem('accessToken', data.accessToken);
    localStorage.setItem('refreshToken', data.refreshToken);
    return true;
  } catch {
    return false;
  }
}

// ── Auth ──────────────────────────────────────────────────────────────────────
export const authApi = {
  register: (data) => req('/auth/register', { method: 'POST', body: JSON.stringify(data) }),
  login: (data) => req('/auth/login', { method: 'POST', body: JSON.stringify(data) }),
  refresh: (refreshToken) => req('/auth/refresh', { method: 'POST', body: JSON.stringify({ refreshToken }) }),
  logout: (refreshToken) => req('/auth/logout', { method: 'POST', body: JSON.stringify({ refreshToken }) }),
  loginTwoFactor: (data) => req('/auth/login-2fa', { method: 'POST', body: JSON.stringify(data) }),
};

// ── Users ─────────────────────────────────────────────────────────────────────
export const usersApi = {
  getMe: () => req('/users/me'),
  updateProfile: (data) => req('/users/me', { method: 'PUT', body: JSON.stringify(data) }),
  search: (q) => req(`/users/search?q=${encodeURIComponent(q)}`),
  getUser: (id) => req(`/users/${id}`),
  getConversations: () => req('/users/conversations'),
  getStarred: () => req('/users/me/starred'),
  uploadAvatar: (file) => {
    const form = new FormData();
    form.append('file', file);
    return fetch(`${BASE}/users/me/avatar`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token()}` },
      body: form,
    }).then((r) => r.json());
  },
};

// ── Messages ──────────────────────────────────────────────────────────────────
export const messagesApi = {
  getDirect: (otherId, page = 1) => req(`/messages/direct/${otherId}?page=${page}`),
  getGroup: (groupId, page = 1) => req(`/messages/group/${groupId}?page=${page}`),
  getChannel: (channelId, page = 1) => req(`/messages/channel/${channelId}?page=${page}`),
  getThread: (msgId) => req(`/messages/${msgId}/thread`),
  edit: (id, newContent) => req(`/messages/${id}`, { method: 'PUT', body: JSON.stringify({ newContent }) }),
  delete: (id) => req(`/messages/${id}`, { method: 'DELETE' }),
  getHistory: (id) => req(`/messages/${id}/history`),
  react: (id, emoji) => req(`/messages/${id}/react`, { method: 'POST', body: JSON.stringify({ emoji }) }),
  pin: (id) => req(`/messages/${id}/pin`, { method: 'POST' }),
  unpin: (id) => req(`/messages/${id}/pin`, { method: 'DELETE' }),
  star: (id) => req(`/messages/${id}/star`, { method: 'POST' }),
  getPinnedDirect: (otherId) => req(`/messages/pinned/direct/${otherId}`),
  getPinnedGroup: (groupId) => req(`/messages/pinned/group/${groupId}`),
  uploadFile: (file, onProgress) => {
    const form = new FormData();
    form.append('file', file);
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      xhr.open('POST', `${BASE}/messages/upload`);
      xhr.setRequestHeader('Authorization', `Bearer ${token()}`);
      xhr.upload.onprogress = (e) => { if (e.lengthComputable && onProgress) onProgress(Math.round((e.loaded / e.total) * 100)); };
      xhr.onload = () => resolve(JSON.parse(xhr.responseText));
      xhr.onerror = () => reject(new Error('Upload failed'));
      xhr.send(form);
    });
  },
};

// ── Groups ────────────────────────────────────────────────────────────────────
export const groupsApi = {
  create: (data) => req('/groups', { method: 'POST', body: JSON.stringify(data) }),
  get: (id) => req(`/groups/${id}`),
  update: (id, data) => req(`/groups/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  addMember: (groupId, userId) => req(`/groups/${groupId}/members`, { method: 'POST', body: JSON.stringify(userId) }),
  removeMember: (groupId, userId) => req(`/groups/${groupId}/members/${userId}`, { method: 'DELETE' }),
  setRole: (groupId, userId, role) => req(`/groups/${groupId}/members/${userId}/role`, { method: 'PUT', body: JSON.stringify(role) }),
};

// ── Channels ──────────────────────────────────────────────────────────────────
export const channelsApi = {
  search: (q) => req(`/channels?q=${encodeURIComponent(q || '')}`),
  create: (data) => req('/channels', { method: 'POST', body: JSON.stringify(data) }),
  get: (id) => req(`/channels/${id}`),
  subscribe: (id) => req(`/channels/${id}/subscribe`, { method: 'POST' }),
  unsubscribe: (id) => req(`/channels/${id}/subscribe`, { method: 'DELETE' }),
};