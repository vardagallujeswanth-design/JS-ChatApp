import { useAuth } from './contexts/AuthContext.jsx';
import AuthPage from './pages/AuthPage.jsx';
import ChatPage from './pages/ChatPage.jsx';

export default function App() {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <div className="splash">
        <div className="splash-logo">
          <svg width="48" height="48" viewBox="0 0 32 32" fill="none">
            <circle cx="16" cy="16" r="16" fill="#25D366"/>
            <path d="M16 5C9.925 5 5 9.925 5 16c0 1.987.536 3.847 1.473 4.448L5 27l6.667-1.75A10.96 10.96 0 0016 27c6.075 0 11-4.925 11-11S22.075 5 16 5z" fill="white" fillOpacity=".9"/>
          </svg>
        </div>
        <div className="spinner large" />
      </div>
    );
  }

  return user ? <ChatPage /> : <AuthPage />;
}