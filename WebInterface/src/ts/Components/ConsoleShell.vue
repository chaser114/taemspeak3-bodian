<template>
  <div class="bd-shell" :class="{ 'with-panel': showQueuePanel }">
    <!-- ============ sidebar ============ -->
    <aside class="bd-side" aria-label="主导航">
      <router-link class="bd-brand" to="/music" exact>
        <span class="bd-brand-mark"><b-icon icon="music-note" size="is-small" /></span>
        <b class="bd-brand-name">{{ brandName }}</b>
      </router-link>

      <nav class="bd-side-group">
        <div class="bd-side-label">音乐</div>
        <router-link to="/music" exact title="点歌">
          <b-icon icon="magnify" /><span>点歌</span>
        </router-link>
        <router-link to="/recent" title="最近播放">
          <b-icon icon="history" /><span>最近播放</span>
        </router-link>
        <button type="button" class="bd-nav-button" :class="{ active: queueOpen }" title="待播队列" @click="toggleQueue">
          <b-icon icon="playlist-music" /><span>待播队列</span>
          <em v-if="queueCount">{{ queueCount }}</em>
        </button>
      </nav>

      <nav v-if="isAdmin" class="bd-side-group">
        <div class="bd-side-label">管理</div>
        <router-link to="/admin" title="管理">
          <b-icon icon="cog-outline" /><span>管理</span>
        </router-link>
        <button type="button" class="bd-nav-button" :class="{ update: hasUpdate }" title="程序更新" @click="updateOpen = true">
          <b-icon icon="update" /><span>程序更新</span>
          <em v-if="hasUpdate" class="dot">新</em>
        </button>
      </nav>

      <div class="bd-side-foot">
        <div class="bd-account-row">
          <span class="bd-avatar" :class="{ guest: isGuest }">{{ avatarText }}</span>
          <span class="bd-account-copy">
            <b>{{ isGuest ? '访客模式' : username }}</b>
            <span>{{ roleLabel }}</span>
          </span>
          <button v-if="!isGuest" type="button" class="bd-icon-button" title="退出登录" aria-label="退出登录" @click="logout">
            <b-icon icon="logout" size="is-small" />
          </button>
        </div>
      </div>
    </aside>

    <!-- ============ main ============ -->
    <section class="bd-main">
      <header class="bd-header">
        <div class="bd-nav-pair">
          <button type="button" class="bd-icon-button" title="后退" aria-label="后退" @click="goBack"><b-icon icon="chevron-left" size="is-small" /></button>
          <button type="button" class="bd-icon-button" title="前进" aria-label="前进" @click="goForward"><b-icon icon="chevron-right" size="is-small" /></button>
        </div>

        <form class="bd-search-field" @submit.prevent="submitSearch">
          <b-icon icon="magnify" size="is-small" />
          <input v-model.trim="query" aria-label="搜索音乐" placeholder="搜索音乐、歌手或专辑">
          <button v-if="query" type="button" title="清除搜索" aria-label="清除搜索" @click="query = ''">
            <b-icon icon="close" size="is-small" />
          </button>
        </form>

        <div class="bd-header-actions">
          <span class="bd-status-chip" :class="connectionState" :title="'机器人状态：' + connectionLabel">
            <i></i><span class="bd-status-text">{{ connectionLabel }}</span>
          </span>
          <span v-if="botName" class="bd-bot-name" :title="botName">{{ botName }}</span>
          <button type="button" class="bd-icon-button" title="切换主题" aria-label="切换主题" @click="toggleTheme">
            <b-icon :icon="theme === 'dark' ? 'white-balance-sunny' : 'weather-night'" size="is-small" />
          </button>
          <button type="button" class="bd-icon-button bd-queue-toggle" title="待播队列" aria-label="待播队列" @click="toggleQueue">
            <b-icon icon="playlist-music" size="is-small" />
          </button>
          <button v-if="!isGuest" type="button" class="bd-logout-button" title="退出登录" @click="logout">
            <b-icon icon="logout" size="is-small" /><span>退出</span>
          </button>
        </div>
      </header>

      <main class="bd-content"><slot/></main>
    </section>

    <!-- ============ up next panel ============ -->
    <aside v-if="showQueuePanel" class="bd-up-next" aria-label="待播队列">
      <header class="bd-panel-head">
        <b>待播队列</b>
        <button type="button" class="bd-icon-button" title="收起队列" aria-label="收起队列" @click="queueOpen = false">
          <b-icon icon="chevron-right" size="is-small" />
        </button>
      </header>
      <p class="bd-panel-sub">{{ queueCount ? (queueCount + ' 首 · 点击立即切换') : '队列为空' }}</p>
      <div class="bd-panel-list">
        <button
          v-for="(track, index) in panelQueue"
          :key="track.resource.resid + '-' + index"
          type="button"
          class="bd-panel-item"
          :class="{ current: track.active }"
          @click="playQueued(track)"
        >
          <span class="bd-panel-art">
            <img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title">
            <b-icon v-else icon="music-note" size="is-small" />
          </span>
          <span class="bd-panel-copy">
            <b>{{ track.title }}</b>
            <span>{{ track.active ? '正在播放' : '点击切歌' }}</span>
          </span>
          <span v-if="track.active" class="bd-eq"><i></i><i></i><i></i></span>
        </button>
        <p v-if="!panelQueue.length" class="bd-panel-empty">还没有加入任何歌曲。</p>
      </div>
      <footer v-if="isAdmin && queueCount" class="bd-panel-foot">
        <small>仅管理员可清空队列</small>
        <button type="button" class="bd-pill danger" @click="clearQueue">清空</button>
      </footer>
    </aside>

    <!-- ============ mobile nav ============ -->
    <nav class="bd-mobile-nav" aria-label="移动端导航">
      <router-link to="/music" exact title="点歌"><b-icon icon="magnify" /><span>点歌</span></router-link>
      <router-link to="/recent" title="最近播放"><b-icon icon="history" /><span>最近</span></router-link>
      <button type="button" class="bd-mobile-tab" :class="{ active: queueOpen }" title="待播队列" @click="toggleQueue">
        <b-icon icon="playlist-music" /><span>队列</span>
      </button>
      <router-link v-if="isAdmin" to="/admin" title="管理"><b-icon icon="cog-outline" /><span>管理</span></router-link>
      <button v-else type="button" class="bd-mobile-tab" title="设置" @click="logout"><b-icon icon="theme-light-dark" /><span>主题</span></button>
    </nav>

    <UpdatePanel :open="updateOpen" @close="updateOpen = false" @applied="onApplied"/>
    <DescriptionPermissionNotice ref="descNotice"/>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, ConsoleBot, MusicState, Track } from "../ConsoleApi";
import UpdatePanel from "./UpdatePanel.vue";
import DescriptionPermissionNotice from "./DescriptionPermissionNotice.vue";

export default Vue.extend({
  components: { UpdatePanel, DescriptionPermissionNotice },
  data() {
    return {
      brandName: "波点音乐",
      botName: "",
      username: "",
      isAdmin: false,
      isGuest: false,
      query: "",
      currentVersion: "",
      hasUpdate: false,
      updateOpen: false,
      queueOpen: false,
      theme: (document.documentElement.getAttribute("data-theme") || "light") as "light" | "dark",
      pollTimer: 0 as any,
      connectionTimer: 0 as any,
      connectionState: "offline" as "connected" | "connecting" | "offline",
      panelQueue: [] as Track[],
      queueCount: 0,
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
    avatarText(): string {
      if (this.isGuest) return "游";
      return (this.username || "A").slice(0, 1).toUpperCase();
    },
    roleLabel(): string {
      if (this.isGuest) return "只读访客";
      return (this.isAdmin ? "管理员" : "普通用户") + (this.displayVersion !== "vunknown" ? (" · " + this.displayVersion) : "");
    },
    showQueuePanel(): boolean {
      return this.queueOpen;
    },
  },
  async created() {
    try {
      const user = await consoleApi<ConsoleUser>("me");
      this.username = user.username || "";
      this.isAdmin = user.role === "admin";
      this.isGuest = user.role === "guest";
      this.brandName = user.brandName;
      document.title = user.brandName;
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
    window.addEventListener("console-queue", this.onQueueEvent as any);
    window.addEventListener("console-queue-open", this.openQueue as any);
    // Ask the page for the current queue once the shell is listening.
    this.$nextTick(() => window.dispatchEvent(new CustomEvent("console-queue-sync")));
  },
  beforeDestroy() {
    if (this.pollTimer) clearInterval(this.pollTimer);
    if (this.connectionTimer) clearInterval(this.connectionTimer);
    window.removeEventListener("console-queue", this.onQueueEvent as any);
    window.removeEventListener("console-queue-open", this.openQueue as any);
  },
  methods: {
    onQueueEvent(event: any) {
      const detail = event && event.detail ? event.detail : {};
      this.panelQueue = detail.queue || [];
      this.queueCount = this.panelQueue.length;
    },
    toggleQueue() { this.queueOpen = !this.queueOpen; },
    openQueue() { this.queueOpen = true; },
    playQueued(track: Track) {
      if (track.active) return;
      window.dispatchEvent(new CustomEvent("console-queue-play", { detail: track.resource }));
    },
    clearQueue() {
      window.dispatchEvent(new CustomEvent("console-queue-clear"));
    },
    goBack() { this.$router.back(); },
    goForward() { this.$router.forward(); },
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
.bd-shell {
  display: grid;
  grid-template-columns: var(--console-side-w) minmax(0, 1fr);
  min-height: 100vh;
  background: var(--console-bg);
  transition: grid-template-columns .24s var(--console-ease-drawer);
}
.bd-shell.with-panel { grid-template-columns: var(--console-side-w) minmax(0, 1fr) var(--console-panel-w); }

/* ---------- sidebar ---------- */
.bd-side {
  position: sticky; top: 0; z-index: 4;
  height: 100vh; display: flex; flex-direction: column;
  padding: 0 10px 10px;
  background: var(--console-side);
  border-right: 1px solid var(--console-line);
  overflow-y: auto;
}
.bd-brand {
  height: var(--console-header-h); flex: 0 0 auto;
  display: flex; align-items: center; gap: 10px;
  padding: 0 8px; color: var(--console-ink); text-decoration: none;
}
.bd-brand-mark {
  width: 30px; height: 30px; flex: 0 0 auto; display: grid; place-items: center;
  border-radius: 8px; color: #fff;
  background: linear-gradient(150deg, #ff4b5c, var(--console-brand) 55%, #c9152c);
  box-shadow: 0 1px 5px rgba(250, 35, 59, .35);
}
.bd-brand-name {
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  font-size: 14px; font-weight: 700; letter-spacing: -.01em;
}

.bd-side-group { display: flex; flex-direction: column; gap: 1px; margin-top: 14px; }
.bd-side-label {
  padding: 0 10px 6px; color: var(--console-muted-2);
  font-size: 11px; font-weight: 700; letter-spacing: .02em;
}
.bd-side-group a, .bd-nav-button {
  position: relative;
  display: flex; align-items: center; gap: 10px;
  width: 100%; height: 34px; padding: 0 10px;
  border: 0; border-radius: 8px;
  background: transparent; color: var(--console-ink);
  font-size: 13.5px; font-weight: 500; text-align: left;
  text-decoration: none; cursor: pointer;
  transition: background .12s ease;
}
.bd-side-group a .icon, .bd-nav-button .icon { color: var(--console-muted); }
.bd-side-group a:hover, .bd-nav-button:hover { background: var(--console-hover); }
.bd-side-group a.router-link-active, .bd-nav-button.active { background: var(--console-fill); font-weight: 600; }
.bd-side-group a.router-link-active .icon, .bd-nav-button.active .icon { color: var(--console-brand); }
.bd-nav-button em {
  margin-left: auto; font-style: normal;
  font-size: 11.5px; font-weight: 700; color: var(--console-muted-2);
}
.bd-nav-button.update em.dot { color: var(--console-brand); }

.bd-side-foot { margin-top: auto; padding-top: 10px; }
.bd-account-row {
  display: flex; align-items: center; gap: 9px;
  padding: 6px 8px; border-radius: 9px;
}
.bd-account-row:hover { background: var(--console-hover); }
.bd-avatar {
  width: 26px; height: 26px; flex: 0 0 auto; display: grid; place-items: center;
  border-radius: 50%; color: #fff;
  background: linear-gradient(140deg, #7d7aff, #4f46e5);
  font-size: 11px; font-weight: 700;
}
.bd-avatar.guest { background: linear-gradient(140deg, #8e8e93, #5b5b60); }
.bd-account-copy { min-width: 0; flex: 1; }
.bd-account-copy b, .bd-account-copy span {
  display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.bd-account-copy b { font-size: 12.5px; font-weight: 600; }
.bd-account-copy span { color: var(--console-muted-2); font-size: 11px; }

/* ---------- main ---------- */
.bd-main { min-width: 0; display: flex; flex-direction: column; }
.bd-header {
  position: sticky; top: 0; z-index: 3;
  height: var(--console-header-h); flex: 0 0 auto;
  display: flex; align-items: center; gap: 14px;
  padding: 0 26px;
  background: var(--console-bg);
  border-bottom: 1px solid transparent;
}
.bd-nav-pair { display: flex; gap: 2px; flex: 0 0 auto; }
.bd-search-field {
  display: flex; align-items: center; gap: 7px;
  width: 300px; height: 32px; padding: 0 10px;
  border-radius: 8px; background: var(--console-fill);
  color: var(--console-muted-2);
}
.bd-search-field input {
  flex: 1; min-width: 0; border: 0; outline: 0;
  background: transparent; color: var(--console-ink); font-size: 13px;
}
.bd-search-field input::placeholder { color: var(--console-muted-2); }
.bd-search-field button {
  width: 20px; height: 20px; display: grid; place-items: center;
  border: 0; background: transparent; color: var(--console-muted-2); cursor: pointer;
}
.bd-header-actions { margin-left: auto; display: flex; align-items: center; gap: 6px; }
.bd-status-chip {
  display: inline-flex; align-items: center; gap: 6px;
  height: 26px; padding: 0 10px 0 8px; border-radius: 999px;
  background: var(--console-fill); color: var(--console-muted);
  font-size: 11.5px; font-weight: 600; white-space: nowrap;
}
.bd-status-chip i { width: 7px; height: 7px; border-radius: 50%; background: var(--console-muted-2); }
.bd-status-chip.connected i { background: var(--console-ok); box-shadow: 0 0 0 3px var(--console-ok-soft); }
.bd-status-chip.connecting i { background: var(--console-warn); box-shadow: 0 0 0 3px var(--console-warn-soft); }
.bd-status-chip.offline i { background: var(--console-muted-2); }
.bd-status-chip.connected { color: var(--console-ok); }
.bd-status-chip.connecting { color: var(--console-warn); }
.bd-bot-name {
  max-width: 150px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  color: var(--console-ink); font-size: 13px; font-weight: 600;
}
.bd-logout-button {
  height: 30px; display: inline-flex; align-items: center; gap: 6px;
  padding: 0 9px; border: 0; border-radius: 8px;
  background: transparent; color: var(--console-muted); cursor: pointer; font-size: 12.5px;
}
.bd-logout-button:hover { background: var(--console-hover); color: var(--console-ink); }
.bd-queue-toggle { display: none; }

.bd-content { flex: 1; min-height: 0; padding-bottom: var(--console-player-h); }

/* ---------- up next ---------- */
.bd-up-next {
  position: sticky; top: 0; z-index: 2;
  height: 100vh; display: flex; flex-direction: column;
  background: var(--console-side);
  border-left: 1px solid var(--console-line);
}
.bd-panel-head {
  height: var(--console-header-h); flex: 0 0 auto;
  display: flex; align-items: center; gap: 8px;
  padding: 0 16px;
}
.bd-panel-head b { font-size: 17px; font-weight: 750; letter-spacing: -.015em; }
.bd-panel-head .icon-button { margin-left: auto; }
.bd-panel-sub { margin: 0; padding: 0 16px 8px; color: var(--console-muted); font-size: 12px; }
.bd-panel-list { flex: 1; min-height: 0; overflow-y: auto; padding: 0 8px; }
.bd-panel-item {
  width: 100%; display: flex; align-items: center; gap: 10px;
  height: 52px; padding: 0 8px;
  border: 0; border-radius: 9px; background: transparent;
  text-align: left; cursor: pointer;
}
.bd-panel-item:hover { background: var(--console-hover); }
.bd-panel-item.current { background: var(--console-brand-soft); }
.bd-panel-art {
  width: 36px; height: 36px; flex: 0 0 auto; display: grid; place-items: center;
  overflow: hidden; border-radius: 6px;
  background: var(--console-surface-3); color: var(--console-brand);
}
.bd-panel-art img { width: 100%; height: 100%; object-fit: cover; }
.bd-panel-copy { min-width: 0; flex: 1; }
.bd-panel-copy b, .bd-panel-copy span {
  display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.bd-panel-copy b { font-size: 12.5px; font-weight: 600; }
.bd-panel-item.current .bd-panel-copy b { color: var(--console-brand); }
.bd-panel-copy span { color: var(--console-muted); font-size: 11.5px; }
.bd-panel-empty { padding: 20px 12px; color: var(--console-muted); font-size: 12.5px; text-align: center; }
.bd-panel-foot {
  flex: 0 0 auto; display: flex; align-items: center; justify-content: space-between;
  gap: 10px; padding: 12px 16px; border-top: 1px solid var(--console-line);
}
.bd-panel-foot small { color: var(--console-muted); font-size: 11.5px; }

.bd-eq { display: flex; align-items: flex-end; gap: 2px; height: 12px; flex: 0 0 auto; }
.bd-eq i { width: 2.5px; border-radius: 1px; background: var(--console-brand); animation: eq .9s ease-in-out infinite; }
.bd-eq i:nth-child(1) { height: 6px; }
.bd-eq i:nth-child(2) { height: 12px; animation-delay: .18s; }
.bd-eq i:nth-child(3) { height: 8px; animation-delay: .36s; }
@keyframes eq { 0%, 100% { transform: scaleY(.4); } 50% { transform: scaleY(1); } }

.bd-mobile-nav { display: none; }

/* ---------- responsive ---------- */
@media (max-width: 1279px) {
  .bd-shell.with-panel { grid-template-columns: var(--console-side-w) minmax(0, 1fr); }
  .bd-up-next {
    position: fixed; z-index: 9; inset: 0 0 0 auto;
    width: 320px; max-width: 88vw;
    box-shadow: -18px 0 50px rgba(0, 0, 0, .18);
  }
}

@media (max-width: 1023px) {
  .bd-shell { grid-template-columns: minmax(0, 1fr); }
  .bd-side { display: none; }
  .bd-header { gap: 10px; padding: 0 14px; }
  .bd-nav-pair { display: none; }
  .bd-search-field { width: auto; flex: 1; min-width: 0; }
  .bd-header-actions { margin-left: 0; gap: 2px; flex: 0 0 auto; }
  .bd-status-chip .bd-status-text, .bd-bot-name, .bd-logout-button, .bd-logout-button span { display: none; }
  .bd-status-chip { padding: 0 8px; }
  .bd-queue-toggle { display: grid; }
  .bd-content { padding-bottom: calc(var(--console-player-h) + var(--console-nav-h) + env(safe-area-inset-bottom)); }
  .bd-up-next { inset: auto 0 0 0; width: 100%; max-width: none; height: 72vh; border-left: 0; border-top: 1px solid var(--console-line); border-radius: 18px 18px 0 0; box-shadow: 0 -18px 50px rgba(0, 0, 0, .22); }
  .bd-panel-head { border-bottom: 1px solid var(--console-line); }
  .bd-panel-foot { padding-bottom: calc(12px + env(safe-area-inset-bottom)); }
  .bd-mobile-nav {
    position: fixed; z-index: 8; inset: auto 0 0 0;
    height: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    display: flex; align-items: stretch;
    padding: 5px 8px env(safe-area-inset-bottom);
    background: var(--console-glass);
    border-top: 1px solid var(--console-line);
    backdrop-filter: blur(22px) saturate(160%);
  }
  .bd-mobile-nav a, .bd-mobile-tab {
    flex: 1; min-width: 0; display: flex; flex-direction: column;
    align-items: center; justify-content: center; gap: 3px;
    border: 0; border-radius: 10px; background: transparent;
    color: var(--console-muted); font-size: 10.5px; font-weight: 600;
    text-decoration: none; cursor: pointer;
  }
  .bd-mobile-nav a.router-link-active, .bd-mobile-tab.active { color: var(--console-brand); }
  .bd-mobile-nav .icon { height: 20px; }
}
</style>
