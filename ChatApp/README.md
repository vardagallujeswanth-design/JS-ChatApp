# ChatApp

This is ChatApp, a real-time chat application implemented with ASP.NET Core, SignalR, and a React + Vite frontend.

## Implemented features (Phase 3 + Phase 4 2FA)

- Direct, Group, and Channel messaging via SignalR
- Channels are admin-only posting
- Channel silent messages (`IsSilent`) for no-notification announcements
- Pinned messages with pin categories (`Important`, `ActionRequired`, `Reference`, `General`)
- Message reactions
- Polls, read receipts, message editing, deletion
- User presence tracking
- Two-factor authentication (TOTP):
  - `/api/auth/2fa/setup`
  - `/api/auth/2fa/verify`
  - `/api/auth/2fa/disable`
  - `/api/auth/login` returns `twoFactorRequired` when enabled
  - `/api/auth/login-2fa` for code-based login

## Backend (ASP.NET Core)

- `ChatHub.cs` updates:
  - `SendChannelMessage(..., bool isSilent = false)`
  - `PinMessage` / `UnpinMessage`
  - `Message` model has `IsSilent`
- `Models.cs` updates:
  - `PinCategory` enum
  - `User` has `IsTwoFactorEnabled` and `TwoFactorSecret`
- `AuthController.cs` updates: register/login/2fa flows
- `TwoFactorService` for TOTP secret generation and validation

## Frontend (Vite + React)

- `AuthPage.jsx` supports login + TOTP step
- `AuthContext.jsx` supports `loginTwoFactor`
- `api.js` supports `loginTwoFactor`
- `ChatWindow.jsx` supports silent channel message toggle and rendering
- `MessageBubble.jsx` shows `🔕 Silent` tag and action support for pin/unpin

## Setup

### Backend

1. Ensure you have .NET SDK 8+.
2. Create DB and apply migrations:
   ```bash
   dotnet ef migrations add AddTwoFactorAndChannelSilent
   dotnet ef database update
   ```
3. Run API:
   ```bash
   dotnet run
   ```

### Frontend

```bash
cd Frontend/frontend
npm install
npm run dev
```

## Git commands

To push changes to remote:

```bash
git add .
git commit -m "Add channel silent/pin and 2FA frontend+backend implementation"
git push
```

## Notes

- `ChatHub` still uses in-memory connections (`_connections`), not yet horizontally scaled.
- Added channel discovery/analytics APIs and UI (Sidebar discovery modal).
- Added broadcast list endpoints (`/api/broadcast`) and UI for list creation + send.
- Added E2E key rotation endpoints (`/api/users/me/public-key` and `/api/users/me/verify-device`) and placeholder browser-based workflow.
- Added GraphQL query stub (`/api/graphql`) for channels/users, plus open API bot endpoint (`/api/openapi/messages`).
- Added admin dashboard endpoints and UI (`/api/admin/stats`, `/api/admin/users/{id}/ban`, `/api/admin/messages/{id}`)
- Added tests: 2FA workflow, silent channel post, pin/unpin flow, message reactions.
