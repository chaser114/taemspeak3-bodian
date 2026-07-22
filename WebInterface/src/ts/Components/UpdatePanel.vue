<template>
  <div v-if="open" class="update-mask" @click.self="close">
    <section class="update-panel" role="dialog" aria-modal="true" aria-labelledby="update-title">
      <header>
        <div>
          <p>程序更新</p>
          <h2 id="update-title">{{ hasUpdate ? '发现新版本' : '当前版本' }}</h2>
        </div>
        <button class="close" title="关闭" type="button" @click="close">×</button>
      </header>

      <div class="meta">
        <div><span>当前版本</span><b>{{ currentVersion || 'unknown' }}</b></div>
        <div><span>最新版本</span><b :class="{ newer: hasUpdate }">{{ latestVersion || '—' }}</b></div>
      </div>

      <p v-if="notes" class="notes">{{ notes }}</p>
      <p v-else-if="!loading && !error" class="notes muted">{{ hasUpdate ? '有可用更新。' : '已是最新版本，或暂时无法获取远程版本。' }}</p>

      <div class="sources">
        <button
          v-for="item in sources"
          :key="item.id"
          type="button"
          :class="['source', { active: source === item.id, unavailable: item.available === false }]"
          :disabled="busy || item.available === false"
          @click="source = item.id"
        >
          <strong>{{ item.label }}</strong>
          <small v-if="item.latestVersion">{{ item.latestVersion }}{{ item.hasUpdate ? ' · 可更新' : '' }}</small>
          <small v-else-if="item.available === false">暂不可用</small>
        </button>
      </div>

      <label class="password">
        管理员密码（二次确认）
        <input v-model="password" type="password" autocomplete="current-password" :disabled="busy" placeholder="输入当前管理员密码">
      </label>

      <p v-if="error" class="error">{{ error }}</p>
      <p v-if="message" class="message">{{ message }}</p>

      <footer>
        <button type="button" class="secondary" :disabled="busy" @click="refresh">重新检查</button>
        <button type="button" class="primary" :disabled="busy || !hasUpdate || !password" @click="apply">
          {{ busy ? '处理中…' : '立即更新' }}
        </button>
      </footer>
    </section>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi } from "../ConsoleApi";

interface SourceInfo {
  id: string;
  label: string;
  available?: boolean;
  latestVersion?: string;
  hasUpdate?: boolean;
  defaultSource?: boolean;
}

interface CheckResult {
  currentVersion?: string;
  latestVersion?: string;
  hasUpdate?: boolean;
  source?: string;
  notes?: string;
  sources?: SourceInfo[];
  errors?: string[];
}

export default Vue.extend({
  props: {
    open: { type: Boolean, default: false },
  },
  data() {
    return {
      loading: false,
      busy: false,
      currentVersion: "",
      latestVersion: "",
      hasUpdate: false,
      notes: "",
      source: "gitcode",
      sources: [
        { id: "gitcode", label: "GitCode 更新（国内服务器）", available: true },
        { id: "github", label: "GitHub 更新（官方源）", available: true },
      ] as SourceInfo[],
      password: "",
      error: "",
      message: "",
    };
  },
  watch: {
    open(value: boolean) {
      if (value) {
        this.password = "";
        this.message = "";
        this.error = "";
        this.refresh();
      }
    },
  },
  methods: {
    close() {
      if (this.busy) return;
      this.$emit("close");
    },
    async refresh() {
      this.loading = true;
      this.error = "";
      try {
        const result = await consoleApi<CheckResult>("update/check", { source: this.source });
        this.currentVersion = result.currentVersion || "";
        this.latestVersion = result.latestVersion || "";
        this.hasUpdate = !!result.hasUpdate;
        this.notes = result.notes || "";
        if (result.source) this.source = result.source;
        if (result.sources && result.sources.length) this.sources = result.sources;
        if (result.errors && result.errors.length && !result.latestVersion) {
          this.error = result.errors.join("；");
        }
      } catch (e) {
        this.error = e instanceof Error ? e.message : "检查更新失败。";
      } finally {
        this.loading = false;
      }
    },
    async apply() {
      if (!this.password || this.busy) return;
      this.busy = true;
      this.error = "";
      this.message = "";
      try {
        const result = await consoleApi<{ message?: string }>("update/apply", {
          source: this.source,
          password: this.password,
        });
        this.message = result.message || "更新已开始，程序会自动重启。约 10～20 秒后刷新网页。日志：logs/console.log；停止：stop 脚本。";
        this.$emit("applied");
        // Give the bot time to exit, replace files and come back.
        setTimeout(() => { window.location.reload(); }, 15000);
      } catch (e) {
        this.error = e instanceof Error ? e.message : "更新失败。";
      } finally {
        this.busy = false;
      }
    },
  },
});
</script>

<style scoped lang="less">
.update-mask {
  position: fixed;
  z-index: 30;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(25, 37, 48, 0.38);
}
.update-panel {
  width: 100%;
  max-width: 460px;
  padding: 24px;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 18px 54px rgba(20, 35, 45, 0.22);
}
header {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  align-items: flex-start;
}
header p {
  margin: 0;
  color: #287f74;
  font-size: 13px;
  font-weight: 700;
}
header h2 {
  margin: 6px 0 0;
  font-size: 22px;
}
.close {
  width: 34px;
  height: 34px;
  border: 0;
  border-radius: 50%;
  background: #edf1f2;
  color: #53616e;
  font-size: 24px;
  line-height: 1;
  cursor: pointer;
}
.meta {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-top: 20px;
}
.meta div {
  padding: 12px;
  border-radius: 8px;
  background: #f5f8f8;
}
.meta span {
  display: block;
  color: #778595;
  font-size: 12px;
}
.meta b {
  display: block;
  margin-top: 6px;
  font-size: 16px;
}
.meta b.newer {
  color: #b8860b;
}
.notes {
  margin: 16px 0 0;
  max-height: 120px;
  overflow: auto;
  color: #4c5d69;
  font-size: 13px;
  line-height: 1.55;
  white-space: pre-wrap;
}
.notes.muted {
  color: #8b97a3;
}
.sources {
  display: grid;
  gap: 8px;
  margin-top: 18px;
}
.source {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  width: 100%;
  padding: 12px 14px;
  border: 1px solid #d9e7e4;
  border-radius: 8px;
  background: #fff;
  color: var(--console-ink);
  text-align: left;
  cursor: pointer;
  font: inherit;
}
.source strong {
  font-size: 14px;
}
.source small {
  color: #778595;
  font-size: 12px;
}
.source.active {
  border-color: #4fb8a8;
  background: #edf7f5;
  box-shadow: 0 0 0 2px rgba(79, 184, 168, 0.14);
}
.source.unavailable {
  opacity: 0.55;
  cursor: not-allowed;
}
.password {
  display: block;
  margin-top: 16px;
  color: #44515e;
  font-size: 13px;
}
.password input {
  width: 100%;
  height: 42px;
  margin-top: 6px;
  padding: 0 12px;
  border: 1px solid #d5e0e3;
  border-radius: 8px;
  font: inherit;
}
.error {
  margin: 12px 0 0;
  color: #b34d57;
  font-size: 13px;
}
.message {
  margin: 12px 0 0;
  color: #197565;
  font-size: 13px;
}
footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
footer button {
  height: 40px;
  padding: 0 14px;
  border: 0;
  border-radius: 8px;
  font: inherit;
  cursor: pointer;
}
footer button:disabled {
  opacity: 0.6;
  cursor: wait;
}
.secondary {
  background: #edf1f2;
  color: #4c5d69;
}
.primary {
  background: #4fb8a8;
  color: #fff;
}
@media (max-width: 520px) {
  .update-panel {
    padding: 20px 16px;
  }
  .meta {
    grid-template-columns: 1fr;
  }
}
</style>
