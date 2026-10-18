<template>
  <main class="login-page">
    <section class="login-panel">
      <div class="brand">
        <span class="brand-mark"><b-icon icon="music-note" /></span>
        <div>
          <strong>{{ brandName }}</strong>
          <small>TeamSpeak 音乐控制台</small>
        </div>
      </div>

      <div class="heading">
        <p>{{ setupBot ? '连接机器人' : (initialized ? '欢迎回来' : '首次部署') }}</p>
        <h1>{{ setupBot ? '完成机器人连接' : (initialized ? '登录控制台' : '创建管理员账号') }}</h1>
        <span>{{ setupBot ? '填写 TeamSpeak 服务器信息，之后可在管理页修改。' : (initialized ? '使用网页账号登录，和 TeamSpeak UID 无关。' : '先创建网页管理员账号，再完成机器人连接。') }}</span>
      </div>

      <form @submit.prevent="submit">
        <template v-if="setupBot">
          <label>TeamSpeak 服务器地址<input v-model.trim="address" type="text" autocomplete="url" placeholder="ts.example.com:9987"></label>
          <label>机器人名称<input v-model.trim="nickname" type="text" autocomplete="off" placeholder="波点音乐"></label>
          <label>服务器密码 <small>可选</small><input v-model="serverPassword" type="password" autocomplete="new-password"></label>
        </template>
        <template v-else>
          <label>账号<input v-model.trim="username" type="text" autocomplete="username" maxlength="32"></label>
          <label>密码<input v-model="password" type="password" autocomplete="current-password"></label>
        </template>

        <p v-if="error" class="error">{{ error }}</p>

        <button class="submit" :disabled="loading">
          {{ loading ? '处理中…' : (setupBot ? '保存并连接' : (initialized ? '登录' : '创建并继续')) }}
        </button>
      </form>

      <template v-if="guestAvailable && !setupBot">
        <div class="divider"><span>或</span></div>
        <button type="button" class="guest-button" :disabled="loading" @click="enterAsGuest">
          <b-icon icon="account-outline" size="is-small" />以访客身份进入
        </button>
        <p class="guest-note">访客只能控制管理员指定的机器人。</p>
      </template>
    </section>
  </main>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleStatus } from "../ConsoleApi";

export default Vue.extend({
  data() {
    return {
      initialized: true,
      setupBot: false,
      guestAvailable: false,
      brandName: "波点音乐",
      username: "",
      password: "",
      address: "",
      nickname: "波点音乐",
      serverPassword: "",
      loading: false,
      error: "",
    };
  },
  async created() {
    try {
      const status = await consoleApi<ConsoleStatus>("status");
      this.initialized = status.initialized;
      this.guestAvailable = !!status.guestEnabled;
      this.brandName = status.brandName;
      document.title = status.brandName;
      if (status.initialized) {
        try {
          await consoleApi("me");
          this.$router.replace("/music");
        } catch (_) { /* stay on login */ }
      }
    } catch (_) {
      this.error = "无法连接到网页控制台。";
    }
  },
  methods: {
    async submit() {
      this.error = "";
      this.loading = true;
      try {
        if (this.setupBot) {
          await consoleApi("setup/bot", { address: this.address, nickname: this.nickname || "波点音乐", serverPassword: this.serverPassword });
          this.$router.replace("/music");
        } else {
          await consoleApi(this.initialized ? "login" : "setup", { username: this.username, password: this.password });
          if (this.initialized) this.$router.replace("/music");
          else this.setupBot = true;
        }
      } catch (e) {
        this.error = e instanceof Error ? e.message : "请求失败，请重试。";
      } finally {
        this.loading = false;
      }
    },
    async enterAsGuest() {
      this.error = "";
      this.loading = true;
      try {
        await consoleApi("guest/login", {});
        this.$router.replace("/music");
      } catch (e) {
        this.error = e instanceof Error ? e.message : "访客模式暂时不可用。";
        this.guestAvailable = false;
      } finally {
        this.loading = false;
      }
    },
  },
});
</script>

<style scoped lang="less">
.login-page {
  min-height: 100vh; min-height: 100dvh;
  display: grid; place-items: center; padding: 28px;
  background: var(--console-bg);
}
.login-panel {
  width: 100%; max-width: 420px; padding: 40px;
  border: 1px solid var(--console-line); border-radius: 20px;
  background: var(--console-surface); box-shadow: var(--console-shadow-md);
}
.brand { display: flex; align-items: center; gap: 12px; }
.brand-mark {
  width: 42px; height: 42px; display: grid; place-items: center;
  border-radius: 12px; color: #fff;
  background: linear-gradient(150deg, #ff4b5c, var(--console-brand) 55%, #c9152c);
  box-shadow: 0 2px 10px rgba(250, 35, 59, .35);
}
.brand strong, .brand small { display: block; }
.brand strong { color: var(--console-ink); font-size: 17px; letter-spacing: -.01em; }
.brand small { margin-top: 2px; color: var(--console-muted); font-size: 12px; }

.heading { margin-top: 42px; }
.heading p { margin: 0; color: var(--console-brand); font-size: 12.5px; font-weight: 700; }
.heading h1 { margin: 7px 0 8px; color: var(--console-ink); font-size: 28px; font-weight: 800; letter-spacing: -.028em; }
.heading span { color: var(--console-muted); font-size: 13.5px; line-height: 1.65; }

label { display: block; margin-top: 18px; color: var(--console-ink); font-size: 12.5px; font-weight: 600; }
label small { color: var(--console-muted); font-weight: 400; }
input {
  width: 100%; height: 46px; margin-top: 7px; padding: 0 14px;
  border: 1px solid var(--console-line-strong); border-radius: 10px;
  outline: 0; background: var(--console-surface-2); color: var(--console-ink);
  font-size: 14px;
}
input:focus { border-color: var(--console-brand); box-shadow: 0 0 0 3px var(--console-brand-soft); }

.submit {
  width: 100%; height: 48px; margin-top: 26px;
  border: 0; border-radius: 10px; background: var(--console-brand); color: #fff;
  font-size: 14px; font-weight: 700; cursor: pointer;
}
.submit:hover:not(:disabled) { background: var(--console-brand-dark); }
.submit:disabled { opacity: .68; cursor: wait; }

.error {
  margin: 16px 0 -6px; padding: 11px 13px; border-radius: 10px;
  background: var(--console-danger-soft); color: var(--console-danger); font-size: 12.5px;
}

.divider { position: relative; margin: 26px 0 18px; text-align: center; }
.divider::before { content: ""; position: absolute; inset: 50% 0 auto; height: 1px; background: var(--console-line); }
.divider span { position: relative; padding: 0 12px; background: var(--console-surface); color: var(--console-muted-2); font-size: 11.5px; }

.guest-button {
  width: 100%; height: 46px; display: flex; align-items: center; justify-content: center; gap: 7px;
  border: 1px solid var(--console-line-strong); border-radius: 10px;
  background: transparent; color: var(--console-ink);
  font-size: 13.5px; font-weight: 600; cursor: pointer;
}
.guest-button:hover:not(:disabled) { background: var(--console-hover); }
.guest-button:disabled { opacity: .6; cursor: wait; }
.guest-note { margin: 10px 0 0; color: var(--console-muted-2); font-size: 11.5px; text-align: center; }

@media (max-width: 520px) {
  .login-page { align-items: start; padding: 10vh 16px 20px; }
  .login-panel { padding: 28px 22px; border-radius: 16px; }
  .heading { margin-top: 32px; }
  .heading h1 { font-size: 25px; }
  input, .submit, .guest-button { height: 48px; }
}
</style>
