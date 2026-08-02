<template>
  <section :class="['player-bar', { expanded }]">
    <button v-if="expanded" type="button" class="back-button" title="返回播放器" @click="expanded = false">
      <b-icon icon="chevron-left" size="is-small" /> 返回
    </button>

    <button type="button" class="track-summary" title="打开完整播放器" @click="expanded = !expanded">
      <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
      <span v-else class="cover-placeholder"><b-icon icon="music-note" size="is-medium" /></span>
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
      <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')">
        <b-icon icon="skip-previous" />
      </button>
      <button type="button" class="play-button" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')">
        <b-icon :icon="showPlayIcon ? 'play' : 'pause'" size="is-medium" />
      </button>
      <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')">
        <b-icon icon="skip-next" />
      </button>
    </div>

    <div class="timeline">
      <div class="progress-track"><i :style="{ transform: 'scaleX(' + progressRatio + ')' }"></i></div>
      <small>{{ time(livePosition) }} / {{ time(state.length || 0) }}</small>
    </div>

    <div class="mode-volume" aria-label="播放模式与音量">
      <button type="button" class="mode-button" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop">
        <b-icon :icon="loopModeIcon" size="is-small" />
      </button>
      <button type="button" class="mode-button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom">
        <b-icon icon="shuffle" size="is-small" />
      </button>
      <label class="volume" :title="'音量 ' + Math.round(localVolume)">
        <b-icon icon="volume-high" size="is-small" />
        <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" @input="onVolumeInput" @change="onVolumeCommit">
        <em>{{ Math.round(localVolume) }}</em>
      </label>
    </div>

    <button type="button" class="queue-button" title="待播队列" aria-label="待播队列" @click="$emit('queue')">
      <b-icon icon="playlist-music" />
      <em v-if="state.queue.length">{{ state.queue.length }}</em>
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
    loopModeIcon(): string {
      if (this.loopMode === "one") return "repeat-once";
      if (this.loopMode === "all") return "repeat";
      return "arrow-right";
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
  display: flex; align-items: center; gap: 20px; padding: 16px 28px;
  background: var(--console-surface); border-top: 1px solid var(--console-line);
  box-shadow: 0 -4px 16px rgba(30, 50, 55, 0.08);
  backdrop-filter: blur(10px);
  transition: height .32s var(--console-ease-out), gap .32s ease, padding .32s ease, background-color .32s ease;
}
button { border: 0; background: transparent; cursor: pointer; }
.track-summary {
  min-width: 220px; max-width: 32%; display: flex; align-items: center; gap: 12px;
  color: var(--console-ink); text-align: left;
}
.track-summary img, .cover-placeholder {
  width: 72px; height: 72px; flex: 0 0 72px; border-radius: var(--console-radius-sm); object-fit: cover;
  background: var(--console-brand-soft); display: grid; place-items: center; color: var(--console-brand-dark);
  box-shadow: var(--console-shadow-sm);
  transition: width .32s var(--console-ease-out), height .32s var(--console-ease-out), flex-basis .32s var(--console-ease-out), border-radius .32s ease;
}
.track-copy { min-width: 0; }
.track-copy b, .track-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-copy b { font-size: 15px; font-weight: 600; }
.track-copy small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.controls { display: flex; align-items: center; gap: 8px; }
.controls button {
  width: 40px; height: 40px; display: grid; place-items: center; padding: 0;
  border-radius: 50%; color: #476170; line-height: 1;
}
@media (hover: hover) and (pointer: fine) {
  .controls button:hover:not(:disabled) {
    background: var(--console-brand-soft);
    color: var(--console-brand-dark);
    transform: translateY(-1px);
  }
}
.controls button:disabled { opacity: .5; cursor: wait; }
.controls .play-button {
  width: 56px; height: 56px; background: var(--console-brand); color: #fff; font-size: 20px;
  box-shadow: none;
}
.controls .play-button:hover:not(:disabled) {
  background: var(--console-brand-dark);
  color: #fff;
  box-shadow: none;
  transform: translateY(-1px);
}
.timeline { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 140px; }
.progress-track { height: 6px; flex: 1; overflow: hidden; border-radius: 999px; background: #e4eaec; }
.progress-track i {
  display: block; width: 100%; height: 100%; background: var(--console-brand);
  transform-origin: left center; will-change: transform; transition: transform .08s linear;
}
.timeline small { color: var(--console-muted); font-size: 12px; white-space: nowrap; }
.mode-volume { display: flex; align-items: center; gap: 8px; flex: 0 0 auto; }
.mode-button {
  width: 36px; height: 36px; display: grid; place-items: center; border-radius: 50%; color: #526b75;
}
.mode-button:hover:not(:disabled) {
  background: var(--console-brand-soft);
  color: var(--console-brand-dark);
  transform: translateY(-1px);
}
.mode-button.on { background: var(--console-brand-soft); color: var(--console-brand-dark); }
.mode-button:disabled { opacity: .5; cursor: wait; }
.volume { display: flex; align-items: center; gap: 6px; min-width: 118px; color: #6a7885; font-size: 12px; }
.volume .icon { color: var(--console-brand); }
.volume input[type="range"] {
  width: 88px; height: 4px; padding: 0; border: 0; border-radius: 999px; background: #e4eaec;
  accent-color: var(--console-brand); cursor: pointer;
}
.volume input[type="range"]:disabled { opacity: .5; cursor: wait; }
.volume em { min-width: 24px; font-style: normal; color: var(--console-muted); text-align: right; }
.queue-button {
  position: relative; width: 44px; height: 44px; border-radius: 50%; color: #526b75;
}
.queue-button:hover {
  background: var(--console-brand-soft);
  color: var(--console-brand-dark);
  transform: translateY(-1px);
}
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
  overscroll-behavior: contain;
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
  position: absolute; z-index: 2; top: 20px; left: 24px; min-height: 44px; padding: 8px 12px;
  display: inline-flex; align-items: center; gap: 6px; border-radius: var(--console-radius-sm);
  color: var(--console-brand-dark); font-weight: 600;
}
.back-button:hover { color: #185d55; }

@media (max-width: 760px) {
  .player-bar {
    left: 0;
    bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    height: 88px;
    display: grid;
    grid-template-columns: 1fr auto;
    grid-template-rows: 1fr auto;
    grid-template-areas:
      "summary controls"
      "modes queue";
    gap: 4px 8px;
    align-items: center;
    padding: 10px 12px 10px 12px;
  }
  .track-summary { grid-area: summary; min-width: 0; max-width: none; }
  .track-summary img, .cover-placeholder { width: 52px; height: 52px; flex-basis: 52px; }
  .controls { grid-area: controls; gap: 2px; justify-self: end; }
  .controls button { width: 34px; height: 34px; }
  .controls .play-button { width: 42px; height: 42px; }
  .timeline {
    display: block; position: absolute; left: 0; right: 0; top: 0; height: 2px;
    min-width: 0; padding: 0; margin: 0; pointer-events: none;
  }
  .timeline small { display: none; }
  .progress-track { height: 2px; border-radius: 0; }
  .mode-volume { grid-area: modes; gap: 4px; min-width: 0; }
  .mode-button { width: 30px; height: 30px; }
  .volume { min-width: 0; }
  .volume input[type="range"] { width: 72px; }
  .volume em { display: none; }
  .queue-button { grid-area: queue; width: 36px; height: 36px; justify-self: end; }

  .player-bar.expanded {
    left: 0;
    bottom: 0;
    height: 100vh;
    height: 100dvh;
    display: flex; flex-direction: column; gap: 10px;
    padding: calc(44px + env(safe-area-inset-top)) 16px calc(16px + env(safe-area-inset-bottom));
  }
  .expanded .timeline {
    position: static; display: flex; width: 100%; max-width: 430px; height: auto; pointer-events: auto;
  }
  .expanded .timeline small { display: inline; }
  .expanded .progress-track { height: 4px; border-radius: 999px; }
  .expanded .track-summary img, .expanded .cover-placeholder { width: 64px; height: 64px; flex-basis: 64px; }
  .expanded .mode-volume { width: 100%; max-width: 430px; justify-content: center; }
  .expanded .volume em { display: inline; }
  .expanded .volume input[type="range"] { width: 120px; }
  .expanded .queue-button { justify-self: center; }
  .lyrics-scroll { padding: 22vh 12px; }
  .lyrics-line { font-size: 15px; }
  .lyrics-line.active { font-size: 18px; }
  .back-button { top: env(safe-area-inset-top); left: 10px; }
}

@media (prefers-reduced-motion: reduce) {
  .player-bar, .track-summary img, .cover-placeholder, .controls button, .queue-button, .back-button, .progress-track i, .lyrics-line { transition: none; }
  .lyrics-scroll { scroll-behavior: auto; }
}

/* Apple Music style overrides: the compact player floats above content; lyrics become a dedicated surface. */
.player-bar {
  left: calc(240px + 28px); right: 28px; bottom: 20px; height: 76px; padding: 10px 18px;
  gap: 16px; border: 1px solid var(--console-line); border-radius: var(--console-radius-full);
  background: var(--console-glass); box-shadow: var(--console-shadow); backdrop-filter: blur(24px) saturate(150%);
}
.track-summary { min-width: 190px; max-width: 28%; }
.track-summary img, .cover-placeholder { width: 56px; height: 56px; flex-basis: 56px; border-radius: 10px; background: var(--console-surface-3); color: var(--console-brand); box-shadow: none; }
.track-copy b { color: var(--console-ink); font-size: 13px; }
.track-copy small { color: var(--console-muted); }
.controls button, .mode-button, .queue-button { color: var(--console-muted); }
.controls button:hover:not(:disabled), .mode-button:hover:not(:disabled), .queue-button:hover { background: var(--console-hover); color: var(--console-ink); transform: none; }
.controls .play-button { width: 46px; height: 46px; background: var(--console-brand); box-shadow: none; }
.controls .play-button:hover:not(:disabled) { background: var(--console-brand-dark); box-shadow: none; transform: none; }
.progress-track { height: 4px; background: var(--console-surface-3); }
.volume input[type="range"] { background: var(--console-surface-3); }
.queue-button em { background: var(--console-brand); }

.player-bar.expanded {
  left: 0; right: 0; bottom: 0; height: 100vh; height: 100dvh; padding: 56px 7vw 36px;
  display: grid; grid-template-columns: minmax(270px, .38fr) minmax(0, .62fr); grid-template-rows: auto 1fr auto auto auto;
  align-items: center; column-gap: 7vw; row-gap: 18px; border: 0; border-radius: 0; background: var(--console-bg); box-shadow: none; backdrop-filter: none;
}
.expanded .track-summary { grid-column: 1; grid-row: 1 / span 2; width: 100%; max-width: 360px; align-self: center; display: flex; flex-direction: column; align-items: flex-start; gap: 18px; }
.expanded .track-summary img, .expanded .cover-placeholder { width: 31vw; max-width: 360px; height: 31vw; max-height: 360px; flex-basis: auto; border-radius: 14px; background: var(--console-surface-2); }
.expanded .track-copy b { font-size: 21px; }
.expanded .track-copy small { margin-top: 6px; font-size: 13px; }
.expanded .lyrics-panel { grid-column: 2; grid-row: 1 / span 5; width: 100%; max-width: none; height: 100%; margin: 0; border: 0; border-left: 1px solid var(--console-line); border-radius: 0; background: transparent; }
.expanded .lyrics-scroll { padding: 28vh 4vw 26vh; }
.lyrics-line { text-align: left; color: var(--console-muted); font-size: 20px; line-height: 1.5; }
.lyrics-line.active { color: var(--console-ink); font-size: 28px; transform: none; }
.expanded .controls { grid-column: 1; grid-row: 3; justify-content: flex-start; }
.expanded .timeline { grid-column: 1; grid-row: 4; width: 100%; max-width: 360px; }
.expanded .mode-volume { grid-column: 1; grid-row: 5; width: 100%; max-width: 360px; justify-content: flex-start; }
.expanded .queue-button { grid-column: 1; grid-row: 5; justify-self: end; align-self: center; }
.back-button { top: 18px; left: 28px; color: var(--console-muted); }
.back-button:hover { color: var(--console-ink); }

@media (max-width: 1023px) {
  .player-bar { left: 12px; right: 12px; bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom) + 10px); height: 78px; display: grid; grid-template-columns: minmax(0, 1fr) auto; grid-template-rows: 1fr auto; grid-template-areas: "summary controls" "modes queue"; gap: 4px 8px; align-items: center; padding: 9px 12px; border-radius: 20px; }
  .track-summary { grid-area: summary; min-width: 0; max-width: none; width: auto; }
  .track-summary img, .cover-placeholder { width: 48px; height: 48px; flex-basis: 48px; }
  .controls { grid-area: controls; gap: 2px; justify-self: end; }
  .timeline { display: block; position: absolute; left: 0; right: 0; top: 0; height: 2px; min-width: 0; padding: 0; margin: 0; pointer-events: none; }
  .timeline small { display: none; }
  .progress-track { height: 2px; border-radius: 0; }
  .mode-volume { grid-area: modes; gap: 4px; min-width: 0; }
  .queue-button { grid-area: queue; width: 36px; height: 36px; justify-self: end; }
  .player-bar.expanded { left: 0; right: 0; bottom: 0; height: 100vh; height: 100dvh; display: flex; flex-direction: column; gap: 10px; padding: calc(54px + env(safe-area-inset-top)) 18px calc(18px + env(safe-area-inset-bottom)); border-radius: 0; }
  .expanded .track-summary { width: 100%; max-width: 430px; flex-direction: row; align-items: center; gap: 14px; }
  .expanded .track-summary img, .expanded .cover-placeholder { width: 62px; height: 62px; flex-basis: 62px; border-radius: 10px; }
  .expanded .lyrics-panel { width: 100%; max-width: 430px; flex: 1; min-height: 0; border: 0; }
  .expanded .lyrics-scroll { padding: 18vh 2px 20vh; }
  .lyrics-line { text-align: left; font-size: 17px; margin-bottom: 16px; }
  .lyrics-line.active { font-size: 22px; }
  .expanded .controls, .expanded .timeline, .expanded .mode-volume { width: 100%; max-width: 430px; flex: none; }
  .expanded .controls { justify-content: center; }
  .expanded .timeline { display: flex; }
  .expanded .mode-volume { justify-content: center; }
  .expanded .queue-button { position: absolute; right: 18px; bottom: calc(14px + env(safe-area-inset-bottom)); }
  .back-button { top: calc(10px + env(safe-area-inset-top)); left: 10px; }
}

@media (max-width: 560px) {
  .player-bar:not(.expanded) { display: grid; overflow: hidden; grid-template-columns: minmax(0, 1fr) 108px; grid-template-rows: 43px 25px; grid-template-areas: "summary controls" "modes queue"; }
  .player-bar:not(.expanded) .track-summary { position: static; grid-area: summary; min-width: 0; width: 100%; max-width: none; }
  .player-bar:not(.expanded) .controls { position: absolute !important; top: 9px; right: 6px; grid-area: auto; display: flex !important; width: 108px !important; min-width: 108px !important; height: 43px; justify-self: auto; justify-content: space-between; z-index: 2; }
  .player-bar:not(.expanded) .controls button { position: static !important; width: 30px !important; min-width: 30px !important; height: 36px !important; padding: 0; flex: 0 0 30px; }
  .player-bar:not(.expanded) .controls .play-button { width: 40px !important; min-width: 40px !important; height: 40px !important; flex-basis: 40px; }
  .player-bar:not(.expanded) .mode-volume { position: static; grid-area: modes; min-width: 0; width: 100%; max-width: none; display: flex; }
  .player-bar:not(.expanded) .queue-button { position: static; grid-area: queue; width: 36px; min-width: 36px; height: 36px; justify-self: end; }
}

@media (max-width: 560px) {
  .player-bar { left: 8px; right: 8px; bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom) + 8px); }
  .track-copy b { max-width: 145px; }
  .controls { gap: 0; }
  .controls button { width: 32px; height: 32px; }
  .controls .play-button { width: 40px; height: 40px; }
  .mode-volume { gap: 2px; }
  .volume input[type="range"] { width: 64px; }
}
</style>
