<template>
  <div class="bd-page">
    <section v-if="!state.configured" class="bd-empty-state">
      <b-icon icon="music-off" size="is-large" />
      <h1>还没有连接机器人</h1>
      <p>{{ isAdmin ? '请先在管理页面完成 TeamSpeak 连接。' : '请联系管理员完成 TeamSpeak 连接。' }}</p>
    </section>

    <template v-else>
      <!-- ===== now playing hero ===== -->
      <section v-if="!recentOnly && state.current" class="bd-hero">
        <div class="bd-hero-cover">
          <img v-if="state.current.coverUrl" :src="state.current.coverUrl" :alt="state.current.title">
          <b-icon v-else icon="music-note" size="is-large" />
        </div>
        <div class="bd-hero-copy">
          <span class="bd-hero-badge"><span class="bd-eq"><i></i><i></i><i></i></span>{{ state.paused ? '已暂停' : '正在播放' }}</span>
          <h1>{{ state.current.title }}</h1>
          <p>{{ artistOf(state.current) }}</p>
        </div>
        <div class="bd-hero-actions">
          <button type="button" class="bd-pill" :disabled="busy" @click="add(state.current.resource)">
            <b-icon icon="playlist-plus" size="is-small" />加入待播
          </button>
          <button type="button" class="bd-pill solid" :disabled="busy" @click="togglePause">
            <b-icon :icon="state.paused ? 'play' : 'pause'" size="is-small" />{{ state.paused ? '继续' : '暂停' }}
          </button>
        </div>
      </section>

      <!-- ===== page heading ===== -->
      <header v-if="recentOnly || !state.current" class="bd-heading">
        <div>
          <p class="bd-eyebrow">{{ recentOnly ? '播放记录' : '点歌' }}</p>
          <h1>{{ recentOnly ? '最近播放' : '想听什么？' }}</h1>
          <span v-if="!recentOnly">搜索歌曲、歌手或专辑，点一下就加入播放队列。</span>
          <span v-else>机器人历史记录中的曲目。</span>
        </div>
        <label v-if="bots.length" class="bd-bot-select">
          <span class="bd-bot-label">控制机器人</span>
          <span class="bd-bot-control">
            <select v-model="botId" aria-label="选择控制机器人" @change="selectBot">
              <option v-for="bot in bots" :key="bot.id" :value="bot.id">{{ bot.name }} · {{ statusText(bot.status) }}</option>
            </select>
            <b-icon icon="chevron-down" size="is-small" />
          </span>
        </label>
      </header>

      <p v-if="error" class="bd-inline-error">{{ error }}</p>

      <!-- ===== search results ===== -->
      <section v-if="!recentOnly && results.length" class="bd-block">
        <div class="bd-block-head">
          <h2>搜索结果</h2>
          <span>{{ results.length }} 首歌曲</span>
        </div>
        <div class="bd-track-list">
          <article
            v-for="(track, index) in results"
            :key="track.resid + track.type + index"
            :class="['bd-track-row', { playing: isCurrent(track) }]"
          >
            <span class="bd-row-index">
              <span v-if="isCurrent(track)" class="bd-eq"><i></i><i></i><i></i></span>
              <template v-else>{{ String(index + 1).padStart(2, '0') }}</template>
            </span>
            <span class="bd-row-art">
              <img v-if="coverOf(track)" :src="coverOf(track)" :alt="track.title">
              <b-icon v-else icon="music-note" size="is-small" />
            </span>
            <span class="bd-row-copy">
              <b>{{ track.title || '未命名歌曲' }}</b>
              <span>{{ artistOfResource(track) }}</span>
            </span>
            <span class="bd-row-duration">{{ durationOf(track) }}</span>
            <span class="bd-row-actions">
              <button type="button" class="bd-pill" :disabled="busy" title="加入待播队列" @click="add(track)">
                <b-icon icon="playlist-plus" size="is-small" /><span>加入</span>
              </button>
              <button type="button" class="bd-pill solid" :disabled="busy" title="立即播放" @click="play(track)">
                <b-icon icon="play" size="is-small" /><span>播放</span>
              </button>
            </span>
          </article>
        </div>
      </section>

      <!-- ===== search empty / prompt ===== -->
      <section v-if="!recentOnly && !results.length && searched" class="bd-block">
        <div class="bd-block-head"><h2>搜索结果</h2></div>
        <p class="bd-muted">没有找到相关歌曲，换个关键词试试。</p>
      </section>

      <!-- ===== recent ===== -->
      <section v-if="!recentOnly && !results.length" class="bd-block">
        <div class="bd-block-head">
          <h2>最近播放</h2>
          <router-link v-if="state.recent.length" to="/recent">查看全部</router-link>
        </div>
        <div v-if="state.recent.length" class="bd-album-grid">
          <button
            v-for="track in state.recent"
            :key="track.resource.resid + track.type"
            type="button"
            class="bd-album-card"
            @click="play(track.resource)"
          >
            <span class="bd-album-cover">
              <img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title">
              <b-icon v-else icon="music-note" size="is-large" />
              <span class="bd-play-fab"><b-icon icon="play" size="is-small" /></span>
            </span>
            <b>{{ track.title }}</b>
            <span>{{ artistOf(track) }}</span>
          </button>
        </div>
        <p v-else class="bd-muted">还没有播放记录。</p>
      </section>

      <!-- ===== recent only page ===== -->
      <section v-if="recentOnly" class="bd-block">
        <div class="bd-block-head">
          <h2>最近</h2>
          <span v-if="state.recent.length">{{ state.recent.length }} 首歌曲</span>
        </div>
        <div v-if="state.recent.length" class="bd-track-list">
          <article
            v-for="(track, index) in state.recent"
            :key="track.resource.resid + track.type + index"
            :class="['bd-track-row', { playing: isCurrentResource(track.resource) }]"
          >
            <span class="bd-row-index">
              <span v-if="isCurrentResource(track.resource)" class="bd-eq"><i></i><i></i><i></i></span>
              <template v-else>{{ String(index + 1).padStart(2, '0') }}</template>
            </span>
            <span class="bd-row-art">
              <img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title">
              <b-icon v-else icon="music-note" size="is-small" />
            </span>
            <span class="bd-row-copy">
              <b>{{ track.title }}</b>
              <span>{{ artistOf(track) }}</span>
            </span>
            <span class="bd-row-duration">{{ durationOf(track.resource) }}</span>
            <span class="bd-row-actions">
              <button type="button" class="bd-pill" :disabled="busy" title="加入待播" @click="add(track.resource)">
                <b-icon icon="playlist-plus" size="is-small" /><span>加入</span>
              </button>
              <button type="button" class="bd-pill solid" :disabled="busy" title="播放" @click="play(track.resource)">
                <b-icon icon="play" size="is-small" /><span>播放</span>
              </button>
            </span>
          </article>
        </div>
        <p v-else class="bd-muted">还没有播放记录。</p>
      </section>
    </template>

    <ConsolePlayerBar
      :state="state"
      :busy="busy"
      :bot-id="botId"
      @previous="control('previous')"
      @pause="control('pause')"
      @next="control('next')"
      @queue="openQueue"
      @volume="setVolume"
      @loop="setLoop"
      @random="setRandom"
      @seek="seek"
    />
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, MusicState, TrackResource, ConsoleBot } from "../ConsoleApi";
import ConsolePlayerBar from "../Components/ConsolePlayerBar.vue";
import { Track } from "../ConsoleApi";

let lastQueue: Track[] = [];
const blank: MusicState = { configured: false, connected: false, current: null, queue: [], recent: [], volume: 50, loop: "off", random: false };

export default Vue.extend({
  components: { ConsolePlayerBar },
  props: { recentOnly: { type: Boolean, default: false } },
  data() {
    return {
      state: blank as MusicState,
      results: [] as TrackResource[],
      bots: [] as ConsoleBot[],
      botId: "",
      error: "",
      isAdmin: false,
      busy: false,
      searched: false,
      actionToken: 0,
      timer: 0 as any,
      listener: null as any,
      playListener: null as any,
      clearListener: null as any,
      syncListener: null as any,
    };
  },
  async created() {
    try {
      const user = await consoleApi<ConsoleUser>("me");
      this.isAdmin = user.role === "admin";
      this.bots = (await consoleApi<{ bots: ConsoleBot[] }>("bots")).bots;
      this.botId = this.pickDefaultBot(user.allowedBots);
      await this.refresh(true);
      this.emitQueue(this.state);
      this.$nextTick(() => this.emitQueue(this.state));
      const initialQuery = typeof this.$route.query.q === "string" ? this.$route.query.q : "";
      if (initialQuery) await this.search(initialQuery);
      this.timer = setInterval(() => this.refresh(false), 5000);

      this.listener = (event: any) => this.search(event.detail);
      this.playListener = (event: any) => this.play(event.detail);
      this.clearListener = () => this.clear();
      this.syncListener = () => this.emitQueue(this.state);
      window.addEventListener("console-search", this.listener);
      window.addEventListener("console-queue-play", this.playListener);
      window.addEventListener("console-queue-clear", this.clearListener);
      window.addEventListener("console-queue-sync", this.syncListener);
    } catch (error) {
      this.error = error instanceof Error ? error.message : "读取控制台数据失败。";
    }
  },
  beforeDestroy() {
    clearInterval(this.timer);
    window.removeEventListener("console-search", this.listener);
    window.removeEventListener("console-queue-play", this.playListener);
    window.removeEventListener("console-queue-clear", this.clearListener);
  },
  watch: {
    "$route.query.q"(value: string) {
      if (value) this.search(value);
      else { this.results = []; this.searched = false; }
    },
    "$route.path"() { this.refresh(true); },
    state: {
      deep: true,
      immediate: true,
      handler(state: MusicState) {
        this.emitQueue(state);
      },
    },
  },
  methods: {
    pickDefaultBot(allowed?: string[]): string {
      if (!this.bots.length) return "";
      if (allowed && allowed.length) {
        const hit = this.bots.find(bot => allowed.indexOf(bot.id) >= 0);
        if (hit) return hit.id;
      }
      const connected = this.bots.find(bot => bot.status === "connected");
      return (connected || this.bots[0]).id;
    },
    selectBot() { this.refresh(true); },
    openQueue() { window.dispatchEvent(new CustomEvent("console-queue-open")); },
    emitQueue(state: MusicState) {
      lastQueue = (state && state.queue) || [];
      window.dispatchEvent(new CustomEvent("console-queue", { detail: { queue: lastQueue } }));
    },
    coverOf(track: TrackResource) { return track.add && track.add.cover_url ? track.add.cover_url : ""; },
    artistOf(track: any): string {
      if (!track || !track.resource) return "歌曲";
      return this.artistOfResource(track.resource);
    },
    artistOfResource(resource: TrackResource): string {
      const add = (resource && resource.add) || {};
      return add.artist || add.singer || add.album || "歌曲";
    },
    durationOf(track: TrackResource): string {
      const add = (track && track.add) || {};
      const raw = add.duration || add.length || "";
      if (!raw) return "";
      const seconds = Number(String(raw).replace(/[^\d.]/g, ""));
      if (!Number.isFinite(seconds) || seconds <= 0) return "";
      const total = Math.floor(seconds);
      return Math.floor(total / 60) + ":" + String(total % 60).padStart(2, "0");
    },
    isCurrent(track: TrackResource): boolean {
      return this.isCurrentResource(track);
    },
    isCurrentResource(track: TrackResource): boolean {
      const current = this.state.current;
      if (!current || !track) return false;
      return current.resource.resid === track.resid && current.resource.type === track.type;
    },
    statusText(status: string) { return status === "connected" ? "已连接" : status === "connecting" ? "连接中" : "离线"; },
    async refresh(force = false) {
      if (this.busy && !force) return;
      const token = this.actionToken;
      try {
        const next = await consoleApi<MusicState>("music/state" + (this.botId ? ("?botId=" + encodeURIComponent(this.botId)) : ""));
        if (this.busy && !force) return;
        if (token !== this.actionToken && !force) return;
        this.state = next;
        this.error = "";
      } catch (error) {
        if (force) this.error = error instanceof Error ? error.message : "读取播放状态失败。";
      }
    },
    async run(action: () => Promise<any>) {
      if (this.busy) return;
      this.busy = true;
      this.actionToken++;
      try {
        await action();
      } catch (error) {
        this.error = error instanceof Error ? error.message : "操作失败。";
      } finally {
        this.busy = false;
        await this.refresh(true);
      }
    },
    async search(query: string) {
      const value = (query || "").trim();
      if (!value) return;
      this.error = "";
      this.searched = true;
      try {
        const result = await consoleApi<{ results: TrackResource[] }>("music/search", { query: value, botId: this.botId });
        this.results = result.results || [];
      } catch (error) {
        this.results = [];
        this.error = error instanceof Error ? error.message : "搜索失败。";
      }
    },
    play(track: TrackResource) {
      return this.run(() => consoleApi("music/play", { resource: track, botId: this.botId }));
    },
    add(track: TrackResource) {
      return this.run(() => consoleApi("music/add", { resource: track, botId: this.botId }));
    },
    control(action: string) {
      return this.run(() => consoleApi("music/" + action, { botId: this.botId }));
    },
    togglePause() { return this.control("pause"); },
    setVolume(volume: number) {
      return this.run(() => consoleApi("music/volume", { volume, botId: this.botId }));
    },
    setLoop(mode: string) {
      return this.run(() => consoleApi("music/loop", { mode, botId: this.botId }));
    },
    setRandom(enabled: boolean) {
      return this.run(() => consoleApi("music/random", { enabled, botId: this.botId }));
    },
    seek(position: number) {
      return this.run(() => consoleApi("music/seek", { position, botId: this.botId }));
    },
    clear() {
      if (!this.isAdmin) return;
      return this.run(() => consoleApi("music/clear", { botId: this.botId }));
    },
  },
});
</script>

<style scoped lang="less">
.bd-page { max-width: 1180px; padding: 4px 26px 26px; }

/* ---------- hero ---------- */
.bd-hero { display: flex; align-items: flex-end; gap: 18px; margin-top: 4px; }
.bd-hero-cover {
  width: 96px; height: 96px; flex: 0 0 auto;
  display: grid; place-items: center; overflow: hidden;
  border-radius: 8px; background: var(--console-surface-3); color: var(--console-brand);
  box-shadow: var(--console-shadow-sm);
}
.bd-hero-cover img { width: 100%; height: 100%; object-fit: cover; }
.bd-hero-copy { min-width: 0; padding-bottom: 2px; }
.bd-hero-badge {
  display: inline-flex; align-items: center; gap: 7px;
  color: var(--console-brand); font-size: 11.5px; font-weight: 700; letter-spacing: .02em;
}
.bd-hero-copy h1 {
  margin: 5px 0 0; overflow: hidden;
  color: var(--console-ink);
  font-size: 26px; font-weight: 800; letter-spacing: -.025em;
  white-space: nowrap; text-overflow: ellipsis;
}
.bd-hero-copy p { margin: 2px 0 0; color: var(--console-muted) !important; font-size: 13px; }
.bd-hero-actions { margin-left: auto; display: flex; gap: 8px; padding-bottom: 6px; }

/* ---------- heading ---------- */
.bd-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; margin: 6px 0 0; }
.bd-eyebrow { margin: 0 0 4px; color: var(--console-brand); font-size: 12px; font-weight: 700; letter-spacing: .02em; }
.bd-heading h1 { margin: 0; color: var(--console-ink); font-size: 30px; font-weight: 800; letter-spacing: -.025em; }
.bd-heading > div > span { display: block; margin-top: 4px; color: var(--console-muted); font-size: 13px; }

.bd-bot-select { flex: 0 0 auto; }
.bd-bot-label { display: block; margin-bottom: 5px; color: var(--console-muted-2); font-size: 11px; font-weight: 600; }
.bd-bot-control {
  position: relative; display: flex; align-items: center;
  height: 34px; padding: 0 28px 0 10px; border-radius: 8px;
  background: var(--console-fill); color: var(--console-muted);
}
.bd-bot-control select {
  max-width: 220px; border: 0; outline: 0; background: transparent;
  color: var(--console-ink); font-size: 12.5px; font-weight: 600;
  appearance: none; cursor: pointer;
}
.bd-bot-control .icon { position: absolute; right: 9px; pointer-events: none; }

/* ---------- blocks ---------- */
.bd-block { margin-top: 26px; }
.bd-block-head { display: flex; align-items: baseline; justify-content: space-between; gap: 12px; margin-bottom: 8px; }
.bd-block-head h2 { margin: 0; color: var(--console-ink); font-size: 19px; font-weight: 750; letter-spacing: -.018em; }
.bd-block-head span, .bd-block-head a { color: var(--console-muted); font-size: 12.5px; text-decoration: none; }
.bd-block-head a { color: var(--console-brand); font-weight: 600; }
.bd-muted { margin: 0; padding: 18px 2px; color: var(--console-muted); font-size: 13px; }

/* ---------- track rows ---------- */
.bd-track-list { display: flex; flex-direction: column; }
.bd-track-row {
  display: grid;
  grid-template-columns: 34px 40px minmax(0, 1fr) 56px 168px;
  align-items: center; gap: 12px;
  height: 52px; padding: 0 8px 0 4px;
  border-radius: 8px; transition: background .12s ease;
}
.bd-track-row:hover, .bd-track-row.playing { background: var(--console-hover); }
.bd-row-index { color: var(--console-muted-2); font-size: 11.5px; text-align: center; font-variant-numeric: tabular-nums; }
.bd-row-art {
  width: 40px; height: 40px; display: grid; place-items: center;
  overflow: hidden; border-radius: 5px;
  background: var(--console-surface-3); color: var(--console-brand);
}
.bd-row-art img { width: 100%; height: 100%; object-fit: cover; }
.bd-row-copy { min-width: 0; }
.bd-row-copy b, .bd-row-copy span { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.bd-row-copy b { color: var(--console-ink); font-size: 13px; font-weight: 600; }
.bd-track-row.playing .bd-row-copy b { color: var(--console-brand); }
.bd-row-copy span { margin-top: 1px; color: var(--console-muted); font-size: 12px; }
.bd-row-duration { color: var(--console-muted-2); font-size: 12px; text-align: right; font-variant-numeric: tabular-nums; }
.bd-row-actions { display: flex; justify-content: flex-end; gap: 6px; visibility: hidden; }
.bd-track-row:hover .bd-row-actions, .bd-track-row.playing .bd-row-actions { visibility: visible; }

/* ---------- album grid ---------- */
.bd-album-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(132px, 1fr)); gap: 20px 16px; }
.bd-album-card { min-width: 0; display: block; padding: 0; border: 0; background: transparent; text-align: left; cursor: pointer; }
.bd-album-cover {
  position: relative; display: grid; place-items: center;
  aspect-ratio: 1 / 1; width: 100%; overflow: hidden;
  border-radius: 7px; background: var(--console-surface-3); color: var(--console-brand);
  box-shadow: var(--console-shadow-sm);
}
.bd-album-cover img { width: 100%; height: 100%; object-fit: cover; }
.bd-play-fab {
  position: absolute; right: 8px; bottom: 8px;
  width: 34px; height: 34px; display: grid; place-items: center;
  border-radius: 50%; background: rgba(20, 20, 22, .62); color: #fff;
  backdrop-filter: blur(10px);
  opacity: 0; transform: translateY(4px);
  transition: opacity .16s ease, transform .16s ease;
  box-shadow: 0 2px 8px rgba(0, 0, 0, .28);
}
.bd-album-card:hover .bd-play-fab { opacity: 1; transform: translateY(0); }
.bd-album-card b { display: block; color: var(--console-ink); margin-top: 8px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; font-weight: 600; }
.bd-album-card > span:last-child { display: block; margin-top: 1px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--console-muted); font-size: 12px; }

.bd-eq { display: flex; align-items: flex-end; gap: 2px; height: 12px; }
.bd-eq i { width: 2.5px; border-radius: 1px; background: var(--console-brand); animation: eq .9s ease-in-out infinite; }
.bd-eq i:nth-child(1) { height: 6px; }
.bd-eq i:nth-child(2) { height: 12px; animation-delay: .18s; }
.bd-eq i:nth-child(3) { height: 8px; animation-delay: .36s; }
@keyframes eq { 0%, 100% { transform: scaleY(.4); } 50% { transform: scaleY(1); } }

@media (max-width: 1023px) {
  .bd-page { padding: 2px 16px 20px; max-width: 100%; overflow-x: hidden; }
  .bd-hero { flex-wrap: wrap; gap: 12px; }
  .bd-hero-cover { width: 72px; height: 72px; }
  .bd-hero-copy { flex: 1 1 auto; min-width: 0; }
  .bd-hero-copy h1 { font-size: 20px; }
  .bd-hero-actions { flex-direction: row; align-items: center; gap: 6px; width: 100%; padding-bottom: 0; }
  .bd-heading { flex-direction: column; align-items: flex-start; gap: 12px; }
  .bd-heading h1 { font-size: 25px; }
  .bd-bot-control select { max-width: 60vw; }
  .bd-track-row { grid-template-columns: 28px 40px minmax(0, 1fr) 64px; height: 50px; gap: 10px; }
  .bd-row-duration { display: none; }
  .bd-row-actions { visibility: visible; }
  .bd-row-actions .pill span { display: none; }
  .bd-row-actions .pill { width: 28px; padding: 0; justify-content: center; }
  .bd-album-grid { grid-template-columns: repeat(auto-fill, minmax(92px, 1fr)); gap: 14px 10px; }
  .bd-album-card b { font-size: 12px; }
  .bd-album-card > span:last-child { font-size: 11px; }
  .bd-block-head h2 { font-size: 17px; }
  .bd-track-list { margin: 0 -6px; }
  .bd-row-actions .pill { height: 24px; }
}
</style>
