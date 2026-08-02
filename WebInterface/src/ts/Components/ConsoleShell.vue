<template>
  <div class="shell">
    <aside class="side" aria-label="主导航">
      <router-link class="logo" to="/music" exact>
        <span class="logo-mark"><b-icon icon="music" size="is-small" /></span>
        <span class="logo-copy">{{ brandName }}</span>
      </router-link>

      <nav class="side-nav">
        <router-link to="/music" exact title="点歌"><b-icon icon="magnify" /><span>点歌</span></router-link>
        <router-link to="/recent" title="最近播放"><b-icon icon="history" /><span>最近播放</span></router-link>
        <router-link v-if="isAdmin" to="/admin" title="管理"><b-icon icon="cog-outline" /><span>管理</span></router-link>
      </nav>

      <div class="side-footer">
        <button v-if="isAdmin" type="button" class="version-chip" :class="{ update: hasUpdate }" :title="hasUpdate ? '发现新版本，点击更新' : ('当前版本 ' + currentVersion)" @click="openUpdate">
          <span>{{ displayVersion }}</span><em v-if="hasUpdate">有更新</em>
        </button>
        <span class="side-caption">TeamSpeak 音乐控制台</span>
      </div>
    </aside>

    <section class="shell-main">
      <header class="header">
        <form class="header-search" @submit.prevent="submitSearch">
          <b-icon icon="magnify" size="is-small" />
          <input v-model.trim="query" aria-label="搜索音乐" placeholder="搜索音乐、歌手或专辑">
          <button v-if="query" type="button" title="清除搜索" aria-label="清除搜索" @click="query = ''"><b-icon icon="close" size="is-small" /></button>
        </form>
        <div class="header-actions">
          <span class="bot-status" :class="connectionState"><i></i><span>{{ connectionLabel }}</span></span>
          <span class="bot-name" :title="botName || brandName">{{ botName || brandName }}</span>
          <button type="button" class="icon-button theme-button" title="切换主题" aria-label="切换主题" @click="toggleTheme">
            <b-icon :icon="theme === 'dark' ? 'white-balance-sunny' : 'weather-night'" size="is-small" />
          </button>
          <button type="button" class="logout-button" title="退出登录" @click="logout"><b-icon icon="logout" size="is-small" /><span>退出</span></button>
        </div>
      </header>
      <main class="shell-content"><slot/></main>
    </section>

    <nav class="mobile-nav" aria-label="移动端导航">
      <router-link to="/music" exact title="点歌"><b-icon icon="magnify" /><span>点歌</span></router-link>
      <router-link to="/recent" title="最近播放"><b-icon icon="history" /><span>最近</span></router-link>
      <router-link v-if="isAdmin" to="/admin" title="管理"><b-icon icon="cog-outline" /><span>管理</span></router-link>
    </nav>

    <UpdatePanel :open="updateOpen" @close="updateOpen = false" @applied="onApplied"/>
    <DescriptionPermissionNotice ref="descNotice"/>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, ConsoleBot } from "../ConsoleApi";
import UpdatePanel from "./UpdatePanel.vue";
import DescriptionPermissionNotice from "./DescriptionPermissionNotice.vue";

export default Vue.extend({
  components: { UpdatePanel, DescriptionPermissionNotice },
  data() {
    return {
      brandName: "波点音乐",
      botName: "",
      isAdmin: false,
      query: "",
      currentVersion: "",
      hasUpdate: false,
      updateOpen: false,
      theme: (document.documentElement.getAttribute("data-theme") || "light") as "light" | "dark",
      pollTimer: 0 as any,
      connectionTimer: 0 as any,
      connectionState: "offline" as "connected" | "connecting" | "offline",
    };
  },
  computed: {
    displayVersion(): string {
      const v = this.currentVersion || "unknown";
      return v.startsWith("v") || v.startsWith("build") ? v : ("v" + v);
    },
    connectionLabel(): string {
      return this.connectionState === "connected" ? "已连接" : this.connectionState === "connecting" ? "连接中" : "离线";
    },
    connectionTitle(): string { return "机器人状态：" + this.connectionLabel; },
  },
  async created() {
    try {
      const user = await consoleApi<ConsoleUser>("me");
      this.brandName = user.brandName;
      this.isAdmin = user.role === "admin";
      if (this.isAdmin) {
        await this.refreshUpdateBadge();
        this.pollTimer = setInterval(() => this.refreshUpdateBadge(), 10 * 60 * 1000);
        this.$nextTick(() => {
          const notice = this.$refs.descNotice as any;
          if (notice && notice.check) notice.check(true);
        });
      }
      await this.refreshConnectionStatus();
      this.connectionTimer = setInterval(() => this.refreshConnectionStatus(), 5000);
    } catch (_) { this.$router.replace("/"); }
  },
  beforeDestroy() {
    if (this.pollTimer) clearInterval(this.pollTimer);
    if (this.connectionTimer) clearInterval(this.connectionTimer);
  },
  methods: {
    submitSearch() {
      if (!this.query) return;
      this.$router.push({ path: "/music", query: { q: this.query } }).catch(() => {});
    },
    toggleTheme() {
      const next = this.theme === "dark" ? "light" : "dark";
      this.theme = next;
      document.documentElement.setAttribute("data-theme", next);
      window.localStorage.setItem("bd-theme", next);
    },
    async logout() { await consoleApi("logout", {}); this.$router.replace("/"); },
    openUpdate() { this.updateOpen = true; },
    onApplied() { this.hasUpdate = false; },
    async refreshUpdateBadge() {
      if (!this.isAdmin) return;
      try {
        const status = await consoleApi<{ currentVersion?: string }>("update/status");
        this.currentVersion = status.currentVersion || "";
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string }>("update/check", { source: "bodian" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) { /* release source outage must not break the shell */ }
    },
    async refreshConnectionStatus() {
      try {
        const result = await consoleApi<{ bots?: ConsoleBot[] }>("bots");
        const bots = result.bots || [];
        const current = bots.find(bot => bot.status === "connected") || bots.find(bot => bot.status === "connecting") || bots[0];
        this.botName = current && current.name ? current.name : "";
        const statuses = bots.map(bot => bot.status);
        this.connectionState = statuses.indexOf("connected") >= 0 ? "connected" : statuses.indexOf("connecting") >= 0 ? "connecting" : "offline";
      } catch (_) { this.connectionState = "offline"; }
    },
  },
});
</script>

<style scoped lang="less">
.shell { min-height: 100vh; background: var(--console-bg); }
.side {
  position: fixed; z-index: 4; inset: 0 auto 0 0; width: 240px; padding: 28px 18px 22px;
  display: flex; flex-direction: column; background: var(--console-surface); border-right: 1px solid var(--console-line);
}
.logo { display: flex; align-items: center; gap: 12px; padding: 0 10px; color: var(--console-ink); text-decoration: none; }
.logo-mark { width: 38px; height: 38px; display: grid; place-items: center; flex: 0 0 auto; border-radius: 11px; background: var(--console-brand); color: #fff; }
.logo-copy { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 17px; font-weight: 700; }
.side-nav { display: grid; gap: 5px; margin-top: 44px; }
.side-nav a { position: relative; display: flex; align-items: center; gap: 13px; height: 46px; padding: 0 14px; border-radius: 11px; color: var(--console-muted); text-decoration: none; font-weight: 600; }
.side-nav a::before { content: ""; position: absolute; left: -18px; width: 3px; height: 0; border-radius: 0 3px 3px 0; background: var(--console-brand); transition: height 160ms ease-out; }
.side-nav a.router-link-active { color: var(--console-brand); background: var(--console-brand-soft); }
.side-nav a.router-link-active::before { height: 24px; }
.side-nav a .icon { width: 20px; }
.side-footer { margin-top: auto; display: grid; gap: 12px; }
.side-caption { color: var(--console-muted-2); font-size: 11px; padding: 0 10px; }
.version-chip { min-height: 38px; display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 0 11px; border: 1px solid var(--console-line); border-radius: 10px; background: var(--console-surface-2); color: var(--console-muted); cursor: pointer; text-align: left; font-size: 12px; }
.version-chip.update { border-color: rgba(235, 173, 0, .35); color: var(--console-warn); }
.version-chip span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.version-chip em { padding: 2px 6px; border-radius: 999px; background: var(--console-warn-soft); color: var(--console-warn); font-size: 11px; font-style: normal; font-weight: 700; }
.shell-main { min-width: 0; min-height: 100vh; margin-left: 240px; padding-bottom: calc(var(--console-player-h) + 40px); }
.header { position: sticky; z-index: 3; top: 0; min-height: 72px; display: flex; align-items: center; gap: 24px; padding: 14px 40px; background: var(--console-glass); border-bottom: 1px solid var(--console-line); backdrop-filter: blur(20px) saturate(140%); }
.header-search { width: 480px; max-width: 50vw; height: 42px; display: flex; align-items: center; gap: 9px; padding: 0 12px 0 15px; border: 1px solid transparent; border-radius: var(--console-radius-full); background: var(--console-surface-2); color: var(--console-muted); transition: border-color 160ms ease, background-color 160ms ease; }
.header-search:focus-within { border-color: var(--console-line-strong); background: var(--console-surface-3); }
.header-search input { min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; color: var(--console-ink); }
.header-search input::placeholder { color: var(--console-muted-2); }
.header-search button, .icon-button { width: 30px; height: 30px; display: grid; place-items: center; padding: 0; border: 0; border-radius: 50%; background: transparent; color: var(--console-muted); cursor: pointer; }
.header-search button:hover, .icon-button:hover { background: var(--console-hover); color: var(--console-ink); }
.header-actions { min-width: 0; margin-left: auto; display: flex; align-items: center; gap: 12px; }
.bot-status { display: inline-flex; align-items: center; gap: 7px; color: var(--console-muted); font-size: 12px; white-space: nowrap; }
.bot-status i { width: 8px; height: 8px; display: block; border-radius: 50%; background: var(--console-muted-2); }
.bot-status.connected i { background: #30d158; box-shadow: 0 0 0 4px rgba(48, 209, 88, .14); }
.bot-status.connecting i { background: #ff9f0a; }
.bot-name { max-width: 160px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--console-ink); font-size: 13px; font-weight: 600; }
.logout-button { height: 36px; display: inline-flex; align-items: center; gap: 7px; padding: 0 10px; border: 0; border-radius: 9px; background: transparent; color: var(--console-muted); cursor: pointer; }
.logout-button:hover { background: var(--console-hover); color: var(--console-ink); }
.shell-content { min-height: calc(100vh - 72px); }
.mobile-nav { display: none; }

@media (max-width: 1023px) {
  .side { display: none; }
  .shell-main { margin-left: 0; padding-bottom: calc(var(--console-player-h) + var(--console-nav-h) + env(safe-area-inset-bottom) + 18px); }
  .header { min-height: 62px; padding: 10px 16px; gap: 10px; overflow: hidden; }
  .header-search { width: 0 !important; max-width: none !important; min-width: 0; flex: 1 1 0 !important; height: 40px; }
  .header-actions { width: 72px !important; min-width: 72px !important; flex: 0 0 72px !important; margin-left: 0; justify-content: flex-end; gap: 4px; }
  .bot-name, .bot-status, .logout-button span { display: none; }
  .logout-button, .theme-button { width: 34px; height: 34px; justify-content: center; padding: 0; }
  .mobile-nav { position: fixed; z-index: 7; inset: auto 0 0; height: calc(var(--console-nav-h) + env(safe-area-inset-bottom)); display: flex; align-items: stretch; padding: 5px 8px env(safe-area-inset-bottom); background: var(--console-glass); border-top: 1px solid var(--console-line); backdrop-filter: blur(20px) saturate(140%); }
  .mobile-nav a { flex: 1; min-width: 0; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 3px; border-radius: 10px; color: var(--console-muted); text-decoration: none; font-size: 11px; font-weight: 600; }
  .mobile-nav a.router-link-active { color: var(--console-brand); }
  .mobile-nav a .icon { height: 20px; }
}
</style>
