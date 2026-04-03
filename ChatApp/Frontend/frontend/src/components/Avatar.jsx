const COLORS = [
  '#7C3AED','#DB2777','#DC2626','#D97706',
  '#059669','#0891B2','#2563EB','#9333EA',
];

function colorFor(name = '') {
  let h = 0;
  for (let i = 0; i < name.length; i++) h = name.charCodeAt(i) + ((h << 5) - h);
  return COLORS[Math.abs(h) % COLORS.length];
}

export default function Avatar({ name = '', avatarUrl, size = 40, online = false, className = '' }) {
  const initials = name.split(/\s+/).map((w) => w[0]).join('').slice(0, 2).toUpperCase() || '?';
  const bg = colorFor(name);
  const dotSize = Math.max(10, size * 0.27);

  return (
    <div
      className={className}
      style={{ position: 'relative', width: size, height: size, flexShrink: 0, display: 'inline-block' }}
    >
      {avatarUrl ? (
        <img
          src={avatarUrl.startsWith('http') ? avatarUrl : `http://localhost:5215${avatarUrl}`}
          alt={name}
          style={{ width: size, height: size, borderRadius: '50%', objectFit: 'cover', display: 'block' }}
          onError={(e) => {
            e.target.style.display = 'none';
            if (e.target.nextSibling) e.target.nextSibling.style.display = 'flex';
          }}
        />
      ) : null}
      <div
        style={{
          width: size, height: size, borderRadius: '50%',
          background: bg, color: '#fff',
          display: avatarUrl ? 'none' : 'flex',
          alignItems: 'center', justifyContent: 'center',
          fontWeight: 600, fontSize: size * 0.37,
          userSelect: 'none', letterSpacing: '-0.5px',
        }}
      >
        {initials}
      </div>
      {online && (
        <span style={{
          position: 'absolute', bottom: 0, right: 0,
          width: dotSize, height: dotSize, borderRadius: '50%',
          background: '#22c55e',
          border: `${Math.max(2, dotSize * 0.2)}px solid var(--bg-primary)`,
          boxSizing: 'border-box',
        }} />
      )}
    </div>
  );
}