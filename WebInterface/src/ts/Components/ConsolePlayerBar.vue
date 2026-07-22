<template>
  <section :class="['player-bar', { expanded }]">
    <button v-if="expanded" type="button" class="back-button" title="返回播放器" @click="expanded = false">‹ 返回</button>

    <button type="button" class="track-summary" title="打开完整播放器" @click="expanded = !expanded">
      <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
      <span v-else class="cover-placeholder">♫</span>
      <span class="track-copy">
        <b>{{ trackTitle }}</b>
        <small>{{ state.current ? (expanded ? '点击返回' : '点击查看歌词') : '搜索后即可加入待播' }}</small>
      </span>
    </button>

    <div v-if="expanded" class="lyrics-panel" ref="lyricsPanel">
      <p v-if="lyricsLoading" class="lyrics-empty">歌词加载中…</p>
      <p v-else-if="!state.current" class="lyrics-empty">暂无播放</p>
      <p v-else-if="!lyricLines.length" class="lyrics-empty">暂无歌词</p>
      <div v-else class="lyrics-scroll">
        <p
          v-for="(line, index) in lyricLines"
          :key="index + '-' + line.time"
          :class="['lyrics-line', { active: index === activeLyricIndex }]"
          :ref="'lyric-' + index"
        >{{ line.text }}</p>
      </div>
    </div>

    <div class="controls" aria-label="播放控制">
      <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')">⏮</button>
      <button type="button" class="play-button" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')">{{ showPlayIcon ? '▶' : 'Ⅱ' }}</button>
      <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')">⏭</button>
    </div>

    <div class="timeline">
      <div class="progress-track"><i :style="{ transform: 'scaleX(' + progressRatio + ')' }"></i></div>
      <small>{{ time(livePosition) }} / {{ time(state.length || 0) }}</small>
    </div>

    <div class="mode-volume" aria-label="播放模式与音量">
      <button type="button" class="mode-button" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop">{{ loopIcon }}</button>
      <button type="button" class="mode-button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom">⧉</button>
      <label class="volume" :title="'音量 ' + Math.round(localVolume)">
        <span>♪</span>
        <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" @input="onVolumeInput" @change="onVolumeCommit">
        <em>{{ Math.round(localVolume) }}</em>
      </label>
    </div>

    <button type="button" class="queue-button" title="待播队列" aria-label="待播队列" @click="$emit('queue')">
      ☷<em v-if="state.queue.length">{{ state.queue.length }}</em>
    </button>
  </section>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, MusicState } from "../ConsoleApi";

interface LyricLine { time: number; text: string; }

export default Vue.extend({
  props: {
    state: { type: Object as () => MusicState, required: true },
    busy: { type: Boolean, default: false },
    botId: { type: String, default: "" },
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
      lyricLines: [] as LyricLine[],
      lyricsLoading: false,
      lyricsTrackKey: "",
      localVolume: 50,
      volumeTimer: 0 as any,
    };
  },
  mounted() { this.frameId = requestAnimationFrame(() => this.tick()); },
  beforeDestroy() {
    cancelAnimationFrame(this.frameId);
    if (this.volumeTimer) clearTimeout(this.volumeTimer);
  },
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
        if (typeof state.volume === "number" && !this.volumeTimer) {
          this.localVolume = Math.max(0, Math.min(100, state.volume));
        }

        if (trackChanged) {
          this.renderedPosition = nextPosition;
          if (this.expanded) this.loadLyrics();
          else {
            this.lyricLines = [];
            this.lyricsTrackKey = "";
          }
        }
      },
    },
    expanded(value: boolean) {
      if (value) this.loadLyrics();
    },
    activeLyricIndex(index: number) {
      if (!this.expanded || index < 0) return;
      this.$nextTick(() => {
        const refs = this.$refs["lyric-" + index] as HTMLElement[] | HTMLElement | undefined;
        const el = Array.isArray(refs) ? refs[0] : refs;
        if (el && el.scrollIntoView) el.scrollIntoView({ block: "center", behavior: "smooth" });
      });
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
    loopMode(): string {
      const mode = (this.state.loop || "off").toLowerCase();
      return mode === "one" || mode === "all" ? mode : "off";
    },
    loopIcon(): string {
      if (this.loopMode === "one") return "①";
      if (this.loopMode === "all") return "∞";
      return "→";
    },
    loopTitle(): string {
      if (this.loopMode === "one") return "单曲循环";
      if (this.loopMode === "all") return "列表循环";
      return "顺序播放";
    },
    activeLyricIndex(): number {
      if (!this.lyricLines.length) return -1;
      const t = this.livePosition + 0.15;
      let idx = 0;
      for (let i = 0; i < this.lyricLines.length; i++) {
        if (this.lyricLines[i].time <= t) idx = i;
        else break;
      }
      return idx;
    },
  },
  methods: {
    trackKeyFor(state: MusicState): string {
      return state.current ? state.current.type + ":" + state.current.resource.resid : "";
    },
    async loadLyrics() {
      const state = this.state as MusicState;
      if (!state.current) {
        this.lyricLines = [];
        this.lyricsTrackKey = "";
        return;
      }
      const key = this.trackKeyFor(state);
      if (key === this.lyricsTrackKey && this.lyricLines.length) return;
      this.lyricsLoading = true;
      this.lyricsTrackKey = key;
      try {
        const path = "music/lyrics" + (this.botId ? ("?botId=" + encodeURIComponent(this.botId)) : "");
        const result = await consoleApi<{ available?: boolean; lines?: LyricLine[] }>(path);
        if (this.trackKeyFor(this.state as MusicState) !== key) return;
        this.lyricLines = (result.lines || []).filter((x) => x && x.text);
      } catch (_) {
        if (this.trackKeyFor(this.state as MusicState) === key) this.lyricLines = [];
      } finally {
        if (this.trackKeyFor(this.state as MusicState) === key) this.lyricsLoading = false;
      }
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

        if (Math.abs(drift) > 3) this.renderedPosition = target;
        else this.renderedPosition += drift * (1 - Math.exp(-8 * elapsed));

        if (state.length) this.renderedPosition = Math.min(state.length, Math.max(0, this.renderedPosition));
      }

      this.frameId = requestAnimationFrame(() => this.tick());
    },
    time(seconds: number) {
      return Math.floor(seconds / 60) + ":" + String(Math.floor(seconds % 60)).padStart(2, "0");
    },
    onVolumeInput(event: Event) {
      const value = Number((event.target as HTMLInputElement).value);
      this.localVolume = value;
      if (this.volumeTimer) clearTimeout(this.volumeTimer);
      this.volumeTimer = setTimeout(() => {
        this.volumeTimer = 0;
        this.$emit("volume", value);
      }, 120);
    },
    onVolumeCommit(event: Event) {
      const value = Number((event.target as HTMLInputElement).value);
      this.localVolume = value;
      if (this.volumeTimer) {
        clearTimeout(this.volumeTimer);
        this.volumeTimer = 0;
      }
      this.$emit("volume", value);
    },
    cycleLoop() {
      const order = ["off", "all", "one"];
      const current = this.loopMode;
      const next = order[(order.indexOf(current) + 1) % order.length];
      this.$emit("loop", next);
    },
    toggleRandom() {
      this.$emit("random", !this.state.random);
    },
  },
});
</script>

<style scoped lang="less">
.player-bar {
  position: fixed; z-index: 6; left: 220px; right: 0; bottom: 0; height: var(--console-player-h);
  display: flex; align-items: center; gap: 20px; padding: 12px 28px;
  background: rgba(255, 255, 255, 0.98); border-top: 1px solid var(--console-line);
  box-shadow: 0 -8px 24px rgba(30, 50, 55, 0.06);
  transition: height .32s var(--console-ease-out), gap .32s ease, padding .32s ease, background-color .32s ease;
}
button { border: 0; background: transparent; cursor: pointer; }
.track-summary {
  min-width: 220px; max-width: 32%; display: flex; align-items: center; gap: 12px;
  color: var(--console-ink); text-align: left;
}
.track-summary img, .cover-placeholder {
  width: 56px; height: 56px; flex: 0 0 56px; border-radius: var(--console-radius-sm); object-fit: cover;
  background: var(--console-brand-soft); display: grid; place-items: center; color: var(--console-brand-dark); font-size: 22px;
  transition: width .32s var(--console-ease-out), height .32s var(--console-ease-out), flex-basis .32s var(--console-ease-out), border-radius .32s ease;
}
.track-copy { min-width: 0; }
.track-copy b, .track-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-copy b { font-size: 14px; }
.track-copy small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.controls { display: flex; align-items: center; gap: 8px; }
.controls button {
  width: 38px; height: 38px; border-radius: 50%; color: #476170; font-size: 17px;
}
@media (hover: hover) and (pointer: fine) {
  .controls button:hover:not(:disabled) { background: var(--console-brand-soft); color: var(--console-brand-dark); }
}
.controls button:disabled { opacity: .5; cursor: wait; }
.controls .play-button {
  width: 50px; height: 50px; background: var(--console-brand); color: #fff; font-size: 19px;
  box-shadow: 0 6px 14px rgba(79, 184, 168, 0.28);
}
.controls .play-button:hover:not(:disabled) { background: var(--console-brand-dark); color: #fff; }
.timeline { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 140px; }
.progress-track { height: 4px; flex: 1; overflow: hidden; border-radius: 999px; background: #e4eaec; }
.progress-track i {
  display: block; width: 100%; height: 100%; background: var(--console-brand);
  transform-origin: left center; will-change: transform; transition: transform .08s linear;
}
.timeline small { color: var(--console-muted); font-size: 12px; white-space: nowrap; }
.mode-volume { display: flex; align-items: center; gap: 8px; flex: 0 0 auto; }
.mode-button {
  width: 34px; height: 34px; border-radius: 50%; color: #526b75; font-size: 14px; font-weight: 700;
}
.mode-button:hover:not(:disabled) { background: var(--console-brand-soft); color: var(--console-brand-dark); }
.mode-button.on { background: var(--console-brand-soft); color: var(--console-brand-dark); }
.mode-button:disabled { opacity: .5; cursor: wait; }
.volume { display: flex; align-items: center; gap: 6px; min-width: 118px; color: #6a7885; font-size: 12px; }
.volume span { width: 16px; text-align: center; color: var(--console-brand); }
.volume input[type="range"] {
  width: 88px; height: 4px; padding: 0; border: 0; border-radius: 999px; background: #e4eaec;
  accent-color: var(--console-brand); cursor: pointer;
}
.volume input[type="range"]:disabled { opacity: .5; cursor: wait; }
.volume em { min-width: 24px; font-style: normal; color: var(--console-muted); text-align: right; }
.queue-button {
  position: relative; width: 42px; height: 42px; border-radius: 50%; color: #526b75; font-size: 20px;
}
.queue-button:hover { background: var(--console-brand-soft); color: var(--console-brand-dark); }
.queue-button em {
  position: absolute; top: -2px; right: -2px; min-width: 17px; padding: 0 4px; border-radius: 10px;
  background: var(--console-brand-dark); color: #fff; font-size: 10px; font-style: normal; line-height: 16px; text-align: center;
}

.lyrics-panel { display: none; }
.player-bar.expanded {
  left: 220px; bottom: 0; height: 100vh; z-index: 10; flex-direction: column; justify-content: flex-start;
  gap: 12px; padding: 48px 28px 24px; background: #f3f8f7; overflow: hidden;
}
.expanded .track-summary { max-width: none; width: 100%; max-width: 720px; justify-content: flex-start; }
.expanded .track-summary img, .expanded .cover-placeholder {
  width: 72px; height: 72px; flex-basis: 72px; border-radius: var(--console-radius);
}
.expanded .track-copy b { font-size: 18px; }
.expanded .lyrics-panel {
  display: block; flex: 1; min-height: 0; width: 100%; max-width: 720px; margin: 0 auto;
  overflow: hidden; border-radius: var(--console-radius); background: rgba(255, 255, 255, 0.78);
  border: 1px solid rgba(229, 233, 238, 0.9);
}
.lyrics-scroll {
  height: 100%; overflow: auto; padding: 26vh 20px; scroll-behavior: smooth;
  -webkit-overflow-scrolling: touch;
}
.lyrics-line {
  margin: 0 0 14px; text-align: center; color: #8a98a5; font-size: 16px; line-height: 1.55;
  transition: color .2s ease, transform .2s ease, font-size .2s ease, font-weight .2s ease;
}
.lyrics-line.active { color: var(--console-brand-dark); font-size: 20px; font-weight: 700; transform: scale(1.02); }
.lyrics-empty { margin: 0; height: 100%; display: grid; place-items: center; color: var(--console-muted); font-size: 14px; }
.expanded .timeline, .expanded .mode-volume { width: 100%; max-width: 620px; flex: none; }
.expanded .mode-volume { justify-content: center; }
.expanded .volume input[type="range"] { width: 140px; }
.back-button {
  position: absolute; top: 20px; left: 24px; color: var(--console-brand-dark); font-size: 15px; font-weight: 600;
}
.back-button:hover { color: #185d55; }

@media (max-width: 760px) {
  .player-bar {
    left: 0;
    bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    height: 84px;
    display: grid;
    grid-template-columns: 1fr auto;
    grid-template-rows: 1fr auto;
    grid-template-areas:
      "summary controls"
      "modes queue";
    gap: 4px 8px;
    align-items: center;
    padding: 8px 12px 8px 12px;
  }
  .track-summary { grid-area: summary; min-width: 0; max-width: none; }
  .track-summary img, .cover-placeholder { width: 46px; height: 46px; flex-basis: 46px; }
  .controls { grid-area: controls; gap: 2px; justify-self: end; }
  .controls button { width: 34px; height: 34px; font-size: 15px; }
  .controls .play-button { width: 42px; height: 42px; font-size: 17px; }
  .timeline {
    display: block; position: absolute; left: 0; right: 0; top: 0; height: 2px;
    min-width: 0; padding: 0; margin: 0; pointer-events: none;
  }
  .timeline small { display: none; }
  .progress-track { height: 2px; border-radius: 0; }
  .mode-volume { grid-area: modes; gap: 4px; min-width: 0; }
  .mode-button { width: 30px; height: 30px; font-size: 13px; }
  .volume { min-width: 0; }
  .volume input[type="range"] { width: 72px; }
  .volume em { display: none; }
  .queue-button { grid-area: queue; width: 36px; height: 36px; justify-self: end; font-size: 18px; }

  .player-bar.expanded {
    left: 0;
    bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    height: calc(100vh - var(--console-nav-h) - env(safe-area-inset-bottom));
    display: flex; flex-direction: column; gap: 10px; padding: 44px 16px 16px;
  }
  .expanded .timeline {
    position: static; display: flex; width: 100%; max-width: 430px; height: auto; pointer-events: auto;
  }
  .expanded .timeline small { display: inline; }
  .expanded .progress-track { height: 4px; border-radius: 999px; }
  .expanded .track-summary img, .expanded .cover-placeholder { width: 56px; height: 56px; flex-basis: 56px; }
  .expanded .mode-volume { width: 100%; max-width: 430px; justify-content: center; }
  .expanded .volume em { display: inline; }
  .expanded .volume input[type="range"] { width: 120px; }
  .expanded .queue-button { justify-self: center; }
  .lyrics-scroll { padding: 22vh 12px; }
  .lyrics-line { font-size: 15px; }
  .lyrics-line.active { font-size: 18px; }
  .back-button { top: 14px; left: 14px; }
}

@media (prefers-reduced-motion: reduce) {
  .player-bar, .track-summary img, .cover-placeholder, .controls button, .queue-button, .back-button, .progress-track i, .lyrics-line { transition: none; }
  .lyrics-scroll { scroll-behavior: auto; }
}
</style>
