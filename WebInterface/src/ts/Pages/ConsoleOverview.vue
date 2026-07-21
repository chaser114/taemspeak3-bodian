<template>
  <main class="music">
    <section v-if="!state.configured"><h1>还没有连接机器人</h1></section>
    <template v-else>
      <header>
        <p>{{ recentOnly ? '最近播放' : '点歌' }}</p>
        <h1>{{ recentOnly ? '最近播放' : '想听什么？' }}</h1>
        <span v-if="!recentOnly">在顶部搜索框中搜索歌曲、歌手或专辑。</span>
        <label v-if="bots.length" class="bot-select">
          <span class="bot-select-label">控制机器人</span>
          <span class="bot-select-control">
            <i class="bot-select-icon">♫</i>
            <select v-model="botId" aria-label="选择控制机器人" @change="selectBot">
              <option v-for="bot in bots" :key="bot.id" :value="bot.id">{{ bot.name }} · {{ statusText(bot.status) }}</option>
            </select>
            <i class="bot-select-chevron">⌄</i>
          </span>
        </label>
      </header>
      <p v-if="error" class="error">{{ error }}</p>

      <section v-if="!recentOnly && results.length">
        <h2>搜索结果</h2>
        <article v-for="track in results" :key="track.resid + track.type">
          <img v-if="cover(track)" :src="cover(track)" :alt="track.title">
          <i v-else>♫</i>
          <div><b>{{ track.title || '未命名歌曲' }}</b></div>
          <button :disabled="busy" @click="play(track)">播放</button>
          <button :disabled="busy" @click="add(track)">加入</button>
        </article>
      </section>

      <section v-if="recentOnly">
        <h2>最近播放</h2>
        <article v-for="track in state.recent" :key="track.resource.resid + track.type">
          <img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title">
          <i v-else>♫</i>
          <div><b>{{ track.title }}</b></div>
          <button :disabled="busy" @click="play(track.resource)">播放</button>
        </article>
        <p v-if="!state.recent.length">还没有播放记录。</p>
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
      <ConsoleQueueDrawer :open="queueOpen" :queue="state.queue" :is-admin="isAdmin" @close="queueOpen = false" @clear="clear"/>
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
.music { max-width: 1050px; margin: auto; padding: 44px; }
.music header p { color: #287f74; font-weight: bold; }
.bot-select { display: inline-flex; align-items: center; gap: 12px; margin-top: 16px; color: #536572; font-size: 13px; }
.bot-select-label { color: #667684; font-weight: 600; white-space: nowrap; }
.bot-select-control { position: relative; display: flex; align-items: center; min-width: 250px; }
.bot-select-icon { position: absolute; z-index: 1; left: 11px; width: 24px; height: 24px; display: grid; place-items: center; border-radius: 8px; background: #e4f5f1; color: #287f74; font-size: 14px; font-style: normal; pointer-events: none; }
.bot-select select { appearance: none; width: 100%; min-width: 0; height: 42px; margin: 0; padding: 0 38px 0 44px; border: 1px solid #d9e7e4; border-radius: 12px; outline: 0; background: #fff; color: var(--console-ink); font: inherit; cursor: pointer; box-shadow: 0 5px 14px rgba(50, 89, 83, .06); transition: border-color .18s ease, box-shadow .18s ease, background-color .18s ease; }
.bot-select select:hover { border-color: #a9d9d0; background: #fbfefd; }
.bot-select select:focus { border-color: #4fb8a8; box-shadow: 0 0 0 3px rgba(79, 184, 168, .14), 0 5px 14px rgba(50, 89, 83, .06); }
.bot-select-chevron { position: absolute; right: 13px; color: #70828b; font-size: 17px; font-style: normal; line-height: 1; pointer-events: none; transform: translateY(-2px); }
.music h1 { font-size: 32px; }
.music section { margin-top: 34px; }
.music article { display: flex; align-items: center; gap: 12px; padding: 10px; border-radius: 8px; }
.music article:hover { background: #edf7f5; }
.music article img, .music article i { width: 46px; height: 46px; border-radius: 7px; object-fit: cover; background: #dff1ed; display: grid; place-items: center; font-style: normal; }
.music article div { flex: 1; min-width: 0; }
.music b { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.music button { height: 34px; border: 0; border-radius: 6px; background: #4fb8a8; color: #fff; padding: 0 11px; cursor: pointer; }
.music button:disabled { opacity: .62; cursor: wait; }
.error { color: #b34d57; }
@media (max-width: 760px) { .music { padding: 28px 16px; } .bot-select { display: flex; align-items: stretch; flex-direction: column; gap: 7px; margin-top: 18px; } .bot-select-control { width: 100%; min-width: 0; } .bot-select select { width: 100%; } }
</style>
