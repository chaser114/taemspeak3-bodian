<template>
  <section :class="['player-bar', { expanded }]">
    <div v-if="expanded" class="now-playing">
      <header class="np-top">
        <div class="np-top-side">
          <button type="button" class="np-icon-button" title="返回" aria-label="返回" @click="expanded = false">
            <b-icon icon="chevron-left" />
          </button>
          <div class="np-now">
            <span class="cover-sm"><img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle"><b-icon v-else icon="music-note" /></span>
            <span class="np-now-info"><b>{{ trackTitle }}</b><small>{{ artistTitle }}</small></span>
          </div>
        </div>
        <span class="np-center">正在播放</span>
        <div class="np-top-side np-top-actions">
          <button type="button" class="np-icon-button" title="切换深浅色" aria-label="切换深浅色" @click="toggleTheme">
            <b-icon :icon="currentTheme === 'dark' ? 'white-balance-sunny' : 'weather-night'" />
          </button>
          <button type="button" class="np-icon-button" title="返回播放器" aria-label="返回播放器" @click="expanded = false">
            <b-icon icon="close" />
          </button>
        </div>
      </header>

      <main class="np-main">
        <section class="np-left">
          <div class="big-cover">
            <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
            <span v-else><b-icon icon="music-note" size="is-large" /></span>
          </div>
          <div class="np-song">
            <b>{{ trackTitle }}</b>
            <small>{{ artistTitle }}</small>
          </div>
          <div class="np-controls" aria-label="播放控制">
            <button type="button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom"><b-icon icon="shuffle" /></button>
            <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')"><b-icon icon="skip-previous" /></button>
            <button type="button" class="play" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')"><b-icon :icon="showPlayIcon ? 'play' : 'pause'" /></button>
            <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')"><b-icon icon="skip-next" /></button>
            <button type="button" :class="{ on: loopMode !== 'off' }" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop"><b-icon :icon="loopModeIcon" /></button>
          </div>
          <div class="np-progress">
            <time>{{ time(livePosition) }}</time>
            <div class="np-track"><i :style="{ width: (progressRatio * 100) + '%' }"></i></div>
            <time>{{ time(state.length || 0) }}</time>
          </div>
          <label class="np-volume" :title="'音量 ' + Math.round(localVolume)">
            <b-icon icon="volume-high" />
            <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" @input="onVolumeInput" @change="onVolumeCommit">
          </label>
        </section>

        <section class="np-lyrics">
          <div class="lyrics-head">歌词</div>
          <div v-if="lyricsLoading" class="lyrics-box lyrics-empty">歌词加载中…</div>
          <div v-else-if="!lyricLines.length" class="lyrics-box lyrics-empty">暂无歌词</div>
          <div v-else class="lyrics-box">
            <p
              v-for="(line, index) in lyricLines"
              :key="index + '-' + line.time"
              :class="['lyric-line', { active: index === activeLyricIndex, before: index < activeLyricIndex }]"
              :ref="'lyric-' + index"
            >{{ line.text }}</p>
          </div>
        </section>

        <div class="np-bottom">
          <div class="np-progress">
            <time>{{ time(livePosition) }}</time>
            <div class="np-track"><i :style="{ width: (progressRatio * 100) + '%' }"></i></div>
            <time>{{ time(state.length || 0) }}</time>
          </div>
          <div class="np-controls" aria-label="播放控制">
            <button type="button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom"><b-icon icon="shuffle" /></button>
            <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')"><b-icon icon="skip-previous" /></button>
            <button type="button" class="play" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')"><b-icon :icon="showPlayIcon ? 'play' : 'pause'" /></button>
            <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')"><b-icon icon="skip-next" /></button>
            <button type="button" :class="{ on: loopMode !== 'off' }" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop"><b-icon :icon="loopModeIcon" /></button>
          </div>
          <label class="np-volume" :title="'音量 ' + Math.round(localVolume)">
            <b-icon icon="volume-high" />
            <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" @input="onVolumeInput" @change="onVolumeCommit">
          </label>
          <div class="np-toolbar">
            <button type="button" class="np-icon-button" title="播放队列" aria-label="播放队列" @click="$emit('queue')"><b-icon icon="playlist-music" /></button>
          </div>
        </div>
      </main>
    </div>

    <template v-else>
      <button type="button" class="track-summary" title="打开完整播放器" @click="expanded = true">
        <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
        <span v-else class="cover-placeholder"><b-icon icon="music-note" size="is-medium" /></span>
        <span class="track-copy"><b>{{ trackTitle }}</b><small>{{ state.current ? '点击查看歌词' : '搜索后即可加入待播' }}</small></span>
      </button>
      <div class="controls" aria-label="播放控制">
        <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')"><b-icon icon="skip-previous" /></button>
        <button type="button" class="play-button" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')"><b-icon :icon="showPlayIcon ? 'play' : 'pause'" size="is-medium" /></button>
        <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')"><b-icon icon="skip-next" /></button>
      </div>
      <div class="timeline"><div class="progress-track"><i :style="{ transform: 'scaleX(' + progressRatio + ')' }"></i></div><small>{{ time(livePosition) }} / {{ time(state.length || 0) }}</small></div>
      <div class="mode-volume" aria-label="播放模式与音量">
        <button type="button" class="mode-button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom"><b-icon icon="shuffle" size="is-small" /></button>
        <button type="button" class="mode-button" :class="{ on: loopMode !== 'off' }" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop"><b-icon :icon="loopModeIcon" size="is-small" /></button>
        <label class="volume" :title="'音量 ' + Math.round(localVolume)"><b-icon icon="volume-high" size="is-small" /><input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" @input="onVolumeInput" @change="onVolumeCommit"><em>{{ Math.round(localVolume) }}</em></label>
      </div>
      <button type="button" class="queue-button" title="待播队列" aria-label="待播队列" @click="$emit('queue')"><b-icon icon="playlist-music" /><em v-if="state.queue.length">{{ state.queue.length }}</em></button>
    </template>
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
    artistTitle(): string {
      return this.state.current ? (this.state.current.type || "歌曲") : "暂无播放";
    },
    currentTheme(): string {
      return document.documentElement.getAttribute("data-theme") || "light";
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
    toggleTheme() {
      const next = this.currentTheme === "dark" ? "light" : "dark";
      document.documentElement.setAttribute("data-theme", next);
      window.localStorage.setItem("bd-theme", next);
    },
  },
});
</script>

<style scoped lang="less">
button { border: 0; background: transparent; cursor: pointer; color: inherit; }
button:disabled { opacity: .45; cursor: wait; }
.player-bar { position: fixed; z-index: 6; left: calc(240px + 28px); right: 28px; bottom: 20px; height: 76px; display: flex; align-items: center; gap: 16px; padding: 10px 18px; border: 1px solid var(--console-line); border-radius: var(--console-radius-full); background: var(--console-glass); box-shadow: var(--console-shadow); backdrop-filter: blur(24px) saturate(150%); color: var(--console-ink); }
.player-bar.expanded { left: 0; right: 0; bottom: 0; width: 100%; height: 100vh; height: 100dvh; padding: 0; display: block; border: 0; border-radius: 0; background: var(--console-bg); box-shadow: none; backdrop-filter: none; }
.track-summary { min-width: 190px; max-width: 28%; display: flex; align-items: center; gap: 12px; color: var(--console-ink); text-align: left; }
.track-summary img, .cover-placeholder { width: 56px; height: 56px; flex: 0 0 56px; display: grid; place-items: center; border-radius: 10px; object-fit: cover; background: var(--console-surface-3); color: var(--console-brand); }
.track-copy { min-width: 0; }
.track-copy b, .track-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-copy b { font-size: 13px; font-weight: 700; }
.track-copy small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.controls { display: flex; align-items: center; gap: 6px; }
.controls button, .mode-button, .queue-button { width: 38px; height: 38px; display: grid; place-items: center; border-radius: 50%; color: var(--console-muted); }
.controls button:hover:not(:disabled), .mode-button:hover:not(:disabled), .queue-button:hover { background: var(--console-hover); color: var(--console-ink); }
.controls .play-button { width: 46px; height: 46px; background: var(--console-brand); color: #fff; }
.controls .play-button:hover:not(:disabled) { background: var(--console-brand-dark); color: #fff; }
.timeline { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 140px; }
.progress-track { height: 4px; flex: 1; overflow: hidden; border-radius: 999px; background: var(--console-surface-3); }
.progress-track i { display: block; width: 100%; height: 100%; background: var(--console-brand); transform-origin: left center; }
.timeline small { color: var(--console-muted); font-size: 12px; white-space: nowrap; }
.mode-volume { display: flex; align-items: center; gap: 4px; flex: 0 0 auto; }
.mode-button.on { color: var(--console-brand); background: var(--console-brand-soft); }
.volume { display: flex; align-items: center; gap: 6px; min-width: 118px; color: var(--console-muted); font-size: 12px; }
.volume input[type="range"], .np-volume input[type="range"] { width: 88px; accent-color: var(--console-brand); cursor: pointer; }
.volume em { min-width: 24px; font-style: normal; color: var(--console-muted); text-align: right; }
.queue-button { position: relative; width: 44px; height: 44px; }
.queue-button em { position: absolute; top: -2px; right: -2px; min-width: 17px; padding: 0 4px; border-radius: 10px; background: var(--console-brand); color: #fff; font-size: 10px; font-style: normal; line-height: 16px; text-align: center; }

.now-playing { position: absolute; inset: 0; display: flex; flex-direction: column; background: var(--console-bg); color: var(--console-ink); }
.np-top { height: 64px; flex: 0 0 auto; display: flex; align-items: center; justify-content: space-between; padding: 0 22px; }
.np-top-side { display: flex; align-items: center; gap: 4px; min-width: 84px; }
.np-top-actions { justify-content: flex-end; }
.np-center { margin: 0 auto; color: var(--console-muted); font-size: 12px; font-weight: 700; letter-spacing: .08em; }
.np-icon-button { width: 38px; height: 38px; display: grid; place-items: center; border-radius: 50%; color: var(--console-muted); }
.np-icon-button:hover { background: var(--console-hover); color: var(--console-ink); }
.np-now { display: none; align-items: center; gap: 10px; min-width: 0; }
.cover-sm { width: 42px; height: 42px; flex: 0 0 42px; display: grid; place-items: center; overflow: hidden; border-radius: 8px; background: var(--console-surface-2); color: var(--console-muted); }
.cover-sm img { width: 100%; height: 100%; object-fit: cover; }
.np-now-info { min-width: 0; }
.np-now-info b, .np-now-info small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.np-now-info b { font-size: 14px; }
.np-now-info small { margin-top: 2px; color: var(--console-muted); font-size: 12px; }
.np-main { flex: 1; min-height: 0; display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); align-items: center; gap: 56px; padding: 20px 56px 44px; overflow: hidden; }
.np-left { display: flex; flex-direction: column; align-items: center; justify-self: center; width: 100%; max-width: 560px; }
.big-cover { width: 34vw; max-width: 400px; aspect-ratio: 1; display: grid; place-items: center; overflow: hidden; border-radius: 18px; background: var(--console-surface-2); color: var(--console-muted); box-shadow: var(--console-shadow); }
.big-cover img { width: 100%; height: 100%; object-fit: cover; }
.np-song { margin-top: 28px; text-align: center; }
.np-song b, .np-song small { display: block; }
.np-song b { font-size: 27px; font-weight: 800; line-height: 1.2; }
.np-song small { margin-top: 5px; color: var(--console-muted); font-size: 15px; }
.np-controls { display: flex; align-items: center; gap: 10px; margin-top: 24px; }
.np-controls button { width: 42px; height: 42px; display: grid; place-items: center; border-radius: 50%; color: var(--console-muted); }
.np-controls button:hover:not(:disabled) { background: var(--console-hover); color: var(--console-ink); }
.np-controls button.on { color: var(--console-brand); }
.np-controls .play { width: 64px; height: 64px; margin: 0 10px; background: var(--console-ink); color: var(--console-bg); }
.np-progress { width: 100%; max-width: 500px; display: flex; align-items: center; gap: 12px; margin-top: 20px; }
.np-progress time { min-width: 34px; color: var(--console-muted-2); font-size: 11px; font-variant-numeric: tabular-nums; }
.np-progress time:last-child { text-align: right; }
.np-track { position: relative; flex: 1; height: 6px; overflow: hidden; border-radius: 999px; background: var(--console-surface-3); }
.np-track i { display: block; height: 100%; border-radius: inherit; background: var(--console-brand); }
.np-volume { display: flex; align-items: center; gap: 10px; margin-top: 22px; color: var(--console-muted); }
.np-volume input[type="range"] { width: 140px; }
.np-lyrics { align-self: center; width: 100%; max-width: 560px; justify-self: center; min-height: 0; }
.lyrics-head { margin-bottom: 6px; padding: 0 20px; color: var(--console-muted-2); font-size: 12px; font-weight: 700; letter-spacing: .1em; }
.lyrics-box { max-height: 560px; overflow: auto; padding: 8px 0; scrollbar-width: thin; }
.lyrics-empty { min-height: 220px; display: grid; place-items: center; color: var(--console-muted); font-size: 14px; }
.lyric-line { margin: 0; padding: 9px 20px; border-radius: 12px; color: var(--console-muted-2); font-size: 16.5px; font-weight: 500; line-height: 1.65; transition: color .2s ease, font-size .2s ease, font-weight .2s ease; }
.lyric-line.before { color: var(--console-muted); }
.lyric-line.active { margin: auto 0; color: var(--console-ink); font-size: 19px; font-weight: 800; }
.np-bottom { display: none; }

@media (max-width: 1023px) {
  .player-bar { left: 12px; right: 12px; bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom) + 10px); }
  .now-playing { position: fixed; z-index: 20; }
  .player-bar.expanded { left: 0; right: 0; bottom: 0; height: 100vh; height: 100dvh; }
  .np { background: linear-gradient(180deg, var(--console-surface-2) 0%, var(--console-bg) 42%); }
  .np-top { height: 58px; padding: 0 14px; }
  .np-top .np-top-side:first-child { flex: 1; min-width: 0; }
  .np-top .np-center { display: none; }
  .np-now { display: flex; }
  .np-main { display: flex; flex-direction: column; align-items: stretch; gap: 0; padding: 6px 24px calc(env(safe-area-inset-bottom) + 8px); overflow-y: auto; }
  .np-left { display: none; }
  .np-lyrics { width: 100%; flex: 1 1 auto; min-height: 0; margin-top: 2px; }
  .lyrics-head { display: none; }
  .lyrics-box { height: 100%; max-height: none; -webkit-mask-image: linear-gradient(180deg, transparent 0, #000 10%, #000 90%, transparent 100%); mask-image: linear-gradient(180deg, transparent 0, #000 10%, #000 90%, transparent 100%); }
  .lyric-line { padding: 11px 0; color: var(--console-muted-2); font-size: 16px; line-height: 1.55; }
  .lyric-line.active { margin: 4px 0; color: var(--console-ink); font-size: 26px; }
  .np-bottom { display: flex; flex-direction: column; padding-top: 2px; }
  .np-bottom .np-progress { margin-top: 0; max-width: none; }
  .np-bottom .np-track { height: 4px; }
  .np-bottom .np-controls { margin-top: 10px; justify-content: center; }
  .np-bottom .np-controls button { width: 40px; height: 40px; }
  .np-bottom .np-controls .play { width: 56px; height: 56px; margin: 0 8px; background: var(--console-ink); color: var(--console-bg); }
  .np-bottom .np-volume { justify-content: center; margin-top: 14px; }
  .np-bottom .np-volume input[type="range"] { width: 110px; }
  .np-toolbar { display: flex; align-items: center; justify-content: center; margin-top: 10px; }
}
@media (max-width: 560px) {
  .player-bar { left: 8px; right: 8px; bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom) + 8px); }
  .player-bar:not(.expanded) { display: grid; grid-template-columns: minmax(0, 1fr) 108px; grid-template-rows: 43px 25px; grid-template-areas: "summary controls" "modes queue"; gap: 4px 8px; padding: 9px 12px; }
  .player-bar:not(.expanded) .track-summary { grid-area: summary; min-width: 0; max-width: none; }
  .player-bar:not(.expanded) .track-summary img, .player-bar:not(.expanded) .cover-placeholder { width: 48px; height: 48px; flex-basis: 48px; }
  .player-bar:not(.expanded) .controls { grid-area: controls; justify-self: end; gap: 0; }
  .player-bar:not(.expanded) .controls button { width: 30px; height: 36px; }
  .player-bar:not(.expanded) .controls .play-button { width: 40px; height: 40px; }
  .player-bar:not(.expanded) .timeline { position: absolute; top: 0; left: 0; right: 0; display: block; min-width: 0; height: 2px; pointer-events: none; }
  .player-bar:not(.expanded) .timeline small { display: none; }
  .player-bar:not(.expanded) .progress-track { height: 2px; border-radius: 0; }
  .player-bar:not(.expanded) .mode-volume { grid-area: modes; min-width: 0; }
  .player-bar:not(.expanded) .queue-button { grid-area: queue; justify-self: end; width: 36px; height: 36px; }
  .np-main { padding-left: 24px; padding-right: 24px; }
  .np-top-actions .np-icon-button:last-child { display: none; }
  .np-controls { gap: 4px; }
  .np-toolbar { padding-bottom: env(safe-area-inset-bottom); }
}
</style>
