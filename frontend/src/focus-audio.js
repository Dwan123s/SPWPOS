// Short local chime; no remote media or autoplay dependency.
export function createFocusAudio() {
  let context;
  return {
    unlock() {
      try {
        const Audio = window.AudioContext || window.webkitAudioContext;
        if (!Audio) return;
        context ??= new Audio();
        if (context.state === "suspended")
          void context.resume().catch(() => {});
      } catch {
        /* Audio is optional. */
      }
    },
    chime(volume) {
      if (context?.state !== "running" || volume <= 0) return;
      try {
        [659.25, 880].forEach((hz, index) => {
          const tone = context.createOscillator(),
            gain = context.createGain();
          const start = context.currentTime + index * 0.18;
          tone.frequency.value = hz;
          gain.gain.setValueAtTime(0, start);
          gain.gain.linearRampToValueAtTime((volume / 100) * 0.2, start + 0.02);
          gain.gain.exponentialRampToValueAtTime(0.001, start + 0.4);
          tone.connect(gain);
          gain.connect(context.destination);
          tone.onended = () => {
            tone.disconnect();
            gain.disconnect();
          };
          tone.start(start);
          tone.stop(start + 0.42);
        });
      } catch {
        /* Countdown continues without sound. */
      }
    },
    close() {
      void context?.close().catch(() => {});
      context = null;
    },
  };
}
