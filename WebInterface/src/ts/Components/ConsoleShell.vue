<template>
  <div class="shell">
    <aside class="side">
      <router-link class="logo" to="/music">
        <span class="symbol"><b-icon icon="music" size="is-small" /></span>
        <strong>{{ brandName }}</strong>
      </router-link>
      <nav>
        <router-link to="/music" title="点歌"><b-icon icon="magnify" /><span>点歌</span></router-link>
        <router-link to="/recent" title="最近播放"><b-icon icon="history" /><span>最近播放</span></router-link>
        <router-link v-if="isAdmin" to="/admin" title="管理"><b-icon icon="cog" /><span>管理</span></router-link>
      </nav>
      <div class="side-footer">
        <button
          v-if="isAdmin"
          type="button"
          class="version-chip"
          :class="{ update: hasUpdate }"
          :title="hasUpdate ? '发现新版本，点击更新' : ('当前版本 ' + currentVersion)"
          @click="openUpdate"
        >
          <span>{{ displayVersion }}</span>
          <em v-if="hasUpdate">有更新</em>
        </button>
      </div>
    </aside>

    <section class="shell-main">
      <header class="header">
        <form class="header-search" @submit.prevent="submitSearch">
          <b-icon icon="magnify" />
          <input v-model.trim="query" placeholder="搜索音乐、歌手或专辑">
          <button type="submit" title="搜索"><b-icon icon="arrow-right" size="is-small" /></button>
        </form>
        <div class="account">
          <span class="account-name">{{ botName || brandName }}</span>
          <span :class="['connection-dot', connectionState]" :title="connectionTitle" :aria-label="connectionTitle"></span>
          <button type="button" class="account-logout" title="退出登录" @click="logout"><b-icon icon="logout" size="is-small" /><span>退出</span></button>
        </div>
      </header>
      <main class="shell-content"><slot/></main>
    </section>

    <nav class="mobile-nav">
      <router-link to="/music" title="点歌"><b-icon icon="magnify" /><span>点歌</span></router-link>
      <router-link to="/recent" title="最近播放"><b-icon icon="history" /><span>最近</span></router-link>
      <router-link v-if="isAdmin" to="/admin" title="管理"><b-icon icon="cog" /><span>管理</span></router-link>
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
    connectionTitle(): string {
      return "机器人状态：" + this.connectionLabel;
    },
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
    } catch (_) {
      this.$router.replace("/");
    }
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
    async logout() {
      await consoleApi("logout", {});
      this.$router.replace("/");
    },
    openUpdate() {
      this.updateOpen = true;
    },
    onApplied() {
      this.hasUpdate = false;
    },
    async refreshUpdateBadge() {
      if (!this.isAdmin) return;
      try {
        const status = await consoleApi<{ currentVersion?: string }>("update/status");
        this.currentVersion = status.currentVersion || "";
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string; latestVersion?: string }>("update/check", { source: "bodian" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) {
        // Silent: release-source downtime should not break the console shell.
      }
    },
    async refreshConnectionStatus() {
      try {
        const result = await consoleApi<{ bots?: ConsoleBot[] }>("bots");
        const bots = result.bots || [];
        const current = bots.find(bot => bot.status === "connected")
          || bots.find(bot => bot.status === "connecting")
          || bots[0];
        this.botName = current && current.name ? current.name : "";
        const statuses = bots.map(bot => bot.status);
        this.connectionState = statuses.indexOf("connected") >= 0
          ? "connected"
          : statuses.indexOf("connecting") >= 0 ? "connecting" : "offline";
      } catch (_) {
        this.connectionState = "offline";
      }
    },
  },
});
</script>

<style scoped lang="less">
.shell { min-height: 100vh; display: flex; background: var(--console-canvas); }
.symbol { font-style: normal; font-size: 18px; line-height: 1; }
.side {
  position: fixed; z-index: 4; top: 0; bottom: 0; width: 220px; padding: 24px 14px;
  background: var(--console-surface); border-right: 1px solid var(--console-line);
  box-shadow: var(--console-shadow-sm);
  display: flex; flex-direction: column;
}
.logo {
  display: flex; align-items: center; gap: 12px; padding: 0 10px; color: var(--console-ink);
  text-decoration: none; font-size: 19px; font-weight: 700;
}
.logo span {
  width: 40px; height: 40px; display: grid; place-items: center; color: #fff;
  background: var(--console-brand); border-radius: var(--console-radius);
  box-shadow: 0 4px 12px rgba(79, 184, 168, 0.3);
}
.side nav { display: grid; gap: 6px; margin-top: 40px; }
.side nav a {
  height: 44px; display: flex; align-items: center; gap: 12px; padding: 0 14px; border: 0;
  border-radius: var(--console-radius-sm); background: transparent; color: #647182;
  font: inherit; text-decoration: none; cursor: pointer;
  position: relative;
  transition: all 0.2s ease;
}
.side nav a::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 0;
  background: var(--console-brand);
  border-radius: 0 2px 2px 0;
  transition: height 0.2s ease;
}
.side nav a.router-link-active {
  color: var(--console-brand-dark);
  background: var(--console-brand-soft);
  font-weight: 600;
}
.side nav a.router-link-active::before {
  height: 24px;
}
.side-footer { margin-top: auto; display: grid; gap: 8px; }
.version-chip {
  width: 100%; min-height: 40px; display: flex; align-items: center; justify-content: space-between;
  gap: 8px; padding: 8px 12px; border: 1px solid var(--console-line); border-radius: var(--console-radius-sm);
  background: var(--console-surface); color: #6a7885; font: inherit; font-size: 12px; cursor: pointer; text-align: left;
  box-shadow: var(--console-shadow-sm);
  transition: all 0.2s ease;
}
.version-chip:hover {
  box-shadow: var(--console-shadow);
  transform: translateY(-1px);
}
.version-chip span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.version-chip em {
  flex: 0 0 auto; padding: 2px 7px; border-radius: 999px; background: var(--console-warn-soft);
  color: var(--console-warn); font-size: 11px; font-style: normal; font-weight: 700;
}
.version-chip.update {
  border-color: #f0d48a; background: var(--console-warn-soft); color: #8a6500;
}
.shell-main { min-width: 0; flex: 1; margin-left: 220px; padding-bottom: calc(var(--console-player-h) + 12px); }
.header {
  height: 72px; display: flex; align-items: center; gap: 16px; padding: 0 28px;
  background: var(--console-surface); border-bottom: 1px solid var(--console-line);
  position: sticky; top: 0; z-index: 3;
  backdrop-filter: blur(10px);
  box-shadow: var(--console-shadow-sm);
}
.header-search {
  width: 52vw; max-width: 520px; height: 46px; display: flex; align-items: center; gap: 8px;
  padding-left: 16px; border-radius: var(--console-radius-full); background: var(--console-surface); color: #8b97a4;
  border: 1px solid transparent;
  box-shadow: var(--console-shadow-sm);
  transition: all 0.2s ease;
}
.header-search:focus-within {
  border-color: rgba(79, 184, 168, 0.35);
  background: #fff;
  box-shadow: 0 0 0 3px rgba(79, 184, 168, 0.12), var(--console-shadow);
}
.header-search input {
  min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; font: inherit; color: var(--console-ink);
}
.header-search button {
  width: 40px; height: 40px; border: 0; border-radius: 50%; background: transparent; color: #435260;
  cursor: pointer; font-size: 18px;
}
.account { margin-left: auto; display: flex; align-items: center; gap: 8px; color: #778494; font-size: 13px; }
.connection-dot { width: 9px; height: 9px; flex: 0 0 auto; border-radius: 50%; background: #aab5c0; box-shadow: 0 0 0 3px rgba(170, 181, 192, 0.14); }
.connection-dot.connected {
  background: #35b878;
  box-shadow: 0 0 0 3px rgba(53, 184, 120, 0.2);
  animation: pulse-connection 2s ease-in-out infinite;
}
@keyframes pulse-connection {
  0%, 100% { box-shadow: 0 0 0 3px rgba(53, 184, 120, 0.2); }
  50% { box-shadow: 0 0 0 6px rgba(53, 184, 120, 0.12); }
}
.connection-dot.connecting { background: #e0a52f; box-shadow: 0 0 0 3px rgba(224, 165, 47, 0.16); }
.account-logout {
  height: 36px; display: inline-flex; align-items: center; gap: 6px; padding: 0 12px;
  border: 0; border-radius: var(--console-radius-sm); background: transparent; color: #778494; cursor: pointer;
  transition: all 0.2s ease;
}
.account-logout:hover {
  background: var(--console-brand-soft);
  color: var(--console-brand-dark);
  transform: translateY(-1px);
}
.shell-content { min-height: calc(100vh - 72px); }
.mobile-nav { display: none; }

@media (max-width: 760px) {
  .side { display: none; }
  .shell-main {
    margin-left: 0;
    padding-bottom: calc(76px + var(--console-nav-h) + env(safe-area-inset-bottom) + 8px);
  }
  .header { height: 60px; gap: 10px; padding: 0 14px; }
  .header-search { width: auto; flex: 1; max-width: none; height: 42px; }
  .account { gap: 7px; }
  .account-name, .account-logout span { display: none; }
  .account-logout { width: 36px; padding: 0; justify-content: center; }
  .mobile-nav {
    position: fixed; z-index: 5; left: 0; right: 0; bottom: 0;
    height: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    display: flex; align-items: stretch; padding: 4px 6px env(safe-area-inset-bottom);
    background: rgba(255, 255, 255, 0.98); border-top: 1px solid var(--console-line);
    box-shadow: 0 -6px 20px rgba(30, 50, 55, 0.05);
  }
  .mobile-nav a, .mobile-nav button {
    flex: 1 1 0; min-width: 0; min-height: 48px; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 3px;
    border: 0; border-radius: var(--console-radius-sm); background: transparent; color: #6a7885;
    font: inherit; font-size: 12px; text-decoration: none; cursor: pointer;
  }
  .mobile-nav a.router-link-active {
    color: var(--console-brand-dark);
    background: var(--console-brand-soft);
    font-weight: 700;
  }
}
</style>
