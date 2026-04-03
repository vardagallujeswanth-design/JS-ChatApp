import { useEffect, useRef, useState } from 'react';
import { callsApi } from '../services/api.js';
import { startConnection } from '../services/signalr.js';

export default function CallPage({ user, targetUserId, targetName, type, onHangup, onDone }) {
  const localVideoRef = useRef(null);
  const remoteVideoRef = useRef(null);
  const [isMuted, setIsMuted] = useState(false);
  const [isSpeaker, setIsSpeaker] = useState(false);
  const [isOnCall, setIsOnCall] = useState(false);
  const [duration, setDuration] = useState(0);
  const [timerId, setTimerId] = useState(null);
  const [lowBandwidth, setLowBandwidth] = useState(false);

  const pcRef = useRef(null);
  const connRef = useRef(null);

  useEffect(() => {
    let interval;
    if (isOnCall) {
      interval = setInterval(() => setDuration((d) => d + 1), 1000);
      setTimerId(interval);
    }
    return () => clearInterval(interval);
  }, [isOnCall]);

  useEffect(() => {
    if (!user) return;

    async function init() {
      const conn = await startConnection();
      connRef.current = conn;

      conn.on('CallOffer', async (payload) => {
        if (!payload || payload.from !== targetUserId) return;
        await createPeerConnection();
        await pcRef.current.setRemoteDescription(new RTCSessionDescription(payload.offer));
        const answer = await pcRef.current.createAnswer();
        await pcRef.current.setLocalDescription(answer);
        conn.invoke('CallAnswer', payload.from, answer);
      });

      conn.on('CallAnswer', async (payload) => {
        if (!payload || payload.from !== targetUserId) return;
        await pcRef.current.setRemoteDescription(new RTCSessionDescription(payload.answer));
      });

      conn.on('IceCandidate', async (payload) => {
        if (!payload || payload.from !== targetUserId) return;
        await pcRef.current.addIceCandidate(new RTCIceCandidate(payload.candidate));
      });

      conn.on('CallEnded', () => {
        cleanup();
      });
    }

    init();

    return () => {
      cleanup();
      connRef.current?.stop();
    };
  }, [targetUserId, user]);

  const cleanup = () => {
    setIsOnCall(false);
    if (timerId) clearInterval(timerId);
    pcRef.current?.close();
    pcRef.current = null;
    onDone?.();
  };

  const createPeerConnection = async () => {
    const pc = new RTCPeerConnection({
      iceServers: [{ urls: 'stun:stun.l.google.com:19302' }]
    });

    pc.onicecandidate = (e) => {
      if (e.candidate) connRef.current?.invoke('IceCandidate', targetUserId, e.candidate);
    };
    pc.ontrack = (e) => {
      if (remoteVideoRef.current) remoteVideoRef.current.srcObject = e.streams[0];
    };

    const localStream = await navigator.mediaDevices.getUserMedia({ audio: true, video: type === 'video' });
    if (localVideoRef.current) localVideoRef.current.srcObject = localStream;

    localStream.getTracks().forEach((track) => pc.addTrack(track, localStream));
    pcRef.current = pc;
    return pc;
  };

  const startCall = async () => {
    const pc = await createPeerConnection();
    const offer = await pc.createOffer({ offerToReceiveAudio: true, offerToReceiveVideo: type === 'video' });
    await pc.setLocalDescription(offer);
    connRef.current?.invoke('CallOffer', targetUserId, offer);
    await callsApi.start(targetUserId, type, lowBandwidth);
    setIsOnCall(true);
  };

  const endCall = async () => {
    await callsApi.end(0, duration);
    connRef.current?.invoke('CallEnded', targetUserId);
    cleanup();
    onHangup?.();
  };

  return (
    <div className="call-panel">
      <div className="call-header">
        <h3>{type === 'video' ? 'Video Call' : 'Voice Call'} with {targetName || 'Unknown'}</h3>
        <span>Duration: {Math.floor(duration / 60)}:{(duration % 60).toString().padStart(2, '0')}</span>
      </div>
      <div className="call-video">
        <video ref={localVideoRef} autoPlay muted playsInline className="local-video" />
        {type === 'video' && <video ref={remoteVideoRef} autoPlay playsInline className="remote-video" />}
      </div>
      <div className="call-controls">
        <button onClick={() => setIsMuted((v) => !v)}>{isMuted ? 'Unmute' : 'Mute'}</button>
        <button onClick={() => setIsSpeaker((v) => !v)}>{isSpeaker ? 'Speaker Off' : 'Speaker On'}</button>
        <button onClick={() => setLowBandwidth((v) => !v)}>{lowBandwidth ? 'Low BW Off' : 'Low BW'}</button>
        {!isOnCall ? (
          <button className="primary" onClick={startCall}>Start Call</button>
        ) : (
          <button className="danger" onClick={endCall}>Hang Up</button>
        )}
      </div>
    </div>
  );
}
