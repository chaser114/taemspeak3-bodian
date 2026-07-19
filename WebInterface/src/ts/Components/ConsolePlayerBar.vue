<template>
  <section :class="['player-bar', { expanded }]">
    <button v-if="expanded" class="back-button" title="返回播放器" @click="expanded = false">‹ 返回</button>

    <button class="track-summary" title="打开完整播放器" @click="expanded = !expanded">
      <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
      <span v-else class="cover-placeholder">♫</span>
      <span class="track-copy">
        <b>{{ trackTitle }}</b>
        <small>{{ state.current ? '正在频道中播放' : '搜索后即可加入待播' }}</small>
      </span>
    </button>

    <div class="controls" aria-label="播放控制">
      <button title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')">⏮</button>
      <button class="play-button" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')">{{ showPlayIcon ? '▶' : 'Ⅱ' }}</button>
      <button title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')">⏭</button>
    </div>

    <div class="timeline">
      <div class="progress-track"><i :style="{ transform: 'scaleX(' + progressRatio + ')' }"></i></div>
      <small>{{ time(livePosition) }} / {{ time(state.length || 0) }}</small>
    </div>

    <button class="queue-button" title="待播队列" aria-label="待播队列" @click="$emit('queue')">
      ☷<em v-if="state.queue.length">{{ state.queue.length }}</em>
    </button>
  </section>
</template>

<script lang="ts">
import Vue from "vue";
import { MusicState } from "../ConsoleApi";

export default Vue.extend({
  props: {
    state: { type: Object as () => MusicState, required: true },
    busy: { type: Boolean, default: false },
  },
  data() {
    const now = Date.now();
    return {
      expanded: false,
      serverPosition: 0,
      renderedPosition: 0,
      syncedAt: now,
      lastFrameAt: now,
      trackKey: "",
      frameId: 0 as number,
    };
  },
  mounted() { this.frameId = requestAnimationFrame(() => this.tick()); },
  beforeDestroy() { cancelAnimationFrame(this.frameId); },
  watch: {
    state: {
      immediate: true,
      deep: true,
      handler(state: MusicState) {
        const nextTrackKey = this.trackKeyFor(state);
        const nextPosition = Math.max(0, state.position || 0);
        const trackChanged = nextTrackKey !== this.trackKey;

        this.serverPosition = nextPosition;
        this.syncedAt = Date.now();
        this.trackKey = nextTrackKey;

        // A new resource is a hard boundary; small polling drift is animated in tick().
        if (trackChanged) this.renderedPosition = nextPosition;
      },
    },
  },
  computed: {
    livePosition(): number {
      const state = this.state as MusicState;
      if (!state.current) return 0;
      return state.length ? Math.min(state.length, this.renderedPosition) : this.renderedPosition;
    },
    progressRatio(): number {
      const state = this.state as MusicState;
      return state.length ? Math.min(1, Math.max(0, this.livePosition / state.length)) : 0;
    },
    trackTitle(): string {
      return this.state.current ? this.state.current.title : "等待点歌";
    },
    canControl(): boolean {
      return !this.busy && !!this.state.current;
    },
    canSkip(): boolean {
      if (this.busy) return false;
      if (this.state.current) return true;
      return !!(this.state.queue && this.state.queue.length);
    },
    showPlayIcon(): boolean {
      return !this.state.current || !!this.state.paused;
    },
    pauseTitle(): string {
      if (!this.state.current) return "暂无可播放歌曲";
      return this.state.paused ? "继续播放" : "暂停播放";
    },
  },
  methods: {
    trackKeyFor(state: MusicState): string {
      return state.current ? state.current.type + ":" + state.current.resource.resid : "";
    },
    tick() {
      const currentTime = Date.now();
      const elapsed = Math.min(0.1, Math.max(0, (currentTime - this.lastFrameAt) / 1000));
      const state = this.state as MusicState;

      this.lastFrameAt = currentTime;

      if (!state.current) {
        this.renderedPosition = 0;
      } else {
        const playing = !state.paused && state.connected;
        if (playing) this.renderedPosition += elapsed;

        const target = this.serverPosition + (playing ? Math.max(0, (currentTime - this.syncedAt) / 1000) : 0);
        const drift = target - this.renderedPosition;

        // Keep normal polling corrections invisible, but recover quickly from a stale snapshot.
        if (Math.abs(drift) > 3) this.renderedPosition = target;
        else this.renderedPosition += drift * (1 - Math.exp(-8 * elapsed));

        if (state.length) this.renderedPosition = Math.min(state.length, Math.max(0, this.renderedPosition));
      }

      this.frameId = requestAnimationFrame(() => this.tick());
    },
    time(seconds: number) {
      return Math.floor(seconds / 60) + ":" + String(Math.floor(seconds % 60)).padStart(2, "0");
    },
  },
});
</script>

<style scoped lang="less">
.player-bar { position: fixed; z-index: 6; left: 216px; right: 0; bottom: 0; height: 96px; display: flex; align-items: center; gap: 24px; padding: 12px 34px; background: rgba(255,255,255,.97); border-top: 1px solid #dfe6e8; box-shadow: 0 -10px 28px rgba(30,50,55,.07); transition: height .36s cubic-bezier(.22,.61,.36,1), gap .36s ease, padding .36s ease, background-color .36s ease, box-shadow .36s ease; }
button { border: 0; background: transparent; cursor: pointer; }
.track-summary { min-width: 250px; max-width: 34%; display: flex; align-items: center; gap: 12px; color: var(--console-ink); text-align: left; }
.track-summary img, .cover-placeholder { width: 58px; height: 58px; flex: 0 0 58px; border-radius: 9px; object-fit: cover; background: #e1f1ed; display: grid; place-items: center; color: #287f74; font-size: 24px; transition: width .36s cubic-bezier(.22,.61,.36,1), height .36s cubic-bezier(.22,.61,.36,1), flex-basis .36s cubic-bezier(.22,.61,.36,1), border-radius .36s ease, transform .36s ease; }
.track-copy { min-width: 0; }
.track-copy b, .track-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-copy small { margin-top: 5px; color: #768493; }
.controls { display: flex; align-items: center; gap: 10px; }
.controls button { width: 38px; height: 38px; border-radius: 50%; color: #476170; font-size: 18px; transition: background .18s ease, color .18s ease, transform .18s cubic-bezier(.22,.61,.36,1), box-shadow .18s ease, opacity .18s ease; }
.controls button:hover:not(:disabled) { background: #edf7f5; color: #257e73; transform: translateY(-2px); }
.controls button:active:not(:disabled) { transform: translateY(0) scale(.9); }
.controls button:disabled { opacity: .55; cursor: wait; }
.controls .play-button { width: 52px; height: 52px; background: #4fb8a8; color: #fff; font-size: 20px; box-shadow: 0 5px 12px rgba(79,184,168,.28); }
.controls .play-button:hover:not(:disabled) { background: #257e73; color: #fff; box-shadow: 0 8px 18px rgba(37,126,115,.25); }
.timeline { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 160px; }
.progress-track { height: 4px; flex: 1; overflow: hidden; border-radius: 4px; background: #dfe6e8; }
.progress-track i { display: block; width: 100%; height: 100%; background: #4fb8a8; transform-origin: left center; will-change: transform; transition: transform .08s linear; }
.timeline small { color: #83909d; font-size: 12px; white-space: nowrap; }
.queue-button { position: relative; width: 42px; height: 42px; border-radius: 50%; color: #526b75; font-size: 22px; transition: background .18s ease, color .18s ease, transform .18s cubic-bezier(.22,.61,.36,1); }
.queue-button:hover { background: #edf7f5; color: #257e73; transform: translateY(-2px); }
.queue-button:active { transform: scale(.9); }
.queue-button em { position: absolute; top: -2px; right: -2px; min-width: 17px; border-radius: 10px; background: #287f74; color: #fff; font-size: 10px; font-style: normal; }

.player-bar.expanded { left: 216px; bottom: 0; height: 100vh; z-index: 10; flex-direction: column; justify-content: center; gap: 18px; padding: 52px 34px; background: #f7fbfa; box-shadow: 0 0 40px rgba(30,50,55,.12); }
.expanded .track-summary { max-width: none; display: grid; text-align: center; }
.expanded .track-summary img, .expanded .cover-placeholder { width: 46vw; height: 46vw; max-width: 340px; max-height: 340px; margin: auto; border-radius: 18px; }
.expanded .track-copy small { margin-top: 8px; }
.expanded .controls { margin-top: 16px; }
.expanded .timeline { width: 70vw; max-width: 620px; flex: none; }
.expanded .queue-button { margin-top: 2px; }
.back-button { position: absolute; top: 24px; left: 30px; color: #287f74; font-size: 16px; transition: color .18s ease, transform .18s cubic-bezier(.22,.61,.36,1); }
.back-button:hover { color: #185d55; transform: translateX(-3px); }

@media (max-width: 760px) {
  .player-bar { left: 0; bottom: calc(58px + env(safe-area-inset-bottom)); height: 76px; gap: 8px; padding: 9px 14px; }
  .track-summary { min-width: 0; flex: 1; }
  .track-summary img, .cover-placeholder { width: 50px; height: 50px; flex-basis: 50px; }
  .controls { gap: 2px; }
  .controls button { width: 31px; height: 31px; font-size: 15px; }
  .controls .play-button { width: 42px; height: 42px; }
  .timeline { display: none; }
  .player-bar.expanded { left: 0; bottom: calc(58px + env(safe-area-inset-bottom)); height: calc(100vh - 58px - env(safe-area-inset-bottom)); padding: 52px 20px 28px; }
  .expanded .track-summary img, .expanded .cover-placeholder { width: 72vw; height: 72vw; max-width: 290px; max-height: 290px; }
  .expanded .timeline { display: flex; width: 100%; max-width: 430px; }
  .back-button { top: 18px; left: 18px; }
}

@media (prefers-reduced-motion: reduce) {
  .player-bar, .track-summary img, .cover-placeholder, .controls button, .queue-button, .back-button, .progress-track i { transition: none; }
}
</style>
