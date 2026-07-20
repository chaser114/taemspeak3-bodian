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
        <button class="logout" title="退出登录" @click="logout"><i class="symbol">⇥</i><span>退出登录</span></button>
      </div>
    </aside>

    <section class="shell-main">
      <header class="header">
        <div class="history-actions">
          <button title="返回" @click="$router.go(-1)">‹</button>
          <button title="前进" @click="$router.go(1)">›</button>
        </div>
        <form class="header-search" @submit.prevent="submitSearch">
          <i class="symbol">⌕</i>
          <input v-model.trim="query" placeholder="搜索音乐、歌手或专辑">
          <button title="搜索">→</button>
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
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string; latestVersion?: string }>("update/check", { source: "gitee" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) {
        // Silent: network / gitee downtime should not break the console shell.
      }
    },
  },
});
</script>

<style scoped lang="less">
.shell { min-height: 100vh; display: flex; background: var(--console-canvas); }
.symbol { font-style: normal; font-size: 19px; line-height: 1; }
.side {
  position: fixed; z-index: 4; top: 0; bottom: 0; width: 216px; padding: 24px 14px;
  background: #fff; border-right: 1px solid var(--console-line); display: flex; flex-direction: column;
}
.logo {
  display: flex; align-items: center; gap: 10px; padding: 0 9px; color: var(--console-ink);
  text-decoration: none; font-size: 19px;
}
.logo span {
  width: 34px; height: 34px; display: grid; place-items: center; color: #fff;
  background: var(--console-brand); border-radius: 10px;
}
.side nav { display: grid; gap: 6px; margin-top: 42px; }
.side nav a, .logout {
  height: 44px; display: flex; align-items: center; gap: 14px; padding: 0 13px; border: 0;
  border-radius: 9px; background: transparent; color: #647182; font: inherit; text-decoration: none; cursor: pointer;
}
.side nav a.router-link-active { color: #fff; background: var(--console-brand); }
.side-footer { margin-top: auto; display: grid; gap: 8px; }
.version-chip {
  width: 100%; min-height: 40px; display: flex; align-items: center; justify-content: space-between;
  gap: 8px; padding: 8px 12px; border: 1px solid #e4ecec; border-radius: 9px; background: #f7fafa;
  color: #6a7885; font: inherit; font-size: 12px; cursor: pointer; text-align: left;
}
.version-chip span { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.version-chip em {
  flex: 0 0 auto; padding: 2px 6px; border-radius: 999px; background: #fff4d6;
  color: #9a6b00; font-size: 11px; font-style: normal; font-weight: 700;
}
.version-chip.update {
  border-color: #f0d48a; background: #fff8e6; color: #8a6500; box-shadow: 0 0 0 1px rgba(240, 212, 138, 0.35);
}
.logout { width: 100%; color: #7e8792; }
.shell-main { min-width: 0; flex: 1; margin-left: 216px; padding-bottom: 104px; }
.header {
  height: 76px; display: flex; align-items: center; gap: 18px; padding: 0 34px;
  background: rgba(255,255,255,.9); border-bottom: 1px solid var(--console-line);
  position: sticky; top: 0; z-index: 3; backdrop-filter: blur(14px);
}
.history-actions { display: flex; gap: 8px; }
.history-actions button {
  width: 34px; height: 34px; border: 0; border-radius: 50%; background: #eef2f3; color: #566473;
  cursor: pointer; font-size: 25px;
}
.header-search {
  width: 55vw; max-width: 520px; height: 42px; display: flex; align-items: center; gap: 8px;
  padding-left: 14px; border-radius: 22px; background: #f2f5f5; color: #8b97a4;
}
.header-search input {
  min-width: 0; flex: 1; border: 0; outline: 0; background: transparent; font: inherit; color: var(--console-ink);
}
.header-search button {
  width: 40px; height: 40px; border: 0; border-radius: 50%; background: transparent; color: #435260;
  cursor: pointer; font-size: 18px;
}
.account { margin-left: auto; display: flex; align-items: center; gap: 8px; color: #778494; font-size: 13px; }
.shell-content { min-height: calc(100vh - 76px); }
.mobile-nav { display: none; }
@media (max-width: 760px) {
  .side { display: none; }
  .shell-main { margin-left: 0; padding-bottom: calc(160px + env(safe-area-inset-bottom)); }
  .header { height: 64px; gap: 10px; padding: 0 14px; }
  .history-actions { display: none; }
  .header-search { width: auto; flex: 1; max-width: none; }
  .account { display: none; }
  .mobile-nav {
    position: fixed; z-index: 5; left: 0; right: 0; bottom: 0; height: calc(58px + env(safe-area-inset-bottom));
    display: flex; align-items: stretch; padding-bottom: env(safe-area-inset-bottom);
    background: rgba(255,255,255,.97); border-top: 1px solid var(--console-line);
  }
  .mobile-nav a, .mobile-nav button {
    flex: 1 1 0; min-width: 0; display: grid; place-items: center; gap: 2px; border: 0;
    background: transparent; color: #6a7885; font: inherit; font-size: 11px; text-decoration: none; cursor: pointer;
  }
  .mobile-nav a.router-link-active { color: var(--console-brand); }
}
</style>
