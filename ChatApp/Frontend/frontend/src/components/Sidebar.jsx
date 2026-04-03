import { useState, useRef, useCallback, useEffect } from 'react';
import Avatar from './Avatar.jsx';
import NewGroupModal from './NewGroupModal.jsx';
import { useTheme } from '../contexts/ThemeContext.jsx';
import { useAuth } from '../contexts/AuthContext.jsx';
import { usersApi } from '../services/api.js';
import { formatConvTime } from '../time.js';

const ICONS = {
  search: <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>,
  newGroup: <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>,
  sun: <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><circle cx="12" cy="12" r="5"/><path d="M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"/></svg>,
  moon: <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>,
  star: <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polygon points="12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2"/></svg>,
  logout: <svg width="17" height="17" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>,
  group: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>,
  channel: <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07A19.5 19.5 0 0 1 4.69 12 19.79 19.79 0 0 1 1.61 3.42 2 2 0 0 1 3.58 1h3a2 2 0 0 1 2 1.72c.127.96.361 1.903.7 2.81a2 2 0 0 1-.45 2.11L7.91 8.51a16 16 0 0 0 6 6l.87-.87a2 2 0 0 1 2.11-.45c.907.339 1.85.573 2.81.7A2 2 0 0 1 21.5 16z"/></svg>,
};

export default function Sidebar({ conversations, selectedChat, setSelectedChat, onlineUsers, loadConversations }) {
  const { theme, toggleTheme } = useTheme();
  const { user, logout } = useAuth();
  const [search, setSearch] = useState('');
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [showNewGroup, setShowNewGroup] = useState(false);
  const [showNewChannel, setShowNewChannel] = useState(false);
  const [showProfile, setShowProfile] = useState(false);
  const [showMenu, setShowMenu] = useState(false);
  const [filter, setFilter] = useState('all'); // all | direct | groups | channels
  const searchTimer = useRef(null);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!showMenu) return;
    const onClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) {
        setShowMenu(false);
      }
    };
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [showMenu]);

  const handleSearch = useCallback((val) => {
    setSearch(val);
    clearTimeout(searchTimer.current);
    if (!val.trim()) { setSearchResults([]); return; }
    searchTimer.current = setTimeout(async () => {
      setSearching(true);
      try { setSearchResults(await usersApi.search(val)); }
      catch (e) { console.error(e); }
      finally { setSearching(false); }
    }, 350);
  }, []);

  const startDirect = (u) => {
    setSelectedChat({ type: 'direct', id: u.id, name: u.displayName || u.username, avatarUrl: u.avatarUrl });
    setSearch(''); setSearchResults([]);
  };

  const selectConv = (conv) => {
    setSelectedChat({ type: conv.type, id: conv.id, name: conv.name, avatarUrl: conv.avatarUrl });
  };

  const filtered = conversations.filter((c) => {
    if (filter === 'direct') return c.type === 'direct';
    if (filter === 'groups') return c.type === 'group';
    if (filter === 'channels') return c.type === 'channel';
    return true;
  });

  const totalUnread = conversations.reduce((s, c) => s + (c.unreadCount || 0), 0);

  return (
    <aside className="sidebar">
      {/* Header */}
      <div className="sidebar-header">
        <button className="sidebar-avatar-btn" onClick={() => setShowProfile(true)}>
          <Avatar name={user?.displayName || user?.username} avatarUrl={user?.avatarUrl} size={36} online />
          <span className="sidebar-username">{user?.displayName || user?.username}</span>
        </button>
        <div className="sidebar-actions">
          {totalUnread > 0 && <span className="global-unread">{totalUnread > 99 ? '99+' : totalUnread}</span>}
          <button className="icon-btn" onClick={() => setShowNewGroup(true)} title="New group">{ICONS.newGroup}</button>
          <button className="icon-btn" onClick={() => setShowNewChannel(true)} title="New channel">{ICONS.channel}</button>
          <button className="icon-btn" onClick={toggleTheme} title="Toggle theme">{theme === 'dark' ? ICONS.sun : ICONS.moon}</button>
          <button className="icon-btn" onClick={() => setShowMenu((v) => !v)} title="More">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="5" r="1" fill="currentColor"/><circle cx="12" cy="12" r="1" fill="currentColor"/><circle cx="12" cy="19" r="1" fill="currentColor"/>
            </svg>
          </button>
          {showMenu && (
            <div className="dropdown-menu" ref={menuRef}>
              <button className="dropdown-item" onClick={() => { setShowProfile(true); setShowMenu(false); }}>
                {ICONS.star} Profile
              </button>
              <button className="dropdown-item" onClick={() => { setSelectedChat({ type: 'starred', id: -1, name: 'Starred Messages', avatarUrl: null }); setShowMenu(false); }}>
                {ICONS.star} Starred messages
              </button>
              <button className="dropdown-item" onClick={() => { setShowNewGroup(true); setShowMenu(false); }}>
                {ICONS.group} Create group
              </button>
              <button className="dropdown-item" onClick={() => { setShowNewChannel(true); setShowMenu(false); }}>
                {ICONS.channel} Create channel
              </button>
              <div className="dropdown-divider" />
              <button className="dropdown-item danger" onClick={() => { logout(); setShowMenu(false); }}>
                {ICONS.logout} Sign out
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Search */}
      <div className="search-bar">
        <span className="search-icon">{ICONS.search}</span>
        <input
          className="search-input"
          placeholder="Search users or start a chat…"
          value={search}
          onChange={(e) => handleSearch(e.target.value)}
        />
        {search && <button className="search-clear" onClick={() => { setSearch(''); setSearchResults([]); }}>✕</button>}
      </div>

      {/* Filter pills */}
      {!search && (
        <div className="filter-pills">
          {['all', 'direct', 'groups', 'channels'].map((f) => (
            <button key={f} className={`pill ${filter === f ? 'active' : ''}`} onClick={() => setFilter(f)}>
              {f.charAt(0).toUpperCase() + f.slice(1)}
            </button>
          ))}
        </div>
      )}

      {/* Search results */}
      {search && (
        <div className="conv-list">
          {searching && <div className="list-hint">Searching…</div>}
          {!searching && searchResults.length === 0 && <div className="list-hint">No users found for "{search}"</div>}
          {searchResults.map((u) => (
            <div key={u.id} className="conv-item" onClick={() => startDirect(u)}>
              <Avatar name={u.displayName || u.username} avatarUrl={u.avatarUrl} size={46} online={onlineUsers.has(u.id)} />
              <div className="conv-body">
                <div className="conv-top">
                  <span className="conv-name">{u.displayName || u.username}</span>
                </div>
                <div className="conv-bottom">
                  <span className="conv-last">@{u.username} · {u.isOnline ? 'Online' : 'Offline'}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Conversation list */}
      {!search && (
        <div className="conv-list">
          {filtered.length === 0 && (
            <div className="list-hint">
              {filter === 'all' ? 'Search for a user above to start chatting' : `No ${filter} yet`}
            </div>
          )}
          {filtered.map((conv) => {
            const isActive = selectedChat?.type === conv.type && selectedChat?.id === conv.id;
            const isOnline = conv.type === 'direct' && onlineUsers.has(conv.id);
            return (
              <div
                key={`${conv.type}_${conv.id}`}
                className={`conv-item ${isActive ? 'active' : ''}`}
                onClick={() => selectConv(conv)}
              >
                <Avatar name={conv.name} avatarUrl={conv.avatarUrl} size={50} online={isOnline} />
                <div className="conv-body">
                  <div className="conv-top">
                    <span className="conv-name">
                      {conv.type === 'group' && <span className="conv-type-icon">{ICONS.group}</span>}
                      {conv.type === 'channel' && <span className="conv-type-icon">{ICONS.channel}</span>}
                      {conv.name}
                    </span>
                    <span className="conv-time">{formatConvTime(conv.lastMessageAt)}</span>
                  </div>
                  <div className="conv-bottom">
                    <span className="conv-last">
                      {conv.lastMessageSenderName && conv.type !== 'direct' && (
                        <span className="conv-sender">{conv.lastMessageSenderName}: </span>
                      )}
                      {conv.lastMessage || 'No messages yet'}
                    </span>
                    {conv.unreadCount > 0 && (
                      <span className="unread-badge">{conv.unreadCount > 99 ? '99+' : conv.unreadCount}</span>
                    )}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {showNewGroup && (
        <NewGroupModal
          type="group"
          onClose={() => setShowNewGroup(false)}
          onCreated={() => { loadConversations(); setShowNewGroup(false); }}
        />
      )}

      {showNewChannel && (
        <NewGroupModal
          type="channel"
          onClose={() => setShowNewChannel(false)}
          onCreated={() => { loadConversations(); setShowNewChannel(false); }}
        />
      )}

      {showProfile && (
        <ProfileModal user={user} onClose={() => setShowProfile(false)} />
      )}
    </aside>
  );
}

function ProfileModal({ user, onClose }) {
  const { updateUser } = useAuth();
  const [form, setForm] = useState({ displayName: user?.displayName || '', about: user?.about || '' });
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');

  const save = async () => {
    setSaving(true);
    try {
      const updated = await usersApi.updateProfile(form);
      updateUser(updated);
      setMsg('Saved!');
      setTimeout(() => setMsg(''), 2000);
    } catch (e) {
      setMsg(e.message);
    } finally { setSaving(false); }
  };

  const uploadAvatar = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const res = await usersApi.uploadAvatar(file);
      updateUser({ avatarUrl: res.avatarUrl });
    } catch (e) { alert(e.message); }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>Profile</h3>
          <button className="icon-btn" onClick={onClose}>✕</button>
        </div>
        <div className="modal-body">
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12, marginBottom: 20 }}>
            <label style={{ cursor: 'pointer', position: 'relative' }}>
              <Avatar name={user?.displayName || user?.username} avatarUrl={user?.avatarUrl} size={80} />
              <div style={{ position: 'absolute', bottom: 0, right: 0, background: 'var(--accent)', borderRadius: '50%', width: 24, height: 24, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: 14 }}>+</div>
              <input type="file" accept="image/*" style={{ display: 'none' }} onChange={uploadAvatar} />
            </label>
            <span style={{ fontSize: 13, color: 'var(--text-secondary)' }}>@{user?.username}</span>
          </div>
          <div className="field">
            <label className="field-label">Display name</label>
            <input className="field-input" value={form.displayName} onChange={(e) => setForm((p) => ({ ...p, displayName: e.target.value }))} />
          </div>
          <div className="field" style={{ marginTop: 12 }}>
            <label className="field-label">About</label>
            <input className="field-input" value={form.about} onChange={(e) => setForm((p) => ({ ...p, about: e.target.value }))} />
          </div>
          {msg && <div style={{ marginTop: 10, fontSize: 13, color: msg === 'Saved!' ? 'var(--accent)' : 'var(--danger)', textAlign: 'center' }}>{msg}</div>}
          <button className="auth-btn" style={{ marginTop: 16 }} onClick={save} disabled={saving}>
            {saving ? 'Saving…' : 'Save changes'}
          </button>
        </div>
      </div>
    </div>
  );
}