<template>
  <section :class="['player-bar', { expanded }]">
    <!-- ============ full screen now playing ============ -->
    <div v-if="expanded" class="stage">
      <div class="stage-wash" aria-hidden="true"></div>
      <div class="stage-veil" aria-hidden="true"></div>

      <header class="stage-top">
        <button type="button" class="stage-icon" title="收起播放器" aria-label="收起播放器" @click="expanded = false">
          <b-icon icon="chevron-down" />
        </button>
        <span class="stage-label">正在播放</span>
        <div class="stage-top-right">
          <button type="button" class="stage-icon" :title="'待播队列（' + queueCount + '）'" aria-label="待播队列" @click="$emit('queue')">
            <b-icon icon="playlist-music" />
          </button>
          <button type="button" class="stage-icon" title="关闭" aria-label="关闭" @click="expanded = false">
            <b-icon icon="close" />
          </button>
        </div>
      </header>

      <div class="stage-body">
        <section class="stage-left">
          <div class="stage-cover">
            <img v-if="cover" :src="cover" :alt="trackTitle">
            <span v-else><b-icon icon="music-note" size="is-large" /></span>
          </div>
          <div class="stage-meta">
            <b>{{ trackTitle }}</b>
            <span>{{ subtitle }}</span>
          </div>
        </section>

        <section class="stage-lyrics" ref="lyricBox">
          <p v-if="lyricsLoading" class="lyric-empty">歌词加载中…</p>
          <p v-else-if="!lyricLines.length" class="lyric-empty">暂无歌词</p>
          <template v-else>
            <p
              v-for="(line, index) in lyricLines"
              :key="index + '-' + line.time"
              :class="['lyric-line', { active: index === activeLyricIndex, past: index < activeLyricIndex }]"
              :ref="'lyric-' + index"
              :tabindex="canSeek ? 0 : undefined"
              :role="canSeek ? 'button' : undefined"
              @click="seekTo(line.time)"
              @keydown.enter.prevent="seekTo(line.time)"
              @keydown.space.prevent="seekTo(line.time)"
            >{{ line.text }}</p>
          </template>
        </section>
      </div>

      <footer class="stage-foot">
        <div class="stage-controls">
          <button type="button" :class="{ on: !!state.random }" title="随机播放" aria-label="随机播放" :disabled="busy" @click="toggleRandom"><b-icon icon="shuffle" /></button>
          <button type="button" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')"><b-icon icon="skip-previous" /></button>
          <button type="button" class="stage-play" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')">
            <b-icon :icon="showPlayIcon ? 'play' : 'pause'" />
          </button>
          <button type="button" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')"><b-icon icon="skip-next" /></button>
          <button type="button" :class="{ on: loopMode !== 'off' }" :title="loopTitle" :aria-label="loopTitle" :disabled="busy" @click="cycleLoop">
            <b-icon :icon="loopModeIcon" />
          </button>
        </div>

        <div class="stage-timeline">
          <time>{{ time(displayPosition) }}</time>
          <div
            class="seek-bar"
            :class="{ disabled: !canSeek, dragging: dragging }"
            ref="stageSeek"
            role="slider"
            tabindex="0"
            :aria-valuemin="0"
            :aria-valuemax="Math.round(state.length || 0)"
            :aria-valuenow="Math.round(displayPosition)"
            aria-label="播放进度"
            @pointerdown="onPointerDown($event, 'stage')"
            @pointermove="onPointerMove($event, 'stage')"
            @pointerup="onPointerUp($event, 'stage')"
            @pointercancel="onPointerUp($event, 'stage')"
            @keydown.left.prevent="nudge(-5)"
            @keydown.right.prevent="nudge(5)"
          >
            <span class="seek-rail"><i :style="{ width: (displayProgressRatio * 100) + '%' }"></i></span>
            <span class="seek-knob" :style="{ left: (displayProgressRatio * 100) + '%' }"></span>
          </div>
          <time>{{ time(state.length || 0) }}</time>
        </div>

        <label class="stage-volume" :title="'音量 ' + Math.round(localVolume)">
          <b-icon icon="volume-high" size="is-small" />
          <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" aria-label="音量" @input="onVolumeInput" @change="onVolumeCommit">
        </label>
      </footer>
    </div>

    <!-- ============ compact bar ============ -->
    <template v-else>
      <button type="button" class="now-playing" title="打开完整播放器" @click="expanded = true">
        <span class="np-cover">
          <img v-if="cover" :src="cover" :alt="trackTitle">
          <b-icon v-else icon="music-note" size="is-medium" />
        </span>
        <span class="np-copy">
          <b>{{ trackTitle }}</b>
          <span>{{ state.current ? subtitle : '搜索后即可加入待播' }}</span>
        </span>
      </button>

      <div class="bar-controls">
        <button type="button" class="ctrl" title="随机播放" aria-label="随机播放" :class="{ on: !!state.random }" :disabled="busy" @click="toggleRandom">
          <b-icon icon="shuffle" size="is-small" />
        </button>
        <button type="button" class="ctrl" title="上一首" aria-label="上一首" :disabled="!canControl" @click="$emit('previous')">
          <b-icon icon="skip-previous" />
        </button>
        <button type="button" class="ctrl play" :title="pauseTitle" :aria-label="pauseTitle" :disabled="!canControl" @click="$emit('pause')">
          <b-icon :icon="showPlayIcon ? 'play' : 'pause'" size="is-medium" />
        </button>
        <button type="button" class="ctrl" title="下一首" aria-label="下一首" :disabled="!canSkip" @click="$emit('next')">
          <b-icon icon="skip-next" />
        </button>
        <button type="button" class="ctrl" :title="loopTitle" :aria-label="loopTitle" :class="{ on: loopMode !== 'off' }" :disabled="busy" @click="cycleLoop">
          <b-icon :icon="loopModeIcon" size="is-small" />
        </button>
      </div>

      <div class="bar-timeline">
        <time>{{ time(displayPosition) }}</time>
        <div
          class="seek-bar"
          :class="{ disabled: !canSeek, dragging: dragging }"
          ref="barSeek"
          role="slider"
          tabindex="0"
          :aria-valuemin="0"
          :aria-valuemax="Math.round(state.length || 0)"
          :aria-valuenow="Math.round(displayPosition)"
          aria-label="播放进度"
          @pointerdown="onPointerDown($event, 'bar')"
          @pointermove="onPointerMove($event, 'bar')"
          @pointerup="onPointerUp($event, 'bar')"
          @pointercancel="onPointerUp($event, 'bar')"
          @keydown.left.prevent="nudge(-5)"
          @keydown.right.prevent="nudge(5)"
        >
          <span class="seek-rail"><i :style="{ width: (displayProgressRatio * 100) + '%' }"></i></span>
          <span class="seek-knob" :style="{ left: (displayProgressRatio * 100) + '%' }"></span>
        </div>
        <time>{{ time(state.length || 0) }}</time>
      </div>

      <div class="bar-right">
        <label class="bar-volume" :title="'音量 ' + Math.round(localVolume)">
          <b-icon icon="volume-high" size="is-small" />
          <input type="range" min="0" max="100" step="1" :value="localVolume" :disabled="busy" aria-label="音量" @input="onVolumeInput" @change="onVolumeCommit">
        </label>
        <button type="button" class="queue-button" :title="'待播队列（' + queueCount + '）'" aria-label="待播队列" @click="$emit('queue')">
          <b-icon icon="playlist-music" />
          <em v-if="queueCount">{{ queueCount }}</em>
        </button>
      </div>
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
      seekDraft: null as number | null,
      dragging: false,
      localVolume: 50,
      volumeTimer: 0 as any,
    };
  },
  mounted() {
    this.frameId = requestAnimationFrame(() => this.tick());
    window.addEventListener("resize", this.onResize as any);
  },
  beforeDestroy() {
    cancelAnimationFrame(this.frameId);
    if (this.volumeTimer) clearTimeout(this.volumeTimer);
    window.removeEventListener("resize", this.onResize as any);
    this.endDrag();
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
          this.seekDraft = null;
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
    busy(value: boolean) {
      if (!value) this.seekDraft = null;
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
    cover(): string {
      return this.state.current && this.state.current.coverUrl ? this.state.current.coverUrl : "";
    },
    queueCount(): number {
      return this.state.queue ? this.state.queue.length : 0;
    },
    livePosition(): number {
      const state = this.state as MusicState;
      if (!state.current) return 0;
      return state.length ? Math.min(state.length, this.renderedPosition) : this.renderedPosition;
    },
    displayPosition(): number {
      const state = this.state as MusicState;
      if (this.seekDraft === null) return this.livePosition;
      return state.length ? Math.min(state.length, Math.max(0, this.seekDraft)) : Math.max(0, this.seekDraft);
    },
    displayProgressRatio(): number {
      const state = this.state as MusicState;
      return state.length ? Math.min(1, Math.max(0, this.displayPosition / state.length)) : 0;
    },
    trackTitle(): string {
      return this.state.current ? this.state.current.title : "等待点歌";
    },
    subtitle(): string {
      if (!this.state.current) return "暂无播放";
      const add = this.state.current.resource.add || {};
      const artist = add.artist || add.singer || "";
      return artist ? artist : "歌曲";
    },
    canControl(): boolean {
      return !this.busy && !!this.state.current;
    },
    canSeek(): boolean {
      return this.canControl && !!this.state.length;
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
      const total = Math.max(0, Math.floor(seconds || 0));
      return Math.floor(total / 60) + ":" + String(total % 60).padStart(2, "0");
    },

    /* ---------- seeking (click / drag / keyboard) ---------- */
    seekTargetOf($event: PointerEvent, refName: "bar" | "stage"): number | null {
      const el = this.$refs[refName === "bar" ? "barSeek" : "stageSeek"] as HTMLElement | undefined;
      const length = Number(this.state.length) || 0;
      if (!el || !length) return null;
      const rect = el.getBoundingClientRect();
      if (rect.width <= 0) return null;
      const ratio = Math.min(1, Math.max(0, ($event.clientX - rect.left) / rect.width));
      return ratio * length;
    },
    onPointerDown($event: PointerEvent, refName: "bar" | "stage") {
      if (!this.canSeek) return;
      const target = this.seekTargetOf($event, refName);
      if (target === null) return;
      this.dragging = true;
      this.seekDraft = target;
      const el = $event.currentTarget as HTMLElement;
      if (el && el.setPointerCapture) {
        try { el.setPointerCapture($event.pointerId); } catch (_) { /* ignore */ }
      }
    },
    onPointerMove($event: PointerEvent, refName: "bar" | "stage") {
      if (!this.dragging) return;
      const target = this.seekTargetOf($event, refName);
      if (target !== null) this.seekDraft = target;
    },
    onPointerUp($event: PointerEvent, refName: "bar" | "stage") {
      if (!this.dragging) return;
      const target = this.seekTargetOf($event, refName);
      this.endDrag();
      const value = target === null ? this.seekDraft : target;
      if (value === null) return;
      if (!this.canSeek) return;
      this.seekDraft = value;
      this.$emit("seek", value);
      // Keep the optimistic position until the next poll confirms it.
      this.renderedPosition = value;
      this.serverPosition = value;
      this.syncedAt = Date.now();
    },
    endDrag() {
      this.dragging = false;
    },
    onResize() {
      if (this.dragging) this.endDrag();
    },
    nudge(delta: number) {
      if (!this.canSeek) return;
      const length = Number(this.state.length) || 0;
      const next = Math.min(length, Math.max(0, this.displayPosition + delta));
      this.seekDraft = next;
      this.renderedPosition = next;
      this.serverPosition = next;
      this.syncedAt = Date.now();
      this.$emit("seek", next);
    },
    seekTo(position: number) {
      if (!this.canSeek || !Number.isFinite(position)) return;
      const length = Number(this.state.length) || 0;
      const next = Math.max(0, Math.min(length, position));
      this.seekDraft = next;
      this.renderedPosition = next;
      this.serverPosition = next;
      this.syncedAt = Date.now();
      this.$emit("seek", next);
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
button { border: 0; background: transparent; cursor: pointer; color: inherit; }
button:disabled { opacity: .42; cursor: not-allowed; }
time { font-variant-numeric: tabular-nums; }

/* ================= compact bar ================= */
.player-bar {
  position: fixed; z-index: 6; inset: auto 0 0 0;
  height: var(--console-player-h);
  display: grid;
  grid-template-columns: minmax(180px, 1fr) auto minmax(220px, 1.4fr) auto;
  align-items: center; gap: 18px;
  padding: 0 22px;
  background: var(--console-glass);
  border-top: 1px solid var(--console-line);
  backdrop-filter: blur(26px) saturate(180%);
  color: var(--console-ink);
}
.player-bar.expanded {
  inset: 0; height: 100vh; height: 100dvh;
  display: block; padding: 0;
  width: 100vw; max-width: 100vw;
  overflow: hidden;
  background: #0b0b0d; border: 0; color: #fff;
}

.now-playing {
  min-width: 0; display: flex; align-items: center; gap: 12px;
  padding: 0; text-align: left;
}
.np-cover {
  width: 52px; height: 52px; flex: 0 0 auto;
  display: grid; place-items: center; overflow: hidden;
  border-radius: 6px; background: var(--console-surface-3); color: var(--console-brand);
}
.np-cover img { width: 100%; height: 100%; object-fit: cover; }
.np-copy { min-width: 0; }
.np-copy b, .np-copy span { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.np-copy b { font-size: 13.5px; font-weight: 600; }
.np-copy span { margin-top: 2px; color: var(--console-muted); font-size: 12px; }

.bar-controls { display: flex; align-items: center; gap: 12px; }
.ctrl { width: 30px; height: 30px; display: grid; place-items: center; border-radius: 50%; color: var(--console-ink); opacity: .82; }
.ctrl:hover:not(:disabled) { opacity: 1; background: var(--console-hover); }
.ctrl.on { color: var(--console-brand); opacity: 1; }
.ctrl.play { width: 36px; height: 36px; opacity: 1; }
.ctrl.play:hover:not(:disabled) { background: var(--console-hover); }

.bar-timeline { display: flex; align-items: center; gap: 9px; min-width: 0; }
.bar-timeline time { flex: 0 0 auto; color: var(--console-muted-2); font-size: 10.5px; }

/* ---------- clickable seek bar ---------- */
.seek-bar {
  position: relative; flex: 1; min-width: 60px;
  height: 16px; display: flex; align-items: center;
  cursor: pointer; touch-action: none; outline: none;
}
.seek-bar.disabled { cursor: default; }
.seek-rail {
  position: relative; width: 100%; height: 4px;
  overflow: hidden; border-radius: 999px;
  background: var(--console-fill-2);
  transition: height .12s ease;
}
.seek-rail i {
  position: absolute; inset: 0 auto 0 0;
  border-radius: 999px; background: var(--console-ink); opacity: .62;
}
.seek-bar:hover .seek-rail, .seek-bar.dragging .seek-rail { height: 6px; }
.seek-knob {
  position: absolute; top: 50%; width: 12px; height: 12px;
  margin-left: -6px; border-radius: 50%;
  background: var(--console-ink); opacity: 0;
  transform: translateY(-50%);
  transition: opacity .12s ease;
  pointer-events: none;
}
.seek-bar:hover .seek-knob, .seek-bar:focus-visible .seek-knob, .seek-bar.dragging .seek-knob { opacity: 1; }
.seek-bar.disabled .seek-knob { display: none; }
.seek-bar:focus-visible .seek-rail { box-shadow: 0 0 0 3px var(--console-brand-soft); }

.bar-right { display: flex; align-items: center; gap: 10px; justify-content: flex-end; }
.bar-volume { display: flex; align-items: center; gap: 7px; color: var(--console-muted); }
.bar-volume input[type="range"] {
  width: 92px; height: 4px; margin: 0; border-radius: 999px;
  appearance: none; background: var(--console-fill-2); cursor: pointer;
}
.bar-volume input[type="range"]::-webkit-slider-thumb {
  width: 12px; height: 12px; border: 0; border-radius: 50%;
  background: var(--console-ink); appearance: none; cursor: grab;
}
.queue-button {
  position: relative; width: 34px; height: 34px;
  display: grid; place-items: center; border-radius: 8px;
  color: var(--console-muted);
}
.queue-button:hover { background: var(--console-hover); color: var(--console-ink); }
.queue-button em {
  position: absolute; top: -1px; right: -2px;
  min-width: 15px; height: 15px; padding: 0 3px;
  display: grid; place-items: center;
  border-radius: 999px; background: var(--console-brand); color: #fff;
  font-size: 9.5px; font-style: normal; font-weight: 700;
}

/* ================= expanded stage ================= */
.stage { position: relative; height: 100%; overflow: hidden; }
.stage-wash { position: absolute; inset: -20%; filter: blur(80px) saturate(160%); opacity: .95; }
.stage-wash::before, .stage-wash::after { content: ""; position: absolute; border-radius: 50%; }
.stage-wash::before { width: 900px; height: 900px; left: 2%; top: -10%; background: radial-gradient(circle, rgba(255, 154, 107, .85), transparent 62%); }
.stage-wash::after { width: 780px; height: 780px; right: 0; bottom: -14%; background: radial-gradient(circle, rgba(122, 108, 255, .85), transparent 62%); }
.stage-veil { position: absolute; inset: 0; background: linear-gradient(180deg, rgba(10, 10, 12, .24), rgba(10, 10, 12, .68)); }

.stage-top {
  position: relative; z-index: 2;
  height: 62px; display: flex; align-items: center; gap: 12px;
  padding: 0 20px; color: rgba(255, 255, 255, .8);
}
.stage-icon {
  width: 32px; height: 32px; display: grid; place-items: center;
  border-radius: 8px; color: rgba(255, 255, 255, .72);
}
.stage-icon:hover { background: rgba(255, 255, 255, .13); color: #fff; }
.stage-label { font-size: 12px; font-weight: 600; letter-spacing: .03em; color: rgba(255, 255, 255, .55); }
.stage-top-right { margin-left: auto; display: flex; gap: 2px; }

.stage-body {
  position: relative; z-index: 2;
  display: grid; grid-template-columns: minmax(0, 380px) minmax(0, 1fr);
  gap: 64px; padding: 8px 72px 0;
  height: calc(100% - 200px);
  max-width: 100%;
}
.stage-left { max-width: 100%; }
.stage-left { min-width: 0; }
.stage-cover {
  width: 100%; aspect-ratio: 1 / 1;
  display: grid; place-items: center; overflow: hidden;
  border-radius: 10px; background: rgba(255, 255, 255, .1);
  color: rgba(255, 255, 255, .8);
  box-shadow: 0 24px 60px rgba(0, 0, 0, .55);
}
.stage-cover img { width: 100%; height: 100%; object-fit: cover; }
.stage-meta { margin-top: 24px; }
.stage-meta b { display: block; font-size: 21px; font-weight: 750; letter-spacing: -.015em; }
.stage-meta span { display: block; margin-top: 3px; color: rgba(255, 255, 255, .58); font-size: 14px; }

.stage-lyrics {
  min-width: 0; height: 100%; overflow-y: auto; padding: 4px 0 40px;
  display: flex; flex-direction: column; justify-content: center;
  -webkit-mask-image: linear-gradient(180deg, transparent 0, #000 10%, #000 86%, transparent 100%);
  mask-image: linear-gradient(180deg, transparent 0, #000 10%, #000 86%, transparent 100%);
  scrollbar-width: none;
}
.stage-lyrics::-webkit-scrollbar { display: none; }
.lyric-line {
  margin: 0; padding: 8px 0;
  color: rgba(255, 255, 255, .3);
  font-size: 20px; font-weight: 700; line-height: 1.38; letter-spacing: -.01em;
  cursor: pointer; transition: color .18s ease, font-size .18s ease;
}
.lyric-line:hover { color: rgba(255, 255, 255, .55); }
.lyric-line.past { color: rgba(255, 255, 255, .16); }
.lyric-line.active { color: #fff; font-size: 23px; text-shadow: 0 2px 24px rgba(0, 0, 0, .6); }
.lyric-empty { margin: 0; color: rgba(255, 255, 255, .4); font-size: 15px; }

.stage-foot {
  position: absolute; z-index: 3; left: 72px; right: 72px; bottom: 28px;
  display: flex; align-items: center; gap: 26px;
}
.stage-controls { display: flex; align-items: center; gap: 20px; color: rgba(255, 255, 255, .9); }
.stage-controls button { color: inherit; opacity: .85; }
.stage-controls button:hover:not(:disabled) { opacity: 1; }
.stage-controls button.on { color: var(--console-brand); opacity: 1; }
.stage-play {
  width: 50px; height: 50px; display: grid; place-items: center;
  border-radius: 50%; background: rgba(255, 255, 255, .14);
  border: 1px solid rgba(255, 255, 255, .16);
  backdrop-filter: blur(10px);
}
.stage-timeline { flex: 1; min-width: 0; display: flex; align-items: center; gap: 12px; }
.stage-timeline time { flex: 0 0 auto; color: rgba(255, 255, 255, .5); font-size: 11px; }
.stage-timeline .seek-rail { background: rgba(255, 255, 255, .2); }
.stage-timeline .seek-rail i { background: #fff; opacity: .92; }
.stage-timeline .seek-knob { background: #fff; }
.stage-volume { display: flex; align-items: center; gap: 7px; color: rgba(255, 255, 255, .5); }
.stage-volume input[type="range"] {
  width: 86px; height: 4px; margin: 0; appearance: none; border-radius: 999px;
  background: rgba(255, 255, 255, .22); cursor: pointer;
}
.stage-volume input[type="range"]::-webkit-slider-thumb {
  width: 12px; height: 12px; border: 0; border-radius: 50%;
  background: #fff; appearance: none; cursor: grab;
}

/* ================= responsive ================= */
@media (max-width: 1023px) {
  .player-bar {
    grid-template-columns: minmax(0, 1fr) auto;
    grid-template-rows: auto auto;
    gap: 6px 12px;
    height: var(--console-player-h);
    padding: 8px 14px calc(8px + env(safe-area-inset-bottom));
    bottom: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    border-radius: 12px 12px 0 0;
  }
  .now-playing { grid-column: 1; grid-row: 1; }
  .np-cover { width: 40px; height: 40px; }
  .bar-controls { grid-column: 2; grid-row: 1; gap: 2px; }
  .ctrl { width: 30px; height: 30px; }
  .ctrl.play { width: 34px; height: 34px; }
  .bar-timeline { grid-column: 1 / -1; grid-row: 2; }
  .bar-right { display: flex; grid-column: 2; grid-row: 2; justify-content: flex-end; }
  .bar-volume { display: none; }
  .player-bar.expanded { bottom: 0; border-radius: 0; height: 100dvh; grid-template-columns: 1fr; grid-template-rows: 1fr; }

  .stage-body { grid-template-columns: minmax(0, 1fr); gap: 16px; padding: 0 20px; height: calc(100% - 220px); }
  .stage-left { width: 100%; display: flex; flex-direction: column; align-items: center; }
  .stage-cover { width: 100%; max-width: ~"min(72vw, 300px)"; margin: 0 auto; }
  .stage-meta { margin-top: 16px; text-align: center; }
  .stage-lyrics { display: none; }
  .stage-foot { left: 16px; right: 16px; bottom: calc(20px + env(safe-area-inset-bottom)); flex-direction: column; gap: 12px; }
  .stage-controls { width: 100%; justify-content: space-between; }
  .stage-timeline { width: 100%; }
  .stage-volume { display: none; }
  .stage-label { font-size: 11px; }
}
</style>
