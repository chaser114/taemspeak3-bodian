<template>
  <div class="shell">
    <aside class="side">
      <router-link class="logo" to="/music">
        <span class="symbol">♪</span>
        <strong>{{ brandName }}</strong>
      </router-link>
      <nav>
        <router-link to="/music" title="点歌"><i class="symbol">⌕</i><span>点歌</span></router-link>
        <router-link to="/recent" title="最近播放"><i class="symbol">↶</i><span>最近播放</span></router-link>
        <router-link v-if="isAdmin" to="/admin" title="管理"><i class="symbol">⚙</i><span>管理</span></router-link>
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
        <button type="button" class="logout" title="退出登录" @click="logout"><i class="symbol">⇥</i><span>退出登录</span></button>
      </div>
    </aside>

    <section class="shell-main">
      <header class="header">
        <div class="history-actions">
          <button type="button" title="返回" @click="$router.go(-1)">‹</button>
          <button type="button" title="前进" @click="$router.go(1)">›</button>
        </div>
        <form class="header-search" @submit.prevent="submitSearch">
          <i class="symbol">⌕</i>
          <input v-model.trim="query" placeholder="搜索音乐、歌手或专辑">
          <button type="submit" title="搜索">→</button>
        </form>
        <div class="account">
          <span>{{ brandName }}</span>
          <i class="symbol">●</i>
        </div>
      </header>
      <main class="shell-content"><slot/></main>
    </section>

    <nav class="mobile-nav">
      <router-link to="/music" title="点歌"><i class="symbol">⌕</i><span>点歌</span></router-link>
      <router-link to="/recent" title="最近播放"><i class="symbol">↶</i><span>最近</span></router-link>
      <router-link v-if="isAdmin" to="/admin" title="管理"><i class="symbol">⚙</i><span>管理</span></router-link>
      <button type="button" title="退出登录" @click="logout"><i class="symbol">⇥</i><span>退出</span></button>
    </nav>

    <UpdatePanel :open="updateOpen" @close="updateOpen = false" @applied="onApplied"/>
    <DescriptionPermissionNotice ref="descNotice"/>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser } from "../ConsoleApi";
import UpdatePanel from "./UpdatePanel.vue";
import DescriptionPermissionNotice from "./DescriptionPermissionNotice.vue";

export default Vue.extend({
  components: { UpdatePanel, DescriptionPermissionNotice },
  data() {
    return {
      brandName: "波点音乐",
      isAdmin: false,
      query: "",
      currentVersion: "",
      hasUpdate: false,
      updateOpen: false,
      pollTimer: 0 as any,
    };
  },
  computed: {
    displayVersion(): string {
      const v = this.currentVersion || "unknown";
      return v.startsWith("v") || v.startsWith("build") ? v : ("v" + v);
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
    } catch (_) {
      this.$router.replace("/");
    }
  },
  beforeDestroy() {
    if (this.pollTimer) clearInterval(this.pollTimer);
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
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string; latestVersion?: string }>("update/check", { source: "github-cn" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) {
        // Silent: network / gitcode downtime should not break the console shell.
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
  display: flex; flex-direction: column;
}
.logo {
  display: flex; align-items: center; gap: 10px; padding: 0 10px; color: var(--console-ink);
  text-decoration: none; font-size: 18px; font-weight: 700;
}
.logo span {
  width: 36px; height: 36px; display: grid; place-items: center; color: #fff;
  background: var(--console-brand); border-radius: var(--console-radius-sm);
  box-shadow: 0 6px 14px rgba(79, 184, 168, 0.28);
}
.side nav { display: grid; gap: 6px; margin-top: 40px; }
.side nav a, .logout {
  height: 44px; display: flex; align-items: center; gap: 12px; padding: 0 14px; border: 0;
  border-radius: var(--console-radius-sm); background: transparent; color: #647182;
  font: inherit; text-decoration: none; cursor: pointer;
}
.side nav a.router-link-active {
  color: #fff; background: var(--console-brand);
  box-shadow: 0 8px 18px rgba(79, 184, 168, 0.22);
}
.side-footer { margin-top: auto; display: grid; gap: 8px; }
.version-chip {
  width: 100%; min-height: 40px; display: flex; align-items: center; justify-content: space-between;
  gap: 8px; padding: 8px 12px; border: 1px solid var(--console-line); border-radius: var(--console-radius-sm);
  background: #f7fafa; color: #6a7885; font: inherit; font-size: 12px; cursor: pointer; text-align: left;
}
.version-chip span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.version-chip em {
  flex: 0 0 auto; padding: 2px 7px; border-radius: 999px; background: var(--console-warn-soft);
  color: var(--console-warn); font-size: 11px; font-style: normal; font-weight: 700;
}
.version-chip.update {
  border-color: #f0d48a; background: var(--console-warn-soft); color: #8a6500;
}
.logout { width: 100%; color: #7e8792; }
.logout:hover { background: #f3f5f6; color: var(--console-ink); }
.shell-main { min-width: 0; flex: 1; margin-left: 220px; padding-bottom: calc(var(--console-player-h) + 12px); }
.header {
  height: 72px; display: flex; align-items: center; gap: 16px; padding: 0 28px;
  background: rgba(255, 255, 255, 0.92); border-bottom: 1px solid var(--console-line);
  position: sticky; top: 0; z-index: 3; backdrop-filter: blur(14px);
}
.history-actions { display: flex; gap: 8px; }
.history-actions button {
  width: 36px; height: 36px; border: 0; border-radius: 50%; background: #eef2f3; color: #566473;
  cursor: pointer; font-size: 22px;
}
.header-search {
  width: 52vw; max-width: 520px; height: 44px; display: flex; align-items: center; gap: 8px;
  padding-left: 14px; border-radius: 999px; background: #f1f4f5; color: #8b97a4;
  border: 1px solid transparent;
}
.header-search:focus-within {
  border-color: rgba(79, 184, 168, 0.35);
  background: #fff;
  box-shadow: 0 0 0 3px rgba(79, 184, 168, 0.12);
}
.header-search input {
  min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; font: inherit; color: var(--console-ink);
}
.header-search button {
  width: 40px; height: 40px; border: 0; border-radius: 50%; background: transparent; color: #435260;
  cursor: pointer; font-size: 18px;
}
.account { margin-left: auto; display: flex; align-items: center; gap: 8px; color: #778494; font-size: 13px; }
.shell-content { min-height: calc(100vh - 72px); }
.mobile-nav { display: none; }

@media (max-width: 760px) {
  .side { display: none; }
  .shell-main {
    margin-left: 0;
    padding-bottom: calc(76px + var(--console-nav-h) + env(safe-area-inset-bottom) + 8px);
  }
  .header { height: 60px; gap: 10px; padding: 0 14px; }
  .history-actions { display: none; }
  .header-search { width: auto; flex: 1; max-width: none; height: 42px; }
  .account { display: none; }
  .mobile-nav {
    position: fixed; z-index: 5; left: 0; right: 0; bottom: 0;
    height: calc(var(--console-nav-h) + env(safe-area-inset-bottom));
    display: flex; align-items: stretch; padding: 4px 6px env(safe-area-inset-bottom);
    background: rgba(255, 255, 255, 0.98); border-top: 1px solid var(--console-line);
    box-shadow: 0 -6px 20px rgba(30, 50, 55, 0.05);
  }
  .mobile-nav a, .mobile-nav button {
    flex: 1 1 0; min-width: 0; min-height: 48px; display: grid; place-items: center; gap: 3px;
    border: 0; border-radius: var(--console-radius-sm); background: transparent; color: #6a7885;
    font: inherit; font-size: 12px; text-decoration: none; cursor: pointer;
  }
  .mobile-nav a .symbol, .mobile-nav button .symbol { font-size: 18px; }
  .mobile-nav a.router-link-active {
    color: var(--console-brand-dark);
    background: var(--console-brand-soft);
    font-weight: 700;
  }
}
</style>
