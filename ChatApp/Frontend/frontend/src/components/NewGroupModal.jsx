import { useState } from 'react';
import { groupsApi, channelsApi, usersApi } from '../services/api.js';
import Avatar from './Avatar.jsx';

export default function NewGroupModal({ type = 'group', onClose, onCreated }) {
  const [step, setStep] = useState(1);
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [search, setSearch] = useState('');
  const [results, setResults] = useState([]);
  const [selected, setSelected] = useState([]);
  const [loading, setLoading] = useState(false);
  const [searching, setSearching] = useState(false);

  const doSearch = async (val) => {
    setSearch(val);
    if (!val.trim()) { setResults([]); return; }
    setSearching(true);
    try { setResults(await usersApi.search(val)); }
    finally { setSearching(false); }
  };

  const toggle = (u) =>
    setSelected((prev) => prev.some((s) => s.id === u.id) ? prev.filter((s) => s.id !== u.id) : [...prev, u]);

  const create = async () => {
    if (!name.trim() || selected.length === 0) return;
    setLoading(true);
    try {
      if (type === 'channel') {
        await channelsApi.create({ name: name.trim(), description: description.trim() || null, memberIds: selected.map((u) => u.id) });
      } else {
        await groupsApi.create({ name: name.trim(), description: description.trim() || null, memberIds: selected.map((u) => u.id) });
      }
      onCreated();
    } catch (e) { alert(`Failed to create ${type}: ${e.message}`); }
    finally { setLoading(false); }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()} style={{ maxWidth: 460 }}>
        <div className="modal-header">
          <h3>{step === 1 ? 'Add participants' : 'Group details'}</h3>
          <button className="icon-btn" onClick={onClose}>✕</button>
        </div>

        {step === 1 ? (
          <div className="modal-body">
            {/* Selected chips */}
            {selected.length > 0 && (
              <div className="chip-list">
                {selected.map((u) => (
                  <span key={u.id} className="chip" onClick={() => toggle(u)}>
                    <Avatar name={u.displayName || u.username} size={20} />
                    {u.displayName || u.username}
                    <span className="chip-x">✕</span>
                  </span>
                ))}
              </div>
            )}
            <input
              className="field-input"
              placeholder="Search users to add…"
              value={search}
              onChange={(e) => doSearch(e.target.value)}
              autoFocus
            />
            <div className="modal-list">
              {searching && <div className="list-hint">Searching…</div>}
              {!searching && search && results.length === 0 && <div className="list-hint">No users found</div>}
              {results.map((u) => {
                const sel = selected.some((s) => s.id === u.id);
                return (
                  <div key={u.id} className={`conv-item ${sel ? 'active' : ''}`} onClick={() => toggle(u)}>
                    <Avatar name={u.displayName || u.username} avatarUrl={u.avatarUrl} size={38} />
                    <div className="conv-body">
                      <span className="conv-name">{u.displayName || u.username}</span>
                      <span className="conv-last">@{u.username}</span>
                    </div>
                    {sel && <span style={{ color: 'var(--accent)', fontSize: 18, marginLeft: 'auto' }}>✓</span>}
                  </div>
                );
              })}
            </div>
            <button
              className="auth-btn"
              style={{ marginTop: 12 }}
              disabled={selected.length === 0}
              onClick={() => setStep(2)}
            >
              Next →
            </button>
          </div>
        ) : (
          <div className="modal-body">
            <div className="chip-list" style={{ marginBottom: 12 }}>
              {selected.map((u) => (
                <span key={u.id} className="chip">
                  <Avatar name={u.displayName || u.username} size={18} />
                  {u.displayName || u.username}
                </span>
              ))}
            </div>
            <div className="field">
              <label className="field-label">Group name *</label>
              <input className="field-input" value={name} onChange={(e) => setName(e.target.value)} placeholder="Team Alpha" autoFocus />
            </div>
            <div className="field" style={{ marginTop: 12 }}>
              <label className="field-label">Description <span className="field-optional">(optional)</span></label>
              <input className="field-input" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="What's this group about?" />
            </div>
            <div style={{ display: 'flex', gap: 8, marginTop: 16 }}>
              <button className="auth-btn" style={{ background: 'var(--bg-hover)', color: 'var(--text-primary)' }} onClick={() => setStep(1)}>← Back</button>
              <button className="auth-btn" disabled={!name.trim() || loading} onClick={create}>
                {loading ? 'Creating…' : 'Create group'}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}