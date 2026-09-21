/**
 * Low-latency Web Audio API synthesizer and Text-To-Speech coaching feedback
 */

class AudioCoachService {
  private audioCtx: AudioContext | null = null;
  private isMuted: boolean = false;
  private isVoiceMuted: boolean = false;
  private lastSpokenText: string = '';
  private lastSpokenTime: number = 0;
  private voiceCooldownMs: number = 2200; // avoid spamming voice tips

  constructor() {
    // Lazy AudioContext initialization on first user interaction to satisfy browser policies
  }

  private initAudio() {
    if (!this.audioCtx) {
      const AudioContextClass = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      this.audioCtx = new AudioContextClass();
    }
    if (this.audioCtx.state === 'suspended') {
      this.audioCtx.resume();
    }
  }

  public setMuted(muted: boolean) {
    this.isMuted = muted;
  }

  public setVoiceMuted(muted: boolean) {
    this.isVoiceMuted = muted;
  }

  public getIsMuted(): boolean {
    return this.isMuted;
  }

  public getIsVoiceMuted(): boolean {
    return this.isVoiceMuted;
  }

  /**
   * Instant low-latency zero-network synthesized audio feedback (<5ms)
   */
  public playTone(type: 'success' | 'warning' | 'error' | 'click' | 'swing') {
    if (this.isMuted) return;
    try {
      this.initAudio();
      if (!this.audioCtx) return;

      const ctx = this.audioCtx;
      const now = ctx.currentTime;

      if (type === 'success') {
        // High harmonic double-ping
        const osc1 = ctx.createOscillator();
        const osc2 = ctx.createOscillator();
        const gain = ctx.createGain();

        osc1.type = 'sine';
        osc2.type = 'triangle';
        osc1.frequency.setValueAtTime(587.33, now); // D5
        osc1.frequency.exponentialRampToValueAtTime(880, now + 0.12); // A5
        osc2.frequency.setValueAtTime(880, now + 0.05);
        osc2.frequency.exponentialRampToValueAtTime(1174.66, now + 0.18); // D6

        gain.gain.setValueAtTime(0.25, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.28);

        osc1.connect(gain);
        osc2.connect(gain);
        gain.connect(ctx.destination);

        osc1.start(now);
        osc2.start(now + 0.05);
        osc1.stop(now + 0.28);
        osc2.stop(now + 0.28);
      } else if (type === 'warning') {
        // Caution double soft tap
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(392, now); // G4
        osc.frequency.setValueAtTime(329.63, now + 0.08); // E4

        gain.gain.setValueAtTime(0.3, now);
        gain.gain.exponentialRampToValueAtTime(0.01, now + 0.22);

        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.22);
      } else if (type === 'error') {
        // Low buzzing error tone
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sawtooth';
        osc.frequency.setValueAtTime(160, now);
        osc.frequency.exponentialRampToValueAtTime(110, now + 0.25);

        gain.gain.setValueAtTime(0.35, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.25);

        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.25);
      } else if (type === 'swing') {
        // Fast whoosh sound using modulated noise or quick sine pitch sweep
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(200, now);
        osc.frequency.exponentialRampToValueAtTime(650, now + 0.08);
        osc.frequency.exponentialRampToValueAtTime(150, now + 0.16);

        gain.gain.setValueAtTime(0.15, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.18);

        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.18);
      } else if (type === 'click') {
        const osc = ctx.createOscillator();
        const gain = ctx.createGain();
        osc.type = 'sine';
        osc.frequency.setValueAtTime(1200, now);
        gain.gain.setValueAtTime(0.1, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.04);
        osc.connect(gain);
        gain.connect(ctx.destination);
        osc.start(now);
        osc.stop(now + 0.04);
      }
    } catch {
      // AudioContext might be blocked until gesture
    }
  }

  /**
   * Spoken coaching feedback with debounce
   */
  public speakTip(tip: string, force = false) {
    if (this.isVoiceMuted) return;
    if (!('speechSynthesis' in window)) return;

    const now = Date.now();
    if (!force && tip === this.lastSpokenText && now - this.lastSpokenTime < this.voiceCooldownMs * 2) {
      return;
    }
    if (!force && now - this.lastSpokenTime < this.voiceCooldownMs) {
      return;
    }

    try {
      window.speechSynthesis.cancel(); // cancel pending speech for zero lag
      const utterance = new SpeechSynthesisUtterance(tip);
      utterance.rate = 1.25; // slightly faster for live coaching
      utterance.pitch = 1.05;
      utterance.volume = 0.9;

      // Select Vietnamese voice if available
      const voices = window.speechSynthesis.getVoices();
      const viVoice = voices.find(v => v.lang.startsWith('vi') || v.name.toLowerCase().includes('vietnam'));
      if (viVoice) {
        utterance.voice = viVoice;
      }

      this.lastSpokenText = tip;
      this.lastSpokenTime = now;
      window.speechSynthesis.speak(utterance);
    } catch {
      // Speech synthesis error handled gracefully
    }
  }
}

export const audioCoach = new AudioCoachService();
