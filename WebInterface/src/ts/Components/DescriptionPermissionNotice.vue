<template>
  <div v-if="open" class="desc-mask" @click.self="ack">
    <section class="desc-panel" role="dialog" aria-modal="true" aria-labelledby="desc-title">
      <header>
        <p>权限提醒</p>
        <h2 id="desc-title">机器人无法修改简介</h2>
      </header>
      <p class="body">{{ message }}</p>
      <ul v-if="bots.length" class="bots">
        <li v-for="bot in bots" :key="bot.id || bot.name">
          <div class="bot-meta">
            <b>{{ bot.label || bot.name || bot.id }}</b>
            <small v-if="bot.server && !(bot.label && bot.label.indexOf(bot.server) >= 0)">服务器 {{ bot.server }}</small>
          </div>
          <span v-if="bot.connected === false">未连接</span>
          <span v-else-if="bot.canSetDescription === false">缺少简介权限</span>
        </li>
      </ul>
      <p class="hint">请在 TeamSpeak「权限 → 服务器组」中，给机器人所在组勾选：<br>「修改自己的简介」或「修改客户端简介」。</p>
      <footer>
        <button type="button" class="secondary" :disabled="busy" @click="dismiss">暂不理会</button>
        <button type="button" class="primary" :disabled="busy" @click="ack">我知道了</button>
      </footer>
    </section>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi } from "../ConsoleApi";

interface BotPerm {
  id?: string;
  name?: string;
  server?: string;
  label?: string;
  connected?: boolean;
  canSetDescription?: boolean | null;
}

export default Vue.extend({
  data() {
    return {
      open: false,
      busy: false,
      message: "",
      bots: [] as BotPerm[],
    };
  },
  methods: {
    async check(force = false) {
      try {
        const path = "bots/description-permission" + (force ? "?force=1" : "");
        const result = await consoleApi<{
          needsAttention?: boolean;
          dismissed?: boolean;
          message?: string;
          bots?: BotPerm[];
        }>(path);
        if (result.dismissed || !result.needsAttention) {
          this.open = false;
          return;
        }
        this.message = result.message || "机器人没有修改简介的权限，请在 TeamSpeak 中授予管理员/简介权限。";
        this.bots = (result.bots || []).filter((b) => b.canSetDescription === false);
        this.open = true;
      } catch (_) {
        // Silent: offline bots / probe failure should not block the console.
      }
    },
    ack() {
      // 我知道了：仅关闭本次；下次进入仍会检测。
      this.open = false;
    },
    async dismiss() {
      this.busy = true;
      try {
        await consoleApi("bots/description-permission/dismiss", {});
        this.open = false;
      } catch (_) {
        this.open = false;
      } finally {
        this.busy = false;
      }
    },
  },
});
</script>

<style scoped lang="less">
.desc-mask {
  position: fixed;
  z-index: 28;
  inset: 0;
  display: grid;
  place-items: center;
  padding: 20px;
  background: rgba(25, 37, 48, 0.38);
}
.desc-panel {
  width: 100%;
  max-width: 460px;
  padding: 24px;
  border-radius: 12px;
  background: #fff;
  box-shadow: 0 18px 54px rgba(20, 35, 45, 0.22);
}
header p {
  margin: 0;
  color: #b8860b;
  font-size: 13px;
  font-weight: 700;
}
header h2 {
  margin: 6px 0 0;
  font-size: 20px;
}
.body {
  margin: 16px 0 0;
  color: #4c5d69;
  font-size: 14px;
  line-height: 1.65;
}
.bots {
  margin: 14px 0 0;
  padding: 0;
  list-style: none;
}
.bots li {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: 8px;
  background: #fff8e6;
  color: #8a6500;
  font-size: 13px;
}
.bots li + li { margin-top: 6px; }
.bot-meta { min-width: 0; flex: 1; }
.bot-meta b, .bot-meta small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.bot-meta small { margin-top: 3px; color: #a07a20; font-size: 11px; }
.hint {
  margin: 14px 0 0;
  color: #778595;
  font-size: 12px;
  line-height: 1.6;
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
</style>
