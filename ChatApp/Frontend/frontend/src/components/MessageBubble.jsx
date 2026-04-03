import { useState, useRef } from 'react';
import Avatar from './Avatar.jsx';
import { messagesApi } from '../services/api.js';
import { formatTime, formatFileSize } from '../time.js';

const EMOJI_CATEGORIES = [
  { name: 'Smileys', items: ['😀','😃','😄','😁','😆','😅','🤣','😂','🙂','🙃','😉','😊','😇','🥰','😍','🤩','😘','😗','😚','😙','😋','😛','😝','😜','🤪'] },
  { name: 'Gestures', items: ['👍','👎','👌','✌️','🤞','🤟','🤘','👋','👏','👐','🤲','🙏','✋','🤚','👊','✊','🤛','🤜'] },
  { name: 'Symbols', items: ['❤️','🧡','💛','💚','💙','💜','🖤','💥','✨','⭐','🔥','💧','🎶','☀️','🌙','❄️','⚡','🎯'] },
];

export default function MessageBubble({
  msg, isMine, isGroup, isChannel,
  onReply, onEdit, onDelete,
  onReact, onVote, currentUserId,
}) {
  const [showActions, setShowActions] = useState(false);
  const [_showEmojiPicker, setShowEmojiPicker] = useState(false);
  const [emojiSearch, setEmojiSearch] = useState('');
  const [showHistory, setShowHistory] = useState(false);
  const [history, setHistory] = useState([]);
  const [viewerSrc, setViewerSrc] = useState(null);
  const [viewerZoom, setViewerZoom] = useState(1);
  const hoverTimer = useRef(null);

  const adjustZoom = (delta) => {
    setViewerZoom((current) => {
      const next = Math.min(4, Math.max(0.5, Number((current + delta).toFixed(2))));
      return next;
    });
  };

  const handleWheelZoom = (e) => {
    if (!viewerSrc) return;
    if (e.ctrlKey || Math.abs(e.deltaY) > 0) {
      e.preventDefault();
      adjustZoom(e.deltaY > 0 ? -0.1 : 0.1);
    }
  };

  const filteredCategories = EMOJI_CATEGORIES
    .map((cat) => ({
      name: cat.name,
      items: cat.items.filter((emoji) => emoji.includes(emojiSearch)),
    }))
    .filter((cat) => cat.items.length > 0);

  if (msg.isDeleted) {
    return (
      <div className={`msg-row ${isMine ? 'mine' : 'theirs'}`}>
        <div className="bubble deleted">
          <span className="deleted-text">🚫 This message was deleted</span>
        </div>
      </div>
    );
  }

  const loadHistory = async () => {
    try {
      const h = await messagesApi.getHistory(msg.id);
      setHistory(h);
      setShowHistory(true);
    } catch (e) { console.error(e); }
  };

  const handleReact = (emoji) => {
    onReact(msg.id, emoji);
    setShowEmojiPicker(false);
    setShowActions(false);
  };

  const handleDelete = async () => {
    if (!confirm('Delete this message?')) return;
    await onDelete(msg.id);
    setShowActions(false);
  };

  const handleEdit = () => { onEdit(msg); setShowActions(false); };
  const handleReply = () => { onReply(msg); setShowActions(false); };

  const handlePin = async () => {
    try {
      if (msg.isPinned) await messagesApi.unpin(msg.id);
      else await messagesApi.pin(msg.id);
    } catch (e) { alert(e.message); }
    setShowActions(false);
  };

  const handleStar = async () => {
    try { await messagesApi.star(msg.id); }
    catch (e) { alert(e.message); }
    setShowActions(false);
  };

  const ticks = isMine
    ? msg.readBy?.some((id) => id !== String(currentUserId)) ? '✓✓' : '✓'
    : null;

  return (
    <div
      className={`msg-row ${isMine ? 'mine' : 'theirs'}`}
      onMouseEnter={() => { hoverTimer.current = setTimeout(() => setShowActions(true), 200); }}
      onMouseLeave={() => { clearTimeout(hoverTimer.current); setShowActions(false); setShowEmojiPicker(false); }}
    >
      {/* Avatar for group/channel */}
      {!isMine && (isGroup || isChannel) && (
        <Avatar name={msg.senderName} avatarUrl={msg.senderAvatar} size={28} />
      )}

      <div className={`bubble ${isMine ? 'mine' : 'theirs'} ${msg.isPinned ? 'pinned' : ''}`}>
        {/* Sender name in group */}
        {!isMine && (isGroup || isChannel) && (
          <div className="bubble-sender">{msg.senderName}</div>
        )}

        {/* Reply preview */}
        {msg.threadParentId && msg.replyContent && (
          <div className="reply-preview">
            <span className="reply-preview-name">{msg.replySenderName || 'Reply'}</span>
            <span className="reply-preview-text">{msg.replyContent}</span>
          </div>
        )}

        {/* Content by type */}
        {msg.type === 'Image' && msg.fileUrl && (
          <img
            className="bubble-image"
            src={`http://localhost:5215${msg.fileUrl}`}
            alt={msg.fileName || 'Image'}
            onClick={() => {
              setViewerSrc(`http://localhost:5215${msg.fileUrl}`);
              setViewerZoom(1);
            }}
          />
        )}

        {viewerSrc && (
          <div className="image-modal-overlay" onClick={() => setViewerSrc(null)}>
            <div className="image-modal" onClick={(e) => e.stopPropagation()} onWheel={handleWheelZoom}>
              <button className="image-modal-close" onClick={() => setViewerSrc(null)} aria-label="Close">✕</button>
              <div className="image-modal-toolbar">
                <button onClick={() => adjustZoom(-0.1)}>-</button>
                <button onClick={() => setViewerZoom(1)}>Reset</button>
                <button onClick={() => adjustZoom(0.1)}>+</button>
              </div>
              <div className="image-modal-content">
                <img
                  className="image-modal-img"
                  src={viewerSrc}
                  alt="Preview"
                  style={{ width: `${viewerZoom * 100}%`, height: 'auto' }}
                />
              </div>
            </div>
          </div>
        )}

        {msg.type === 'Video' && msg.fileUrl && (
          <video className="bubble-video" controls preload="metadata">
            <source src={`http://localhost:5215${msg.fileUrl}`} type={msg.fileMimeType || 'video/mp4'} />
          </video>
        )}

        {msg.type === 'Audio' && msg.fileUrl && (
          <audio className="bubble-audio" controls>
            <source src={`http://localhost:5215${msg.fileUrl}`} />
          </audio>
        )}

        {(msg.type === 'File' || msg.type === 'Document') && msg.fileUrl && (
          <a
            className="bubble-file"
            href={`http://localhost:5215${msg.fileUrl}`}
            target="_blank"
            rel="noreferrer"
            download={msg.fileName}
          >
            <span className="file-icon">📄</span>
            <div className="file-info">
              <span className="file-name">{msg.fileName}</span>
              <span className="file-size">{formatFileSize(msg.fileSize)}</span>
            </div>
            <span className="file-dl">↓</span>
          </a>
        )}

        {/* Poll */}
        {msg.type === 'Poll' && msg.poll && (
          <div className="poll-card">
            <div className="poll-question">{msg.poll.question}</div>
            {msg.poll.options.map((opt) => {
              const total = msg.poll.options.reduce((sum, o) => sum + o.voteCount, 0);
              const percent = total > 0 ? Math.round((opt.voteCount / total) * 100) : 0;
              const isVoted = opt.votedByMe;
              return (
                <div key={opt.id} className="poll-option">
                  <button
                    className={`poll-option-btn ${isVoted ? 'active' : ''}`}
                    disabled={Boolean(msg.poll.expiresAt && new Date(msg.poll.expiresAt) < new Date())}
                    onClick={() => onVote(opt.id)}
                  >
                    <span>{opt.text}</span>
                    <span>{opt.voteCount} vote{opt.voteCount === 1 ? '' : 's'}</span>
                  </button>
                  <div className="poll-bar-bg">
                    <div className="poll-bar-fill" style={{ width: `${percent}%` }} />
                  </div>
                  <div className="poll-percent">{percent}%</div>
                  {msg.poll.isCreator && opt.voterNames?.length > 0 && (
                    <div className="poll-voters">Voters: {opt.voterNames.join(', ')}</div>
                  )}
                </div>
              );
            })}
            {msg.poll.expiresAt && (
              <div className="poll-expiry">Expires: {new Date(msg.poll.expiresAt).toLocaleString()}</div>
            )}
          </div>
        )}

        {/* Text */}
        {msg.content && (
          <div className="bubble-text">{msg.content}</div>
        )}

        {/* Meta row */}
        <div className="bubble-meta">
          {msg.isEdited && <span className="edited-tag">edited</span>}
          {msg.isPinned && <span className="pin-tag">📌</span>}
          {msg.isSilent && <span className="silent-tag">🔕 Silent</span>}
          {msg.expiresAt && <span className="expiry-tag">⏱</span>}
          <span className="bubble-time">{formatTime(msg.sentAt)}</span>
          {isMine && <span className={`bubble-ticks ${ticks === '✓✓' ? 'read' : ''}`}>{ticks}</span>}
        </div>

        {/* Reactions display */}
        {msg.reactions?.length > 0 && (
          <div className="reaction-bar">
            {msg.reactions.map((r) => (
              <button
                key={r.emoji}
                className={`reaction-chip ${r.reactedByMe ? 'mine' : ''}`}
                onClick={() => handleReact(r.emoji)}
              >
                {r.emoji} {r.count}
              </button>
            ))}
          </div>
        )}

        {/* Action bar */}
        {showActions && (
          <div className={`action-bar ${isMine ? 'left' : 'right'}`}>
            <button className="action-btn" onClick={() => setShowEmojiPicker((v) => !v)} title="React">😀</button>
            {_showEmojiPicker && (
              <div className="emoji-picker" onMouseLeave={() => setShowEmojiPicker(false)}>
                <input
                  className="emoji-search"
                  value={emojiSearch}
                  onChange={(e) => setEmojiSearch(e.target.value)}
                  placeholder="Search emojis..."
                />
                <div className="emoji-categories">
                  {filteredCategories.map((cat) => (
                    <div key={cat.name} className="emoji-category">
                      <div className="emoji-category-name">{cat.name}</div>
                      <div className="emoji-grid">
                        {cat.items.map((em) => (
                          <button key={em} className="emoji-btn" onClick={() => handleReact(em)}>{em}</button>
                        ))}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
            <button className="action-btn" onClick={handleReply} title="Reply">↩</button>
            {isMine && <button className="action-btn" onClick={handleEdit} title="Edit">✏️</button>}
            {msg.isEdited && <button className="action-btn" onClick={loadHistory} title="Edit history">📋</button>}
            <button className="action-btn" onClick={handlePin} title={msg.isPinned ? 'Unpin' : 'Pin'}>{msg.isPinned ? '📌' : '📍'}</button>
            <button className="action-btn" onClick={handleStar} title={msg.isStarred ? 'Unstar' : 'Star'}>{msg.isStarred ? '⭐' : '☆'}</button>
            {isMine && <button className="action-btn danger" onClick={handleDelete} title="Delete">🗑</button>}
          </div>
        )}
      </div>

      {/* Thread count */}
      {msg.threadReplyCount > 0 && (
        <button className="thread-btn" onClick={() => onReply(msg)}>
          💬 {msg.threadReplyCount} {msg.threadReplyCount === 1 ? 'reply' : 'replies'}
        </button>
      )}

      {/* Edit history modal */}
      {showHistory && (
        <div className="modal-overlay" onClick={() => setShowHistory(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <div className="modal-header">
              <h3>Edit history</h3>
              <button className="icon-btn" onClick={() => setShowHistory(false)}>✕</button>
            </div>
            <div className="modal-body">
              {history.length === 0 && <p style={{ color: 'var(--text-secondary)', fontSize: 14 }}>No history found.</p>}
              {history.map((h) => (
                <div key={h.id} className="history-entry">
                  <span className="history-time">{new Date(h.editedAt).toLocaleString()}</span>
                  <p className="history-content">{h.previousContent}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}