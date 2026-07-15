import AgoraRTC, { type IAgoraRTCClient, type IMicrophoneAudioTrack } from "agora-rtc-sdk-ng";

/**
 * Thin wrapper around the Agora Web SDK for a single voice channel at a time (Nightfall only ever
 * needs one active channel per player — main room or mafia room, never both simultaneously).
 */
export class VoiceSession {
  private client: IAgoraRTCClient | null = null;
  private micTrack: IMicrophoneAudioTrack | null = null;

  get isConnected(): boolean {
    return this.client !== null;
  }

  async join(appId: string, channelName: string, token: string, uid: number, canPublish: boolean): Promise<void> {
    await this.leave();

    const client = AgoraRTC.createClient({ mode: "rtc", codec: "vp8" });
    await client.join(appId, channelName, token, uid);
    this.client = client;

    client.on("user-published", async (user, mediaType) => {
      if (mediaType !== "audio") return;
      await client.subscribe(user, mediaType);
      user.audioTrack?.play();
    });

    if (canPublish) {
      this.micTrack = await AgoraRTC.createMicrophoneAudioTrack();
      await client.publish([this.micTrack]);
    }
  }

  async setMuted(muted: boolean): Promise<void> {
    await this.micTrack?.setEnabled(!muted);
  }

  async leave(): Promise<void> {
    this.micTrack?.close();
    this.micTrack = null;
    await this.client?.leave();
    this.client = null;
  }
}
