<template>
  <main class="music">
    <section v-if="!state.configured"><h1>还没有连接机器人</h1></section>
    <template v-else>
      <header>
        <p>{{ recentOnly ? '最近播放' : '点歌' }}</p>
        <h1>{{ recentOnly ? '最近播放' : '想听什么？' }}</h1>
        <span v-if="!recentOnly">在顶部搜索框中搜索歌曲、歌手或专辑。</span>
        <label v-if="bots.length" class="bot-select">控制机器人<select v-model="botId" @change="selectBot"><option v-for="bot in bots" :key="bot.id" :value="bot.id">{{ bot.name }} · {{ statusText(bot.status) }}</option></select></label>
      </header>
      <p v-if="error" class="error">{{ error }}</p>

      <section v-if="!recentOnly && results.length">
        <h2>搜索结果</h2>
        <article v-for="track in results" :key="track.resid + track.type">
          <img v-if="cover(track)" :src="cover(track)" :alt="track.title">
          <i v-else>♫</i>
          <div><b>{{ track.title || '未命名歌曲' }}</b></div>
          <button @click="play(track)">播放</button>
          <button @click="add(track)">加入</button>
        </article>
      </section>

      <section v-if="recentOnly">
        <h2>最近播放</h2>
        <article v-for="track in state.recent" :key="track.resource.resid + track.type">
          <img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title">
          <i v-else>♫</i>
          <div><b>{{ track.title }}</b></div>
          <button @click="play(track.resource)">播放</button>
        </article>
        <p v-if="!state.recent.length">还没有播放记录。</p>
      </section>

      <ConsolePlayerBar :state="state" @previous="control('previous')" @pause="control('pause')" @next="control('next')" @queue="queueOpen = true"/>
      <ConsoleQueueDrawer :open="queueOpen" :queue="state.queue" :is-admin="isAdmin" @close="queueOpen = false" @clear="clear"/>
    </template>
  </main>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, MusicState, TrackResource, ConsoleBot } from "../ConsoleApi";
import ConsolePlayerBar from "../Components/ConsolePlayerBar.vue";
import ConsoleQueueDrawer from "../Components/ConsoleQueueDrawer.vue";

const blank: MusicState = { configured: false, connected: false, current: null, queue: [], recent: [] };

export default Vue.extend({
  components: { ConsolePlayerBar, ConsoleQueueDrawer },
  props: { recentOnly: { type: Boolean, default: false } },
  data() {
    return { state: blank as MusicState, results: [] as TrackResource[], bots: [] as ConsoleBot[], botId: "", error: "", isAdmin: false, queueOpen: false, timer: 0 as any, listener: null as any };
  },
  async created() {
    const user = await consoleApi<ConsoleUser>("me");
    this.isAdmin = user.role === "admin";
    this.bots = (await consoleApi<{ bots: ConsoleBot[] }>("bots")).bots;
    this.botId = this.bots[0] ? this.bots[0].id : "";
    await this.refresh();
    const initialQuery = typeof this.$route.query.q === "string" ? this.$route.query.q : "";
    if (initialQuery) await this.search(initialQuery);
    this.timer = setInterval(this.refresh, 5000);
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
    async refresh() {
      try { this.state = await consoleApi<MusicState>("music/state?botId=" + encodeURIComponent(this.botId)); }
      catch (error) { this.error = error instanceof Error ? error.message : "状态同步失败。"; }
    },
    async search(query: string) {
      if (!query) return;
      try { this.results = (await consoleApi<{ results: TrackResource[] }>("music/search", { query, botId: this.botId })).results; this.error = ""; }
      catch (error) { this.error = error instanceof Error ? error.message : "搜索失败。"; }
    },
    async selectBot() {
      this.results = [];
      await this.refresh();
      const query = this.$route.query.q;
      if (!this.recentOnly && typeof query === "string" && query) await this.search(query);
    },
    async call(path: string, body: any = {}) {
      try { await consoleApi(path, { ...body, botId: this.botId }); await this.refresh(); }
      catch (error) { this.error = error instanceof Error ? error.message : "操作失败。"; }
    },
    play(resource: TrackResource) { return this.call("music/play", { resource }); },
    add(resource: TrackResource) { return this.call("music/add", { resource }); },
    control(name: string) { return this.call("music/" + name); },
    clear() { this.queueOpen = false; return this.call("music/clear"); },
  },
});
</script>

<style scoped lang="less">
.music { max-width: 1050px; margin: auto; padding: 44px; }
.music header p { color: #287f74; font-weight: bold; }
.bot-select { display: inline-flex; align-items: center; gap: 8px; margin-top: 14px; color: #536572; font-size: 13px; }
.bot-select select { width: auto; min-width: 180px; height: 34px; margin: 0; }
.music h1 { font-size: 32px; }
.music section { margin-top: 34px; }
.music article { display: flex; align-items: center; gap: 12px; padding: 10px; border-radius: 8px; }
.music article:hover { background: #edf7f5; }
.music img, .music i { width: 46px; height: 46px; border-radius: 7px; object-fit: cover; background: #dff1ed; display: grid; place-items: center; font-style: normal; }
.music article div { flex: 1; min-width: 0; }
.music b { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.music button { height: 34px; border: 0; border-radius: 6px; background: #4fb8a8; color: #fff; padding: 0 11px; cursor: pointer; }
.error { color: #b34d57; }
@media (max-width: 760px) { .music { padding: 28px 16px; } .bot-select { display: flex; align-items: stretch; flex-direction: column; } .bot-select select { width: 100%; } }
</style>
