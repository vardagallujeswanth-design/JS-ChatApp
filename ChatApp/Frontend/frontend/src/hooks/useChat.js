import { useState, useEffect, useCallback, useRef } from 'react';
import { startConnection } from '../services/signalr.js';
import { messagesApi, usersApi } from '../services/api.js';

export function useChat(user) {
  const [conversations, setConversations] = useState([]);
  const [selectedChat, setSelectedChat] = useState(null);
  const [messages, setMessages] = useState([]);
  const [typingUsers, setTypingUsers] = useState([]);
  const [onlineUsers, setOnlineUsers] = useState(new Set());
  const [loadingMessages, setLoadingMessages] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [page, setPage] = useState(1);
  const connRef = useRef(null);
  const typingTimers = useRef({});
  const selectedRef = useRef(null);
  selectedRef.current = selectedChat;

  const loadConversations = useCallback(async () => {
    try {
      const data = await usersApi.getConversations();
      setConversations(data);
      const online = new Set(
        data.filter((c) => c.type === 'direct' && c.isOnline).map((c) => c.id)
      );
      setOnlineUsers(online);
    } catch (err) {
      console.error('loadConversations:', err);
    }
  }, []);

  // ── SignalR setup ──────────────────────────────────────────────────────────
  useEffect(() => {
    if (!user) return;
    let mounted = true;

    startConnection()
      .then((conn) => {
        if (!mounted) return;
        connRef.current = conn;

        // New message arrives
        conn.on('ReceiveMessage', (msg) => {
          setMessages((prev) => {
            if (prev.some((m) => m.id === msg.id)) return prev;
            return [...prev, msg];
          });

          // Update conversation preview
          setConversations((prev) => {
            const isGroup = !!msg.groupId;
            const isChannel = !!msg.channelId;
            const chatId = isGroup ? msg.groupId : isChannel ? msg.channelId : (msg.senderId === user.id ? msg.receiverId : msg.senderId);
            const chatType = isGroup ? 'group' : isChannel ? 'channel' : 'direct';

            const sel = selectedRef.current;
            const isSelected = sel?.type === chatType && sel?.id === chatId;

            const updated = prev.map((c) => {
              if (c.type === chatType && c.id === chatId) {
                return {
                  ...c,
                  lastMessage: msg.isDeleted ? 'Message deleted' : msg.content,
                  lastMessageSenderName: msg.senderName,
                  lastMessageAt: msg.sentAt,
                  unreadCount: (!isSelected && msg.senderId !== user.id) ? (c.unreadCount || 0) + 1 : 0,
                };
              }
              return c;
            });

            const found = updated.some((c) => c.type === chatType && c.id === chatId);
            if (!found) {
              // New conversation — reload list
              setTimeout(() => loadConversations(), 300);
            }

            return updated.sort((a, b) => new Date(b.lastMessageAt || 0) - new Date(a.lastMessageAt || 0));
          });
        });

        // Message was edited
        conn.on('MessageEdited', (msg) => {
          setMessages((prev) => prev.map((m) => (m.id === msg.id ? msg : m)));
        });

        // Message was deleted
        conn.on('MessageDeleted', ({ messageId }) => {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === messageId ? { ...m, isDeleted: true, content: 'This message was deleted' } : m
            )
          );
        });

        // Reactions updated
        conn.on('ReactionsUpdated', ({ messageId, reactions }) => {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === messageId
                ? {
                    ...m,
                    reactions: reactions.map((r) => ({
                      emoji: r.emoji,
                      count: r.count,
                      reactedByMe: false, // will be recalculated on next load
                    })),
                  }
                : m
            )
          );
        });

        // Poll updates
        conn.on('PollUpdated', ({ messageId, poll }) => {
          setMessages((prev) =>
            prev.map((m) =>
              m.id === messageId
                ? {
                    ...m,
                    poll,
                  }
                : m
            )
          );
        });

        // Typing
        conn.on('UserTyping', ({ userId, name }) => {
          setTypingUsers((prev) =>
            prev.some((u) => u.userId === userId) ? prev : [...prev, { userId, name }]
          );
          clearTimeout(typingTimers.current[userId]);
          typingTimers.current[userId] = setTimeout(() => {
            setTypingUsers((prev) => prev.filter((u) => u.userId !== userId));
          }, 3000);
        });

        conn.on('UserStopTyping', (userId) => {
          clearTimeout(typingTimers.current[userId]);
          setTypingUsers((prev) => prev.filter((u) => u.userId !== userId));
        });

        // Presence
        conn.on('UserPresence', ({ userId, online }) => {
          setOnlineUsers((prev) => {
            const next = new Set(prev);
            online ? next.add(userId) : next.delete(userId);
            return next;
          });
          setConversations((prev) =>
            prev.map((c) =>
              c.type === 'direct' && c.id === userId ? { ...c, isOnline: online } : c
            )
          );
        });

        // Read receipts
        conn.on('MessagesRead', (byUserId) => {
          setMessages((prev) =>
            prev.map((m) => ({
              ...m,
              readBy: m.readBy?.includes(String(byUserId))
                ? m.readBy
                : [...(m.readBy || []), String(byUserId)],
            }))
          );
        });

        // Reconnect — rejoin rooms
        conn.onreconnected(() => {
          loadConversations();
        });
      })
      .catch((err) => console.error('SignalR failed:', err));

    loadConversations();

    return () => { mounted = false; };
  }, [user, loadConversations]);

  // ── Load messages when chat changes ───────────────────────────────────────
  useEffect(() => {
    if (!selectedChat) { setMessages([]); return; }

    setMessages([]);
    setTypingUsers([]);
    setPage(1);
    setHasMore(false);
    setLoadingMessages(true);

    const loaders = {
      direct: () => messagesApi.getDirect(selectedChat.id, 1),
      group: () => messagesApi.getGroup(selectedChat.id, 1),
      channel: () => messagesApi.getChannel(selectedChat.id, 1),
    };

    (loaders[selectedChat.type] || loaders.direct)()
      .then((result) => {
        setMessages(result.items || result);
        setHasMore(result.hasMore || false);
      })
      .catch(console.error)
      .finally(() => setLoadingMessages(false));

    // Clear unread count
    setConversations((prev) =>
      prev.map((c) =>
        c.type === selectedChat.type && c.id === selectedChat.id ? { ...c, unreadCount: 0 } : c
      )
    );

    // Mark as read via SignalR
    const conn = connRef.current;
    if (conn?.state === 'Connected') {
      conn.invoke('MarkRead', selectedChat.id, selectedChat.type).catch(() => {});
    }
  }, [selectedChat]);

  // ── Load older messages (pagination) ──────────────────────────────────────
  const loadMore = useCallback(async () => {
    if (!selectedChat || !hasMore || loadingMessages) return;
    const nextPage = page + 1;
    setLoadingMessages(true);

    try {
      const loaders = {
        direct: () => messagesApi.getDirect(selectedChat.id, nextPage),
        group: () => messagesApi.getGroup(selectedChat.id, nextPage),
        channel: () => messagesApi.getChannel(selectedChat.id, nextPage),
      };
      const result = await (loaders[selectedChat.type] || loaders.direct)();
      setMessages((prev) => [...(result.items || result), ...prev]);
      setHasMore(result.hasMore || false);
      setPage(nextPage);
    } catch (err) {
      console.error(err);
    } finally {
      setLoadingMessages(false);
    }
  }, [selectedChat, hasMore, loadingMessages, page]);

  // ── Send message ───────────────────────────────────────────────────────────
  const sendMessage = useCallback(async (payload) => {
    const conn = connRef.current;
    if (!conn || conn.state !== 'Connected' || !selectedChat) return;

    const {
      content, encryptedContent = null, isEncrypted = false,
      type = 'Text', fileUrl = null, fileName = null,
      fileSize = null, fileMimeType = null, thumbnailUrl = null,
      threadParentId = null, expiresAt = null,
    } = payload;

    try {
      if (selectedChat.type === 'group') {
        await conn.invoke('SendGroupMessage',
          selectedChat.id, content, encryptedContent, isEncrypted,
          type, fileUrl, fileName, fileSize, fileMimeType, thumbnailUrl,
          threadParentId, expiresAt);
      } else if (selectedChat.type === 'channel') {
        await conn.invoke('SendChannelMessage',
          selectedChat.id, content, type, fileUrl, fileName, fileSize, payload.isSilent || false);
      } else {
        await conn.invoke('SendDirectMessage',
          selectedChat.id, content, encryptedContent, isEncrypted,
          type, fileUrl, fileName, fileSize, fileMimeType, thumbnailUrl,
          threadParentId, expiresAt);
      }
    } catch (err) {
      console.error('SendMessage failed:', err);
      throw err;
    }
  }, [selectedChat]);

  // ── Typing ─────────────────────────────────────────────────────────────────
  const sendTyping = useCallback((isTyping) => {
    const conn = connRef.current;
    if (!conn || conn.state !== 'Connected' || !selectedChat) return;
    conn.invoke(isTyping ? 'Typing' : 'StopTyping', selectedChat.id, selectedChat.type).catch(() => {});
  }, [selectedChat]);

  // ── Optimistic reaction ────────────────────────────────────────────────────
  const toggleReaction = useCallback(async (messageId, emoji) => {
    const conn = connRef.current;
    if (!conn || conn.state !== 'Connected') return;
    await conn.invoke('ReactToMessage', messageId, emoji);
  }, []);

  const sendPoll = useCallback(async (data) => {
    const conn = connRef.current;
    if (!conn || conn.state !== 'Connected' || !selectedChat) return;

    const {
      question,
      options,
      isMultipleChoice,
      expiresAt = null,
      threadParentId = null,
    } = data;

    if (selectedChat.type === 'group') {
      await conn.invoke('SendGroupPoll', selectedChat.id, question, options, isMultipleChoice, expiresAt, threadParentId);
    } else if (selectedChat.type === 'channel') {
      await conn.invoke('SendChannelPoll', selectedChat.id, question, options, isMultipleChoice, expiresAt);
    } else {
      await conn.invoke('SendDirectPoll', selectedChat.id, question, options, isMultipleChoice, expiresAt, threadParentId);
    }
  }, [selectedChat]);

  const votePoll = useCallback(async (pollOptionId) => {
    const conn = connRef.current;
    if (!conn || conn.state !== 'Connected') return;
    await conn.invoke('VotePoll', pollOptionId);
  }, []);

  return {
    conversations,
    selectedChat,
    setSelectedChat,
    messages,
    setMessages,
    loadingMessages,
    hasMore,
    loadMore,
    typingUsers,
    onlineUsers,
    sendMessage,
    sendPoll,
    votePoll,
    sendTyping,
    toggleReaction,
    loadConversations,
  };
}