import * as signalR from '@microsoft/signalr';

let connection = null;

export function getConnection() {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5215/hubs/chat', {
        accessTokenFactory: () => localStorage.getItem('accessToken') || '',
      })
      .withAutomaticReconnect([0, 1000, 3000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
}

export async function startConnection() {
  const conn = getConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start();
  }
  return conn;
}

export async function stopConnection() {
  if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
    await connection.stop();
    connection = null;
  }
}