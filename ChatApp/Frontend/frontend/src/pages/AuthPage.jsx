import { useState } from 'react';
import { useAuth } from '../contexts/AuthContext.jsx';

export default function AuthPage() {
  const { login, loginTwoFactor, register } = useAuth();
  const [mode, setMode] = useState('login');
  const [twoFactorRequired, setTwoFactorRequired] = useState(false);
  const [credentials, setCredentials] = useState({ phoneNumber: '', password: '' });
  const [form, setForm] = useState({ username: '', phoneNumber: '', password: '', displayName: '' });
  const [twoFactorCode, setTwoFactorCode] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const set = (e) => setForm((p) => ({ ...p, [e.target.name]: e.target.value }));

  const submit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      if (mode === 'login' && !twoFactorRequired) {
        if (!form.phoneNumber.trim()) throw new Error('Phone number is required');
        const res = await login(form.phoneNumber.trim(), form.password);
        if (res?.twoFactorRequired) {
          setTwoFactorRequired(true);
          setCredentials({ phoneNumber: form.phoneNumber.trim(), password: form.password });
          setLoading(false);
          return;
        }
      } else if (mode === 'login' && twoFactorRequired) {
        if (!twoFactorCode.trim()) throw new Error('2FA code is required');
        await loginTwoFactor(credentials.phoneNumber, credentials.password, twoFactorCode.trim());
      } else {
        if (!form.username.trim()) throw new Error('Username is required');
        if (form.username.length < 3) throw new Error('Username must be at least 3 characters');
        if (!form.phoneNumber.trim()) throw new Error('Phone number is required');
        if (form.password.length < 6) throw new Error('Password must be at least 6 characters');
        await register(form.username.trim(), form.phoneNumber.trim(), form.password, form.displayName || form.username);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth-root">
      <div className="auth-card">
        {/* Logo */}
        <div className="auth-brand">
          <div className="auth-logo-icon">
            <svg width="32" height="32" viewBox="0 0 32 32" fill="none">
              <path d="M16 2C8.268 2 2 8.268 2 16c0 2.386.625 4.625 1.72 5.562L2 30l8.667-2.287A13.9 13.9 0 0016 30c7.732 0 14-6.268 14-14S23.732 2 16 2z" fill="white" fillOpacity=".9"/>
              <path d="M12.5 11c-.3-.8-.6-.82-.9-.84-.233-.013-.5-.013-.77-.013-.267 0-.7.1-1.067.5-.367.4-1.4 1.367-1.4 3.333s1.433 3.867 1.633 4.133c.2.267 2.767 4.367 6.8 5.934 3.367 1.326 4.05 1.063 4.783.996.734-.067 2.367-.966 2.7-1.9.333-.933.333-1.733.233-1.9-.1-.167-.367-.267-.767-.467-.4-.2-2.367-1.167-2.733-1.3-.367-.133-.633-.2-.9.2s-1.033 1.3-1.267 1.567c-.233.266-.467.3-.867.1-.4-.2-1.692-.624-3.22-1.99-1.19-1.06-1.993-2.369-2.227-2.769-.233-.4-.025-.617.175-.817.18-.18.4-.467.6-.7.2-.233.267-.4.4-.667.133-.267.067-.5-.033-.7-.1-.2-.867-2.18-1.19-2.98z" fill="#25D366"/>
            </svg>
          </div>
          <h1 className="auth-title">ChatApp</h1>
          <p className="auth-subtitle">Better than WhatsApp.</p>
        </div>

        {/* Tabs */}
        <div className="auth-tabs">
          <button
            className={`auth-tab ${mode === 'login' ? 'active' : ''}`}
            onClick={() => { setMode('login'); setError(''); setTwoFactorRequired(false); }}
          >Sign in</button>
          <button
            className={`auth-tab ${mode === 'register' ? 'active' : ''}`}
            onClick={() => { setMode('register'); setError(''); }}
          >Create account</button>
        </div>

        <form onSubmit={submit} className="auth-form">
          {mode === 'register' && (
            <>
              <div className="field">
                <label className="field-label">Username</label>
                <input
                  className="field-input"
                  name="username"
                  value={form.username}
                  onChange={set}
                  placeholder="johndoe"
                  autoComplete="username"
                  required
                />
              </div>
              <div className="field">
                <label className="field-label">Display name <span className="field-optional">(optional)</span></label>
                <input
                  className="field-input"
                  name="displayName"
                  value={form.displayName}
                  onChange={set}
                  placeholder="John Doe"
                />
              </div>
            </>
          )}

          <div className="field">
            <label className="field-label">Phone number</label>
            <input
              className="field-input"
              name="phoneNumber"
              type="tel"
              value={form.phoneNumber}
              onChange={set}
              placeholder="+1234567890"
              autoComplete="tel"
              required
            />
          </div>

          <div className="field">
            <label className="field-label">Password</label>
            <input
              className="field-input"
              name="password"
              type="password"
              value={form.password}
              onChange={set}
              placeholder="••••••••"
              autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              required
            />
          </div>

          {twoFactorRequired && (
            <div className="field">
              <label className="field-label">2FA code</label>
              <input
                className="field-input"
                name="twoFactorCode"
                value={twoFactorCode}
                onChange={(e) => setTwoFactorCode(e.target.value)}
                placeholder="123456"
                required
              />
            </div>
          )}

          {error && <div className="auth-error">{error}</div>}

          <button className="auth-btn" type="submit" disabled={loading}>
            {loading
              ? <span className="spinner" />
              : mode === 'login'
                ? twoFactorRequired
                  ? 'Verify 2FA Code'
                  : 'Sign in'
                : 'Create account'}
          </button>
        </form>

        <p className="auth-footer">
          Phone number is required; email is no longer needed.
        </p>
      </div>
    </div>
  );
}