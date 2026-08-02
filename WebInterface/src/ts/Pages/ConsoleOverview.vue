<template>
  <main class="music">
    <section v-if="!state.configured" class="empty-state">
      <span class="empty-icon"><b-icon icon="music-off" /></span>
      <h1>还没有连接机器人</h1>
      <p>请先在管理页面完成 TeamSpeak 连接。</p>
    </section>
    <template v-else>
      <header v-if="!recentOnly" class="music-heading">
        <div>
          <p class="eyebrow">{{ recentOnly ? '播放记录' : '点歌' }}</p>
          <h1>{{ recentOnly ? '最近播放' : '想听什么？' }}</h1>
          <span v-if="!recentOnly">搜索歌曲、歌手或专辑，马上加入播放队列。</span>
        </div>
        <label v-if="bots.length" class="bot-select">
          <span class="bot-select-label">控制机器人</span>
          <span class="bot-select-control">
            <i class="bot-select-icon"><b-icon icon="music" size="is-small" /></i>
            <select v-model="botId" aria-label="选择控制机器人" @change="selectBot">
              <option v-for="bot in bots" :key="bot.id" :value="bot.id">{{ bot.name }} · {{ statusText(bot.status) }}</option>
            </select>
            <i class="bot-select-chevron"><b-icon icon="chevron-down" size="is-small" /></i>
          </span>
        </label>
      </header>
      <p v-if="error" class="error">{{ error }}</p>

      <section v-if="!recentOnly && results.length" class="results-section">
        <div class="section-title"><h2>搜索结果</h2><span>{{ results.length }} 首歌曲</span></div>
        <div class="track-list">
          <article v-for="(track, index) in results" :key="track.resid + track.type" class="track-row">
            <span class="track-index">{{ String(index + 1).padStart(2, '0') }}</span>
            <img v-if="cover(track)" :src="cover(track)" :alt="track.title">
            <i v-else class="cover-placeholder"><b-icon icon="music-note" /></i>
            <div class="track-info"><b>{{ track.title || '未命名歌曲' }}</b><small>{{ track.type || '歌曲' }}</small></div>
            <button class="row-action secondary" :disabled="busy" title="加入待播队列" @click="add(track)"><b-icon icon="playlist-plus" size="is-small" /><span>加入</span></button>
            <button class="row-action primary" :disabled="busy" title="立即播放" @click="play(track)"><b-icon icon="play" size="is-small" /><span>播放</span></button>
          </article>
        </div>
      </section>

      <section v-if="!recentOnly && !results.length" class="recent-section">
        <div class="section-title"><h2>最近播放</h2><router-link v-if="!recentOnly && state.recent.length" to="/recent">查看全部</router-link></div>
        <div v-if="state.recent.length" class="recent-strip">
          <article v-for="track in state.recent" :key="track.resource.resid + track.type" class="recent-card" @click="play(track.resource)">
            <div class="recent-cover"><img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title"><i v-else><b-icon icon="music-note" /></i><button type="button" title="播放" @click.stop="play(track.resource)"><b-icon icon="play" size="is-small" /></button></div>
            <b>{{ track.title }}</b><small>{{ track.type || '歌曲' }}</small>
          </article>
        </div>
        <p v-else class="empty-copy">还没有播放记录。</p>
      </section>

      <section v-if="recentOnly" class="recent-page">
        <header class="recent-page-head">
          <div>
            <p class="eyebrow">播放记录</p>
            <h1>最近播放</h1>
            <span>{{ state.recent.length }} 首歌曲</span>
          </div>
          <span v-if="state.recent.length" class="clear-history">播放记录由机器人历史模块维护</span>
        </header>
        <div v-if="state.recent.length" class="recent-list">
          <div class="day-group">
            <div class="day-label">最近</div>
            <article v-for="(track, index) in state.recent" :key="track.resource.resid + track.type + index" class="song-row">
              <button type="button" class="row-play" title="播放" aria-label="播放" :disabled="busy" @click="play(track.resource)"><b-icon icon="play" size="is-small" /></button>
              <span class="scover"><img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title"><b-icon v-else icon="music-note" /></span>
              <span class="song-info"><b>{{ track.title }}</b><small>{{ track.type || '歌曲' }}</small></span>
              <span class="dur">{{ duration(track) }}</span>
              <button type="button" class="row-add" title="加入待播" aria-label="加入待播" :disabled="busy" @click="add(track.resource)"><b-icon icon="playlist-plus" size="is-small" /></button>
            </article>
          </div>
        </div>
        <p v-else class="empty-copy recent-empty">还没有播放记录。</p>
      </section>

      <ConsolePlayerBar
        :state="state"
        :busy="busy"
        :bot-id="botId"
        @previous="control('previous')"
        @pause="control('pause')"
        @next="control('next')"
        @queue="queueOpen = true"
        @volume="setVolume"
        @loop="setLoop"
        @random="setRandom"
      />
      <ConsoleQueueDrawer :open="queueOpen" :queue="state.queue" :is-admin="isAdmin" @close="queueOpen = false" @play="playQueuedTrack" @clear="clear"/>
    </template>
  </main>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, MusicState, TrackResource, ConsoleBot } from "../ConsoleApi";
import ConsolePlayerBar from "../Components/ConsolePlayerBar.vue";
import ConsoleQueueDrawer from "../Components/ConsoleQueueDrawer.vue";

const blank: MusicState = { configured: false, connected: false, current: null, queue: [], recent: [], volume: 50, loop: "off", random: false };

export default Vue.extend({
  components: { ConsolePlayerBar, ConsoleQueueDrawer },
  props: { recentOnly: { type: Boolean, default: false } },
  data() {
    return {
      state: blank as MusicState,
      results: [] as TrackResource[],
      bots: [] as ConsoleBot[],
      botId: "",
      error: "",
      isAdmin: false,
      queueOpen: false,
      busy: false,
      actionToken: 0,
      timer: 0 as any,
      listener: null as any,
    };
  },
  async created() {
    const user = await consoleApi<ConsoleUser>("me");
    this.isAdmin = user.role === "admin";
    this.bots = (await consoleApi<{ bots: ConsoleBot[] }>("bots")).bots;
    this.botId = this.bots[0] ? this.bots[0].id : "";
    await this.refresh(true);
    const initialQuery = typeof this.$route.query.q === "string" ? this.$route.query.q : "";
    if (initialQuery) await this.search(initialQuery);
    this.timer = setInterval(() => this.refresh(false), 5000);
    this.listener = (event: any) => this.search(event.detail);
    window.addEventListener("console-search", this.listener);
  },
  beforeDestroy() {
    clearInterval(this.timer);
    window.removeEventListener("console-search", this.listener);
  },
  watch: {
    "$route.query.q"(value: string) { if (value) this.search(value); else this.results = []; },
  },
  methods: {
    cover(track: TrackResource) { return track.add && track.add.cover_url; },
    statusText(status: string) { return status === "connected" ? "已连接" : status === "connecting" ? "连接中" : "离线"; },
    nextQueueTrack() {
      const queue = this.state.queue || [];
      if (!queue.length) return null;
      const activeIndex = queue.findIndex((track) => track.active);
      if (activeIndex >= 0 && activeIndex + 1 < queue.length) return queue[activeIndex + 1];
      if (activeIndex < 0) return queue[0];
      return null;
    },
    async refresh(force = false) {
      if (this.busy && !force) return;
      const token = this.actionToken;
      try {
        const next = await consoleApi<MusicState>("music/state?botId=" + encodeURIComponent(this.botId));
        if (this.busy && !force) return;
        if (token !== this.actionToken && !force) return;
        this.state = next;
        this.error = "";
      } catch (error) {
        if (this.busy && !force) return;
        this.error = error instanceof Error ? error.message : "状态同步失败。";
      }
    },
    async search(query: string) {
      if (!query) return;
      try { this.results = (await consoleApi<{ results: TrackResource[] }>("music/search", { query, botId: this.botId })).results; this.error = ""; }
      catch (error) { this.error = error instanceof Error ? error.message : "搜索失败。"; }
    },
    async selectBot() {
      this.results = [];
      await this.refresh(true);
      const query = this.$route.query.q;
      if (!this.recentOnly && typeof query === "string" && query) await this.search(query);
    },
    async call(path: string, body: any = {}, optimistic?: () => void) {
      const token = ++this.actionToken;
      this.busy = true;
      this.error = "";
      if (optimistic) optimistic();
      try {
        await consoleApi(path, { ...body, botId: this.botId });
        if (token !== this.actionToken) return;
        await this.refresh(true);
      } catch (error) {
        if (token !== this.actionToken) return;
        this.error = error instanceof Error ? error.message : "操作失败。";
        await this.refresh(true);
      } finally {
        if (token === this.actionToken) this.busy = false;
      }
    },
    play(resource: TrackResource) {
      return this.call("music/play", { resource });
    },
    playQueuedTrack(resource: TrackResource) {
      this.queueOpen = false;
      return this.play(resource);
    },
    add(resource: TrackResource) {
      return this.call("music/add", { resource });
    },
    control(name: string) {
      if (name === "pause") {
        if (!this.state.current) return Promise.resolve();
        return this.call("music/pause", {}, () => {
          this.state = { ...this.state, paused: !this.state.paused };
        });
      }
      if (name === "next") {
        if (!this.state.current && !(this.state.queue && this.state.queue.length)) return Promise.resolve();
        return this.call("music/next", {}, () => {
          const next = this.nextQueueTrack();
          if (!next) return;
          this.state = {
            ...this.state,
            current: { ...next, active: true },
            paused: false,
            position: 0,
            length: 0,
          };
        });
      }
      if (name === "previous" && !this.state.current) return Promise.resolve();
      return this.call("music/" + name);
    },
    clear() {
      this.queueOpen = false;
      return this.call("music/clear");
    },
    duration(track: any) {
      const value = track.resource && track.resource.add && (track.resource.add.duration || track.resource.add.length);
      return value ? String(value) : "--:--";
    },
    setVolume(volume: number) {
      const value = Math.max(0, Math.min(100, Number(volume) || 0));
      return this.call("music/volume", { volume: value, botId: this.botId }, () => {
        this.state = { ...this.state, volume: value };
      });
    },
    setLoop(mode: string) {
      const next = mode === "one" || mode === "all" ? mode : "off";
      return this.call("music/loop", { mode: next, botId: this.botId }, () => {
        this.state = {
          ...this.state,
          loop: next,
          random: next === "one" ? false : this.state.random,
        };
      });
    },
    setRandom(enabled: boolean) {
      const value = !!enabled;
      return this.call("music/random", { enabled: value, botId: this.botId }, () => {
        this.state = {
          ...this.state,
          random: value,
          loop: value && this.state.loop === "one" ? "all" : this.state.loop,
        };
      });
    },
  },
});
</script>

<style scoped lang="less">
.music { max-width: 1120px; margin: 0 auto; padding: 54px 40px 48px; }
.music-heading { display: flex; align-items: flex-end; justify-content: space-between; gap: 32px; }
.eyebrow { margin: 0 0 8px; color: var(--console-brand); font-size: 13px; font-weight: 700; }
.music-heading h1 { margin: 0; color: var(--console-ink); font-size: clamp(30px, 4vw, 44px); line-height: 1.08; letter-spacing: -.025em; }
.music-heading > div > span { display: block; margin-top: 12px; color: var(--console-muted); font-size: 14px; }
.bot-select { display: inline-flex; align-items: center; gap: 12px; flex: 0 0 auto; color: var(--console-muted); font-size: 12px; }
.bot-select-label { white-space: nowrap; }
.bot-select-control { position: relative; display: flex; align-items: center; min-width: 230px; }
.bot-select-icon { position: absolute; z-index: 1; left: 12px; display: grid; place-items: center; color: var(--console-brand); font-style: normal; pointer-events: none; }
.bot-select select { appearance: none; width: 100%; height: 40px; padding: 0 34px 0 34px; border: 1px solid var(--console-line); border-radius: 10px; outline: 0; background: var(--console-surface-2); color: var(--console-ink); cursor: pointer; }
.bot-select select:focus { border-color: var(--console-brand); box-shadow: 0 0 0 3px var(--console-brand-soft); }
.bot-select-chevron { position: absolute; right: 11px; color: var(--console-muted); font-style: normal; pointer-events: none; }
.music section { margin-top: 50px; }
.section-title { display: flex; align-items: baseline; justify-content: space-between; gap: 16px; margin-bottom: 16px; }
.section-title h2 { margin: 0; color: var(--console-ink); font-size: 19px; letter-spacing: -.01em; }
.section-title > span, .section-title a { color: var(--console-muted); font-size: 12px; text-decoration: none; }
.section-title a:hover { color: var(--console-brand); }
.track-list { border-top: 1px solid var(--console-line); }
.track-row { display: flex; align-items: center; gap: 14px; min-height: 76px; padding: 10px 4px; border-bottom: 1px solid var(--console-line); }
.track-row:hover { background: var(--console-hover); }
.track-index { width: 26px; flex: 0 0 26px; color: var(--console-muted-2); font-size: 12px; font-variant-numeric: tabular-nums; text-align: center; }
.track-row img, .track-row .cover-placeholder { width: 52px; height: 52px; flex: 0 0 52px; border-radius: 9px; object-fit: cover; background: var(--console-surface-3); color: var(--console-muted); }
.cover-placeholder { display: grid; place-items: center; font-style: normal; }
.track-info { min-width: 0; flex: 1; }
.track-info b, .track-info small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-info b { color: var(--console-ink); font-size: 14px; font-weight: 600; }
.track-info small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.row-action { min-width: 72px; height: 34px; display: inline-flex; align-items: center; justify-content: center; gap: 5px; padding: 0 10px; border: 1px solid transparent; border-radius: 9px; cursor: pointer; font-size: 12px; font-weight: 600; }
.row-action.secondary { border-color: var(--console-line); background: transparent; color: var(--console-muted); }
.row-action.secondary:hover { border-color: var(--console-line-strong); color: var(--console-ink); }
.row-action.primary { background: var(--console-brand); color: #fff; }
.row-action.primary:hover { background: var(--console-brand-dark); }
.row-action:disabled { opacity: .5; cursor: wait; }
.recent-strip { display: flex; gap: 16px; overflow-x: auto; padding: 2px 2px 12px; scrollbar-width: thin; }
.recent-card { width: 170px; flex: 0 0 170px; cursor: pointer; }
.recent-cover { position: relative; width: 170px; height: 170px; overflow: hidden; border-radius: 13px; background: var(--console-surface-3); }
.recent-cover img, .recent-cover > i { width: 100%; height: 100%; display: grid; place-items: center; object-fit: cover; color: var(--console-muted); font-size: 30px; font-style: normal; }
.recent-cover::after { content: ""; position: absolute; inset: 45% 0 0; background: linear-gradient(transparent, rgba(0,0,0,.35)); pointer-events: none; opacity: 0; transition: opacity 160ms ease; }
.recent-card:hover .recent-cover::after { opacity: 1; }
.recent-cover button { position: absolute; right: 10px; bottom: 10px; z-index: 1; width: 34px; height: 34px; display: grid; place-items: center; border: 0; border-radius: 50%; background: var(--console-brand); color: #fff; opacity: 0; cursor: pointer; transform: translateY(4px); transition: opacity 160ms ease, transform 160ms ease; }
.recent-card:hover .recent-cover button { opacity: 1; transform: translateY(0); }
.recent-card > b, .recent-card > small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.recent-card > b { margin-top: 10px; color: var(--console-ink); font-size: 13px; font-weight: 600; }
.recent-card > small { margin-top: 3px; color: var(--console-muted); font-size: 12px; }
.empty-state { min-height: 55vh; display: grid; place-content: center; justify-items: center; text-align: center; }
.empty-icon { width: 56px; height: 56px; display: grid; place-items: center; border-radius: 18px; background: var(--console-brand-soft); color: var(--console-brand); }
.empty-state h1 { margin: 18px 0 6px; font-size: 24px; }
.empty-state p, .empty-copy { color: var(--console-muted); font-size: 14px; }
.error { margin: 18px 0 0; color: var(--console-danger); font-size: 13px; }
.recent-page { max-width: 1000px; margin: 0 auto; padding: 40px 34px 60px; }
.recent-page-head { display: flex; align-items: baseline; justify-content: space-between; gap: 16px; }
.recent-page-head h1 { margin: 0; color: var(--console-ink); font-size: 28px; font-weight: 800; letter-spacing: -.02em; }
.recent-page-head span { display: block; margin-top: 5px; color: var(--console-muted); font-size: 13px; }
.clear-history { color: var(--console-muted); font-size: 13px; }
.recent-list { margin-top: 22px; border-top: 1px solid var(--console-line); }
.day-group { margin-top: 22px; }
.day-label { display: flex; align-items: center; gap: 10px; margin-bottom: 10px; color: var(--console-muted); font-size: 13px; font-weight: 700; }
.day-label::after { content: ""; flex: 1; height: 1px; background: var(--console-line); }
.song-row { display: flex; align-items: center; gap: 14px; height: 64px; padding: 0 10px; border-bottom: 1px solid var(--console-line); border-radius: 12px; }
.song-row:hover { background: var(--console-hover); }
.row-play, .row-add { width: 36px; height: 36px; flex: 0 0 auto; display: grid; place-items: center; border: 0; border-radius: 50%; background: transparent; color: var(--console-ink); cursor: pointer; }
.row-play:hover, .row-add:hover { background: var(--console-brand-soft); color: var(--console-brand); }
.scover { width: 44px; height: 44px; flex: 0 0 auto; display: grid; place-items: center; overflow: hidden; border-radius: 9px; background: var(--console-surface-3); color: var(--console-muted); }
.scover img { width: 100%; height: 100%; object-fit: cover; }
.song-info { min-width: 0; flex: 1; }
.song-info b, .song-info small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.song-info b { color: var(--console-ink); font-size: 14.5px; font-weight: 600; }
.song-info small { margin-top: 3px; color: var(--console-muted); font-size: 12.5px; }
.dur { width: 44px; color: var(--console-muted); font-size: 12.5px; font-variant-numeric: tabular-nums; text-align: right; }
.recent-empty { min-height: 36vh; display: grid; place-items: center; }
@media (max-width: 1023px) {
  .music { padding: 34px 20px 38px; }
  .music-heading { display: block; }
  .music-heading h1 { font-size: 34px; }
  .bot-select { display: flex; align-items: center; margin-top: 26px; }
  .bot-select-control { flex: 1; min-width: 0; }
  .music section { margin-top: 38px; }
  .recent-page { padding: 28px 20px 150px; }
}
@media (max-width: 560px) {
  .music { padding: 28px 16px 34px; }
  .music-heading h1 { font-size: 30px; }
  .music-heading > div > span { font-size: 13px; }
  .bot-select { align-items: stretch; flex-direction: column; gap: 8px; }
  .track-row { gap: 10px; min-height: 68px; }
  .track-index { display: none; }
  .track-row img, .track-row .cover-placeholder { width: 46px; height: 46px; flex-basis: 46px; }
  .row-action { min-width: 34px; width: 34px; padding: 0; }
  .row-action span { display: none; }
  .recent-card, .recent-cover { width: 142px; flex-basis: 142px; }
  .recent-cover { height: 142px; }
  .recent-cover button { opacity: 1; transform: none; }
  .recent-page { padding: 24px 16px 150px; }
  .recent-page-head h1 { font-size: 24px; }
  .song-row { gap: 10px; }
  .row-play { width: 30px; }
  .scover { width: 42px; height: 42px; }
  .row-add { width: 30px; }
}
</style>
