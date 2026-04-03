import { useAuth } from '../contexts/AuthContext.jsx';
import { useChat } from '../hooks/useChat.js';
import Sidebar from '../components/Sidebar.jsx';
import ChatWindow from '../components/ChatWindow.jsx';

export default function ChatPage() {
  const { user } = useAuth();
  const chat = useChat(user);

  return (
    <div className="chat-layout">
      <Sidebar
        conversations={chat.conversations}
        selectedChat={chat.selectedChat}
        setSelectedChat={chat.setSelectedChat}
        onlineUsers={chat.onlineUsers}
        loadConversations={chat.loadConversations}
      />
      <ChatWindow
        selectedChat={chat.selectedChat}
        messages={chat.messages}
        setMessages={chat.setMessages}
        loadingMessages={chat.loadingMessages}
        hasMore={chat.hasMore}
        loadMore={chat.loadMore}
        typingUsers={chat.typingUsers}
        onlineUsers={chat.onlineUsers}
        sendMessage={chat.sendMessage}
        sendPoll={chat.sendPoll}
        votePoll={chat.votePoll}
        sendTyping={chat.sendTyping}
        toggleReaction={chat.toggleReaction}
        currentUser={user}
      />
    </div>
  );
}