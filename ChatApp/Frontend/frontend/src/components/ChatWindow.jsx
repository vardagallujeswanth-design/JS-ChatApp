import { useState, useRef, useEffect, useCallback } from 'react';
import Avatar from './Avatar.jsx';
import MessageBubble from './MessageBubble.jsx';
import { messagesApi } from '../services/api.js';
import { formatDateLabel } from '../time.js';

export default function ChatWindow({
  selectedChat, messages, setMessages, loadingMessages,
  hasMore, loadMore, typingUsers, onlineUsers,
  sendMessage, sendPoll, votePoll, sendTyping, toggleReaction,
  currentUser,
}) {
  const [input, setInput] = useState('');
  const [replyTo, setReplyTo] = useState(null);
  const [editingMsg, setEditingMsg] = useState(null);
  const [showPollModal, setShowPollModal] = useState(false);
  const [pollQuestion, setPollQuestion] = useState('');
  const [pollOptions, setPollOptions] = useState(['', '']);
  const [pollMultiple, setPollMultiple] = useState(false);
  const [pollExpiresAt, setPollExpiresAt] = useState('');
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragging, setDragging] = useState(false);
  const [isSilent, setIsSilent] = useState(false);
  const [showPinned, setShowPinned] = useState(false);
  const [pinnedMessages, setPinnedMessages] = useState([]);

  const bottomRef = useRef(null);
  const topRef = useRef(null);
  const textareaRef = useRef(null);
  const fileInputRef = useRef(null);
  const typingTimer = useRef(null);
  const isTypingRef = useRef(false);

  // Scroll to bottom on new messages
  useEffect(() => {
    if (!loadingMessages) {
      bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages.length, loadingMessages]);

  // Infinite scroll — load more when scrolled to top
  useEffect(() => {
    const el = topRef.current;
    if (!el) return;
    const obs = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting && hasMore && !loadingMessages) loadMore();
    }, { threshold: 0.1 });
    obs.observe(el);
    return () => obs.disconnect();
  }, [hasMore, loadingMessages, loadMore]);

  // Load pinned messages
  const loadPinned = useCallback(async () => {
    if (!selectedChat) return;
    try {
      const pins = selectedChat.type === 'group'
        ? await messagesApi.getPinnedGroup(selectedChat.id)
        : await messagesApi.getPinnedDirect(selectedChat.id);
      setPinnedMessages(pins);
    } catch (e) { console.error(e); }
  }, [selectedChat]);

  useEffect(() => { setShowPinned(false); setPinnedMessages([]); }, [selectedChat]);

  if (!selectedChat) {
    return (
      <div className="chat-empty">
        <div className="chat-empty-inner">
          <div className="chat-empty-icon">
            <svg width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1">
              <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>
            </svg>
          </div>
          <h2>Welcome to ChatApp</h2>
          <p>Select a conversation or search for a user to start chatting.</p>
        </div>
      </div>
    );
  }

  // ── Typing ────────────────────────────────────────────────────────────────
  const handleInputChange = (e) => {
    setInput(e.target.value);
    if (!isTypingRef.current) {
      isTypingRef.current = true;
      sendTyping(true);
    }
    clearTimeout(typingTimer.current);
    typingTimer.current = setTimeout(() => {
      isTypingRef.current = false;
      sendTyping(false);
    }, 1500);
    // Auto-resize textarea
    e.target.style.height = 'auto';
    e.target.style.height = Math.min(e.target.scrollHeight, 140) + 'px';
  };

  // ── Send ──────────────────────────────────────────────────────────────────
  const handleSend = async () => {
    const text = input.trim();
    if (!text && !editingMsg) return;

    clearTimeout(typingTimer.current);
    isTypingRef.current = false;
    sendTyping(false);

    if (editingMsg) {
      try {
        const updated = await messagesApi.edit(editingMsg.id, text);
        setMessages((prev) => prev.map((m) => (m.id === updated.id ? updated : m)));
      } catch (e) { alert(e.message); }
      setEditingMsg(null);
      setInput('');
      return;
    }

    setInput('');
    if (textareaRef.current) { textareaRef.current.style.height = 'auto'; }
    setReplyTo(null);

    await sendMessage({
      content: text,
      threadParentId: replyTo?.id ?? null,
      isSilent: isChannel ? isSilent : false,
    });
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
    if (e.key === 'Escape') {
      setReplyTo(null);
      setEditingMsg(null);
      setInput('');
    }
  };

  // ── File upload ───────────────────────────────────────────────────────────
  const uploadFile = async (file) => {
    if (!file) return;
    setUploading(true);
    setUploadProgress(0);
    try {
      const res = await messagesApi.uploadFile(file, setUploadProgress);
      await sendMessage({
        content: file.name,
        type: res.type,
        fileUrl: res.fileUrl,
        fileName: res.fileName,
        fileSize: res.fileSize,
        fileMimeType: res.mimeType,
        thumbnailUrl: res.thumbnailUrl,
      });
    } catch (e) {
      alert('Upload failed: ' + e.message);
    } finally {
      setUploading(false);
      setUploadProgress(0);
    }
  };

  const handleFileSelect = (e) => { uploadFile(e.target.files?.[0]); e.target.value = ''; };

  const handleDrop = (e) => {
    e.preventDefault();
    setDragging(false);
    uploadFile(e.dataTransfer.files?.[0]);
  };

  // ── Delete & edit callbacks ───────────────────────────────────────────────
  const handleDelete = async (msgId) => {
    try {
      await messagesApi.delete(msgId);
      setMessages((prev) => prev.map((m) => m.id === msgId ? { ...m, isDeleted: true, content: 'This message was deleted' } : m));
    } catch (e) { alert(e.message); }
  };

  const handleEdit = (msg) => {
    setEditingMsg(msg);
    setInput(msg.content);
    textareaRef.current?.focus();
  };

  // ── Group messages by date ────────────────────────────────────────────────
  const grouped = [];
  let lastDate = null;
  for (const msg of messages) {
    const dateStr = new Date(msg.sentAt).toDateString();
    if (dateStr !== lastDate) {
      grouped.push({ type: 'date', label: formatDateLabel(msg.sentAt), key: `date_${msg.sentAt}` });
      lastDate = dateStr;
    }
    grouped.push({ type: 'msg', msg, key: `msg_${msg.id}` });
  }

  const isOnline = selectedChat.type === 'direct' && onlineUsers.has(selectedChat.id);
  const isChannel = selectedChat.type === 'channel';
  const isGroup = selectedChat.type === 'group';
  const canSend = !isChannel; // channel: only admins — enforced server side

  const typingLabel = typingUsers.length > 0
    ? typingUsers.length === 1
      ? `${typingUsers[0].name} is typing…`
      : `${typingUsers.map((u) => u.name).join(', ')} are typing…`
    : null;

  return (
    <div
      className={`chat-window ${dragging ? 'drag-over' : ''}`}
      onDragOver={(e) => { e.preventDefault(); setDragging(true); }}
      onDragLeave={() => setDragging(false)}
      onDrop={handleDrop}
    >
      {/* ── Header ── */}
      <div className="chat-header">
        <Avatar name={selectedChat.name} avatarUrl={selectedChat.avatarUrl} size={38} online={isOnline} />
        <div className="chat-header-info">
          <div className="chat-header-name">{selectedChat.name}</div>
          <div className="chat-header-sub">
            {typingLabel
              ? <span className="typing-label">{typingLabel}</span>
              : selectedChat.type === 'direct'
                ? <span>{isOnline ? 'Online' : 'Offline'}</span>
                : <span>{isGroup ? 'Group' : 'Channel'}</span>
            }
          </div>
        </div>
        <div className="chat-header-actions">
          {(isGroup || selectedChat.type === 'direct') && (
            <button
              className="icon-btn"
              title="Pinned messages"
              onClick={() => { setShowPinned((v) => !v); if (!showPinned) loadPinned(); }}
            >
              📌
            </button>
          )}
          {isChannel && (
            <label className="silent-toggle" title="Silent message mode (no notifications)">
              <input type="checkbox" checked={isSilent} onChange={(e) => setIsSilent(e.target.checked)} />
              Silent
            </label>
          )}
        </div>
      </div>

      {/* ── Pinned panel ── */}
      {showPinned && (
        <div className="pinned-panel">
          <div className="pinned-panel-title">📌 Pinned messages</div>
          {pinnedMessages.length === 0 && <div className="pinned-empty">No pinned messages</div>}
          {pinnedMessages.map((p) => (
            <div key={p.pinId} className="pinned-item">
              <span className="pinned-content">{p.message.content}</span>
              <span className="pinned-by">Pinned by {p.pinnedByName}</span>
            </div>
          ))}
        </div>
      )}

      {/* ── Messages ── */}
      <div className="messages-area">
        <div ref={topRef} className="scroll-top-sentinel" />
        {loadingMessages && <div className="loading-msgs">Loading messages…</div>}
        {hasMore && !loadingMessages && (
          <button className="load-more-btn" onClick={loadMore}>Load older messages</button>
        )}

        {grouped.map((item) => {
          if (item.type === 'date') {
            return (
              <div key={item.key} className="date-divider">
                <span>{item.label}</span>
              </div>
            );
          }
          const { msg } = item;
          const isMine = msg.senderId === currentUser?.id;
          return (
            <MessageBubble
              key={item.key}
              msg={msg}
              isMine={isMine}
              isGroup={isGroup}
              isChannel={isChannel}
              currentUserId={currentUser?.id}
              onReply={setReplyTo}
              onEdit={handleEdit}
              onDelete={handleDelete}
              onReact={toggleReaction}
              onVote={votePoll}
            />
          );
        })}
        <div ref={bottomRef} />
      </div>

      {/* ── Upload progress ── */}
      {uploading && (
        <div className="upload-progress">
          <div className="upload-bar" style={{ width: `${uploadProgress}%` }} />
          <span>{uploadProgress}% uploading…</span>
        </div>
      )}

      {/* ── Reply bar ── */}
      {replyTo && !editingMsg && (
        <div className="compose-context">
          <div className="compose-context-label">
            <span className="context-icon">↩</span>
            <span>Replying to <strong>{replyTo.senderName}</strong></span>
          </div>
          <div className="compose-context-preview">{replyTo.content}</div>
          <button className="icon-btn" onClick={() => setReplyTo(null)}>✕</button>
        </div>
      )}

      {/* ── Editing bar ── */}
      {editingMsg && (
        <div className="compose-context editing">
          <div className="compose-context-label">
            <span className="context-icon">✏️</span>
            <span>Editing message</span>
          </div>
          <div className="compose-context-preview">{editingMsg.content}</div>
          <button className="icon-btn" onClick={() => { setEditingMsg(null); setInput(''); }}>✕</button>
        </div>
      )}

      {/* ── Drag overlay ── */}
      {showPollModal && (
        <div className="modal-overlay" onClick={() => setShowPollModal(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Create poll</h3>
              <button className="icon-btn" onClick={() => setShowPollModal(false)}>✕</button>
            </div>
            <div className="modal-body">
              <div className="field">
                <label className="field-label">Question</label>
                <input className="field-input" value={pollQuestion} onChange={(e) => setPollQuestion(e.target.value)} placeholder="Ask something…" />
              </div>
              <div className="field">
                <label className="field-label">Options</label>
                {pollOptions.map((opt, idx) => (
                  <div key={idx} style={{ display: 'flex', gap: 6, marginBottom: 8 }}>
                    <input
                      className="field-input"
                      value={opt}
                      onChange={(e) => setPollOptions((prev) => {
                        const next = [...prev];
                        next[idx] = e.target.value;
                        return next;
                      })}
                      placeholder={`Option ${idx + 1}`}
                    />
                    {pollOptions.length > 2 && (
                      <button className="icon-btn" style={{width:24,height:24}} onClick={() => setPollOptions((prev) => prev.filter((_, i) => i !== idx))}>✕</button>
                    )}
                  </div>
                ))}
                <button className="auth-btn" type="button" onClick={() => setPollOptions((prev) => [...prev, ''])}>Add option</button>
              </div>
              <div className="field" style={{ display: 'flex', alignItems: 'center', gap: 10, marginTop: 8 }}>
                <input id="poll-multiple" type="checkbox" checked={pollMultiple} onChange={(e) => setPollMultiple(e.target.checked)} />
                <label htmlFor="poll-multiple" className="field-label">Multiple choice</label>
              </div>
              <div className="field" style={{ marginTop: 8 }}>
                <label className="field-label">Expires at (optional)</label>
                <input className="field-input" type="datetime-local" value={pollExpiresAt} onChange={(e) => setPollExpiresAt(e.target.value)} />
              </div>
              <button
                className="auth-btn"
                onClick={async () => {
                  if (!pollQuestion.trim() || pollOptions.filter((o) => o.trim()).length < 2) {
                    alert('Fill poll question and at least 2 options.');
                    return;
                  }
                  try {
                    await sendPoll({
                      question: pollQuestion.trim(),
                      options: pollOptions.map((o) => o.trim()).filter(Boolean),
                      isMultipleChoice: pollMultiple,
                      expiresAt: pollExpiresAt ? new Date(pollExpiresAt).toISOString() : null,
                    });
                    setShowPollModal(false);
                    setPollQuestion('');
                    setPollOptions(['', '']);
                    setPollMultiple(false);
                    setPollExpiresAt('');
                  } catch (err) {
                    alert(err.message || 'Failed to create poll');
                  }
                }}
              >
                Create Poll
              </button>
            </div>
          </div>
        </div>
      )}

      {dragging && (
        <div className="drag-overlay">
          <div className="drag-overlay-inner">
            <span style={{ fontSize: 40 }}>📎</span>
            <p>Drop file to send</p>
          </div>
        </div>
      )}

      {/* ── Input bar ── */}
      {canSend && (
        <div className="input-bar">
          <button className="icon-btn poll-btn" onClick={() => setShowPollModal(true)} title="Create poll">📊</button>
          <button className="icon-btn attach-btn" onClick={() => fileInputRef.current?.click()} disabled={uploading} title="Attach file">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M21.44 11.05l-9.19 9.19a6 6 0 0 1-8.49-8.49l9.19-9.19a4 4 0 0 1 5.66 5.66l-9.2 9.19a2 2 0 0 1-2.83-2.83l8.49-8.48"/>
            </svg>
          </button>
          <input type="file" ref={fileInputRef} style={{ display: 'none' }} onChange={handleFileSelect} />

          <textarea
            ref={textareaRef}
            className="chat-textarea"
            placeholder={editingMsg ? 'Edit message…' : 'Message…'}
            value={input}
            onChange={handleInputChange}
            onKeyDown={handleKeyDown}
            rows={1}
          />

          <button
            className={`send-btn ${input.trim() ? 'active' : ''}`}
            onClick={handleSend}
            disabled={!input.trim() && !editingMsg}
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <line x1="22" y1="2" x2="11" y2="13"/>
              <polygon points="22 2 15 22 11 13 2 9 22 2"/>
            </svg>
          </button>
        </div>
      )}

      {isChannel && (
        <div className="channel-readonly">
          📢 This is a channel. Only admins can post.
        </div>
      )}
    </div>
  );
}