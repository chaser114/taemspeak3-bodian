<template>
  <main class="admin">
    <header><p>控制台</p><h1>管理机器人与账号</h1></header>
    <p v-if="error" class="error">{{ error }}</p>

    <section class="update-card" :class="{ highlight: hasUpdate }">
      <div class="update-copy">
        <h2>程序更新</h2>
        <p>
          当前版本 <b>{{ displayVersion }}</b>
          <template v-if="hasUpdate"> · 发现新版本 <b class="newer">{{ latestVersion }}</b></template>
          <template v-else> · 已是最新或待检查</template>
        </p>
      </div>
      <button type="button" :class="{ glow: hasUpdate }" @click="updateOpen = true">
        {{ hasUpdate ? '立即更新' : '检查更新' }}
      </button>
    </section>
    <UpdatePanel :open="updateOpen" @close="updateOpen = false" @applied="onUpdateApplied"/>

    <section class="service-card">
      <div class="section-heading">
        <h2>服务与日志</h2>
        <span v-if="servicePid">PID {{ servicePid }}</span>
      </div>
      <div class="service-actions">
        <button type="button" class="secondary" :disabled="serviceBusy" @click="refreshLogs">刷新日志</button>
        <button type="button" class="secondary" :disabled="serviceBusy" @click="openServiceConfirm('restart')">重启服务</button>
        <button type="button" class="danger" :disabled="serviceBusy" @click="openServiceConfirm('stop')">停止服务</button>
      </div>
      <p v-if="serviceMessage" class="service-message">{{ serviceMessage }}</p>
      <pre class="log-view" ref="logView">{{ logText || '暂无日志。' }}</pre>
    </section>

    <div class="grid">
      <section class="brand-card">
        <h2>站点名称</h2>
        <form @submit.prevent="saveBrand">
          <label>显示名称<input v-model.trim="brandName"></label>
          <button>保存名称</button>
        </form>
      </section>

      <section class="bots-card">
        <div class="section-heading"><h2>音乐机器人</h2><span>{{ bots.length }} 个</span></div>
        <p v-if="!bots.length" class="empty">还没有创建机器人。</p>
        <article v-for="bot in bots" :key="bot.id">
          <div class="bot-info"><b>{{ bot.name }}</b><small>{{ bot.address }}</small></div>
          <span :class="['status', bot.status]">{{ statusText(bot.status) }}</span>
          <button class="text-button" @click="openEdit(bot)">编辑</button>
          <button class="text-button delete" @click="remove(bot)">删除</button>
        </article>
      </section>

      <section class="new-bot-card">
        <h2>新建机器人</h2>
        <p>填写连接信息后，机器人会自动保存并尝试连接。</p>
        <form @submit.prevent="createBot">
          <label>服务器地址<input v-model.trim="newAddress" placeholder="例如：ts.example.com:9987"></label>
          <label>机器人名称<input v-model.trim="newNickname" placeholder="波点音乐"></label>
          <label>服务器密码（可选）<input v-model="newPassword" type="password"></label>
          <button>创建并连接</button>
        </form>
      </section>

      <section class="accounts-card">
        <div class="section-heading">
          <h2>网页账号</h2>
          <button type="button" class="text-button" @click="openOwnPassword">修改我的密码</button>
        </div>
        <p class="empty">密码至少 8 位。创建成功后会显示在下方列表。</p>
        <p v-if="accountError" class="inline-error">{{ accountError }}</p>
        <p v-if="accountSuccess" class="inline-success">{{ accountSuccess }}</p>
        <form class="create-account" @submit.prevent="createAccount">
          <label>账号<input v-model.trim="username" autocomplete="off" maxlength="32" placeholder="1-32 个字符"></label>
          <label>密码<input v-model="userPassword" type="password" autocomplete="new-password" placeholder="至少 8 位"></label>
          <label>角色
            <select v-model="role">
              <option value="user">普通用户</option>
              <option value="admin">管理员</option>
            </select>
          </label>
          <button type="submit" :disabled="accountBusy">{{ accountBusy ? '创建中…' : '创建账号' }}</button>
        </form>
        <article v-for="account in accounts" :key="account.username">
          <div>
            <b>{{ account.username }}</b>
            <small>{{ account.role === 'admin' ? '管理员' : '普通用户' }}{{ account.enabled ? '' : ' · 已停用' }}</small>
          </div>
          <button type="button" class="text-button" @click="openResetPassword(account)">改密</button>
          <button type="button" class="text-button" @click="toggle(account)">{{ account.enabled ? '已启用' : '已停用' }}</button>
        </article>
      </section>
    </div>

    <div v-if="editing" class="modal-mask" @click.self="closeEdit">
      <section class="edit-modal" role="dialog" aria-modal="true" aria-labelledby="edit-title">
        <div class="modal-heading"><h2 id="edit-title">编辑机器人</h2><button type="button" class="close" title="关闭" @click="closeEdit">×</button></div>
        <p>{{ editing.name }}</p>
        <form @submit.prevent="saveEdit">
          <label>服务器地址<input v-model.trim="editAddress"></label>
          <label>机器人名称<input v-model.trim="editNickname"></label>
          <label>服务器密码（留空则不设密码）<input v-model="editPassword" type="password"></label>
          <div class="modal-actions"><button type="button" class="secondary" @click="closeEdit">取消</button><button type="submit">保存并重新连接</button></div>
        </form>
      </section>
    </div>

    <div v-if="passwordModal" class="modal-mask" @click.self="closePasswordModal">
      <section class="edit-modal" role="dialog" aria-modal="true" aria-labelledby="pwd-title">
        <div class="modal-heading">
          <h2 id="pwd-title">{{ passwordModal.mode === 'own' ? '修改我的密码' : ('修改密码 · ' + passwordModal.username) }}</h2>
          <button type="button" class="close" title="关闭" @click="closePasswordModal">×</button>
        </div>
        <form @submit.prevent="submitPassword">
          <label v-if="passwordModal.mode === 'own'">当前密码<input v-model="passwordModal.currentPassword" type="password" autocomplete="current-password"></label>
          <label>新密码<input v-model="passwordModal.newPassword" type="password" autocomplete="new-password" placeholder="至少 8 位"></label>
          <label>确认新密码<input v-model="passwordModal.confirmPassword" type="password" autocomplete="new-password"></label>
          <p v-if="passwordModal.error" class="inline-error">{{ passwordModal.error }}</p>
          <div class="modal-actions">
            <button type="button" class="secondary" @click="closePasswordModal">取消</button>
            <button type="submit" :disabled="accountBusy">{{ accountBusy ? '提交中…' : '保存密码' }}</button>
          </div>
        </form>
      </section>
    </div>

    <div v-if="serviceConfirm" class="modal-mask" @click.self="closeServiceConfirm">
      <section class="edit-modal" role="dialog" aria-modal="true" aria-labelledby="svc-title">
        <div class="modal-heading">
          <h2 id="svc-title">{{ serviceConfirm.action === 'restart' ? '重启服务' : '停止服务' }}</h2>
          <button type="button" class="close" title="关闭" @click="closeServiceConfirm">×</button>
        </div>
        <p class="confirm-hint">
          <template v-if="serviceConfirm.action === 'restart'">确认后服务会自动重启，约 10 秒后刷新网页即可。</template>
          <template v-else>停止后网页将无法访问，需要在服务器上重新运行启动脚本。</template>
        </p>
        <form @submit.prevent="submitServiceConfirm">
          <label>
            管理员密码（二次确认）
            <input
              ref="servicePasswordInput"
              v-model="serviceConfirm.password"
              type="password"
              autocomplete="current-password"
              :disabled="serviceBusy"
              placeholder="输入当前管理员密码"
            >
          </label>
          <p v-if="serviceConfirm.error" class="inline-error">{{ serviceConfirm.error }}</p>
          <div class="modal-actions">
            <button type="button" class="secondary" :disabled="serviceBusy" @click="closeServiceConfirm">取消</button>
            <button
              type="submit"
              :class="serviceConfirm.action === 'stop' ? 'danger-solid' : ''"
              :disabled="serviceBusy || !serviceConfirm.password"
            >{{ serviceBusy ? '处理中…' : (serviceConfirm.action === 'restart' ? '确认重启' : '确认停止') }}</button>
          </div>
        </form>
      </section>
    </div>
  </main>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser } from "../ConsoleApi";
import UpdatePanel from "../Components/UpdatePanel.vue";

interface Bot { id: string; name: string; address: string; status: string; }
interface Account { username: string; role: string; enabled: boolean; }

export default Vue.extend({
  components: { UpdatePanel },
  data() {
    return {
      brandName: "波点音乐", bots: [] as Bot[], accounts: [] as Account[], error: "",
      newAddress: "", newNickname: "波点音乐", newPassword: "",
      username: "", userPassword: "", role: "user",
      accountBusy: false, accountError: "", accountSuccess: "",
      meUsername: "",
      passwordModal: null as null | {
        mode: "own" | "admin";
        username: string;
        currentPassword: string;
        newPassword: string;
        confirmPassword: string;
        error: string;
      },
      editing: null as Bot | null, editAddress: "", editNickname: "", editPassword: "",
      updateOpen: false, currentVersion: "", latestVersion: "", hasUpdate: false,
      servicePid: 0 as number | string, serviceBusy: false, serviceMessage: "",
      serviceConfirm: null as null | { action: "restart" | "stop"; password: string; error: string },
      logText: "", logTimer: 0 as any,
    };
  },
  computed: {
    displayVersion(): string {
      const v = this.currentVersion || "unknown";
      return v.startsWith("v") || v.startsWith("build") ? v : ("v" + v);
    },
  },
  async created() {
    const user = await consoleApi<ConsoleUser>("me");
    if (user.role !== "admin") { this.$router.replace("/music"); return; }
    this.brandName = user.brandName;
    this.meUsername = user.username;
    await this.reload();
    await this.refreshUpdate();
    await this.refreshService();
    await this.refreshLogs();
    this.logTimer = setInterval(() => this.refreshLogs(true), 5000);
  },
  beforeDestroy() {
    if (this.logTimer) clearInterval(this.logTimer);
  },
  methods: {
    async reload() {
      this.bots = (await consoleApi<{ bots: Bot[] }>("bots")).bots;
      this.accounts = (await consoleApi<{ accounts: Account[] }>("accounts")).accounts;
    },
    async refreshUpdate() {
      try {
        const status = await consoleApi<{ currentVersion?: string }>("update/status");
        this.currentVersion = status.currentVersion || "";
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string; latestVersion?: string }>("update/check", { source: "gitcode" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.latestVersion = check.latestVersion || "";
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) { /* ignore */ }
    },
    onUpdateApplied() {
      this.hasUpdate = false;
      this.refreshUpdate();
    },
    async refreshService() {
      try {
        const status = await consoleApi<{ pid?: number }>("service/status");
        this.servicePid = status.pid || "";
      } catch (_) { /* ignore */ }
    },
    async refreshLogs(silent = false) {
      try {
        const logs = await consoleApi<{ text?: string }>("service/logs?lines=250");
        this.logText = logs.text || "";
        this.$nextTick(() => {
          const el = this.$refs.logView as HTMLElement | undefined;
          if (el) el.scrollTop = el.scrollHeight;
        });
      } catch (error) {
        if (!silent) this.error = error instanceof Error ? error.message : "读取日志失败。";
      }
    },
    openServiceConfirm(action: "restart" | "stop") {
      this.serviceMessage = "";
      this.error = "";
      this.serviceConfirm = { action, password: "", error: "" };
      this.$nextTick(() => {
        const input = this.$refs.servicePasswordInput as HTMLInputElement | undefined;
        if (input) input.focus();
      });
    },
    closeServiceConfirm() {
      if (this.serviceBusy) return;
      this.serviceConfirm = null;
    },
    async submitServiceConfirm() {
      if (!this.serviceConfirm || !this.serviceConfirm.password || this.serviceBusy) return;
      const action = this.serviceConfirm.action;
      const password = this.serviceConfirm.password;
      this.serviceBusy = true;
      this.serviceConfirm.error = "";
      this.serviceMessage = "";
      try {
        const path = action === "restart" ? "service/restart" : "service/stop";
        const result = await consoleApi<{ message?: string }>(path, { password });
        this.serviceMessage = result.message || (action === "restart" ? "服务即将重启，请稍候刷新。" : "服务即将停止。");
        this.serviceConfirm = null;
        if (action === "restart") setTimeout(() => { window.location.reload(); }, 12000);
      } catch (error) {
        const message = error instanceof Error ? error.message : (action === "restart" ? "重启失败。" : "停止失败。");
        if (this.serviceConfirm) this.serviceConfirm.error = message;
        this.error = message;
      } finally {
        this.serviceBusy = false;
      }
    },
    statusText(status: string) { return status === "connected" ? "已连接" : status === "connecting" ? "连接中" : "离线"; },
    async run(action: () => Promise<any>) {
      this.error = "";
      try { await action(); }
      catch (error) { this.error = error instanceof Error ? error.message : "操作失败。"; }
    },
    saveBrand() { return this.run(() => consoleApi("settings/brand", { brandName: this.brandName })); },
    createBot() {
      return this.run(async () => {
        await consoleApi("setup/bot", { address: this.newAddress, nickname: this.newNickname, serverPassword: this.newPassword });
        this.newAddress = ""; this.newNickname = "波点音乐"; this.newPassword = "";
        await this.reload();
      });
    },
    openEdit(bot: Bot) {
      this.editing = bot;
      this.editAddress = bot.address;
      this.editNickname = bot.name;
      this.editPassword = "";
    },
    closeEdit() { this.editing = null; this.editPassword = ""; },
    saveEdit() {
      if (!this.editing) return;
      return this.run(async () => {
        await consoleApi("setup/bot", { id: this.editing!.id, address: this.editAddress, nickname: this.editNickname, serverPassword: this.editPassword });
        this.closeEdit();
        await this.reload();
      });
    },
    remove(bot: Bot) { return this.run(async () => { await consoleApi("bots/delete", { id: bot.id }); await this.reload(); }); },
    async createAccount() {
      this.accountError = "";
      this.accountSuccess = "";
      this.error = "";
      const name = (this.username || "").trim();
      const password = this.userPassword || "";
      if (!name) { this.accountError = "请填写账号。"; return; }
      if (password.length < 8) { this.accountError = "密码至少需要 8 个字符。"; return; }
      this.accountBusy = true;
      try {
        await consoleApi("accounts", { username: name, password, role: this.role });
        this.accountSuccess = "账号「" + name + "」已创建。";
        this.username = "";
        this.userPassword = "";
        this.role = "user";
        await this.reload();
      } catch (error) {
        this.accountError = error instanceof Error ? error.message : "创建账号失败。";
        this.error = this.accountError;
      } finally {
        this.accountBusy = false;
      }
    },
    openOwnPassword() {
      this.passwordModal = {
        mode: "own",
        username: this.meUsername,
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
        error: "",
      };
    },
    openResetPassword(account: Account) {
      this.passwordModal = {
        mode: "admin",
        username: account.username,
        currentPassword: "",
        newPassword: "",
        confirmPassword: "",
        error: "",
      };
    },
    closePasswordModal() { this.passwordModal = null; },
    async submitPassword() {
      if (!this.passwordModal) return;
      const modal = this.passwordModal;
      modal.error = "";
      if (!modal.newPassword || modal.newPassword.length < 8) {
        modal.error = "新密码至少需要 8 个字符。";
        return;
      }
      if (modal.newPassword !== modal.confirmPassword) {
        modal.error = "两次输入的新密码不一致。";
        return;
      }
      this.accountBusy = true;
      try {
        if (modal.mode === "own") {
          await consoleApi("accounts/password", {
            currentPassword: modal.currentPassword,
            newPassword: modal.newPassword,
          });
          this.accountSuccess = "密码已修改，请重新登录。";
          this.closePasswordModal();
          setTimeout(async () => {
            try { await consoleApi("logout", {}); } catch (_) { /* ignore */ }
            this.$router.replace("/");
          }, 800);
        } else {
          await consoleApi("accounts/password", {
            username: modal.username,
            newPassword: modal.newPassword,
          });
          this.accountSuccess = "已修改账号「" + modal.username + "」的密码。";
          this.closePasswordModal();
        }
      } catch (error) {
        modal.error = error instanceof Error ? error.message : "修改密码失败。";
      } finally {
        this.accountBusy = false;
      }
    },
    toggle(account: Account) { return this.run(async () => { await consoleApi("accounts/enabled", { username: account.username, enabled: !account.enabled }); await this.reload(); }); },
  },
});
</script>

<style scoped lang="less">
.admin { max-width: 1120px; margin: auto; padding: 36px 28px 48px; }
.admin > header p { margin: 0 0 6px; color: var(--console-brand-dark); font-weight: 700; font-size: 13px; }
.admin h1 { margin: 0; font-size: 28px; letter-spacing: -0.02em; }
.update-card {
  display: flex; align-items: center; justify-content: space-between; gap: 16px;
  margin-top: 20px; padding: 18px 20px; border: 1px solid var(--console-line);
  border-radius: var(--console-radius); background: var(--console-surface); box-shadow: var(--console-shadow);
}
.update-card.highlight { border-color: #f0d48a; background: var(--console-warn-soft); }
.update-copy h2 { margin: 0; font-size: 17px; }
.update-copy p { margin: 6px 0 0; color: var(--console-muted); font-size: 13px; }
.update-copy b { color: var(--console-ink); }
.update-copy b.newer { color: var(--console-warn); }
.update-card button {
  height: 42px; padding: 0 16px; border: 0; border-radius: var(--console-radius-sm);
  background: var(--console-brand); color: #fff; cursor: pointer; font: inherit; font-weight: 600; white-space: nowrap;
}
.update-card button.glow { background: #d4a017; box-shadow: 0 6px 16px rgba(212, 160, 23, 0.24); }
.service-card {
  margin-top: 16px; padding: 18px 20px; border: 1px solid var(--console-line);
  border-radius: var(--console-radius); background: var(--console-surface); box-shadow: var(--console-shadow);
}
.service-actions { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 12px; }
.service-actions button {
  height: 40px; padding: 0 14px; border: 0; border-radius: var(--console-radius-sm); font: inherit; cursor: pointer;
}
.service-actions button:disabled { opacity: .6; cursor: wait; }
.service-actions .secondary { background: #edf1f2; color: #4c5d69; }
.service-actions .danger { background: var(--console-danger-soft); color: var(--console-danger); }
.service-message { margin: 12px 0 0; color: #197565; font-size: 13px; }
.log-view {
  margin: 12px 0 0; max-height: 220px; overflow: auto; padding: 12px 14px;
  border-radius: var(--console-radius-sm); background: #101820; color: #d7e2ea; font-size: 12px; line-height: 1.5;
  white-space: pre-wrap; word-break: break-word;
}
.grid {
  display: grid; grid-template-columns: minmax(300px, .9fr) minmax(0, 1.8fr);
  grid-template-rows: auto auto auto; gap: 16px; margin-top: 16px; align-items: start;
}
.grid section {
  min-width: 0; padding: 20px; border: 1px solid var(--console-line);
  border-radius: var(--console-radius); background: var(--console-surface); box-shadow: var(--console-shadow);
}
.brand-card { grid-column: 1; grid-row: 1; }
.new-bot-card { grid-column: 1; grid-row: 2; }
.bots-card { grid-column: 2; grid-row: 1 / span 2; }
.accounts-card { grid-column: 1 / span 2; grid-row: 3; }
.section-heading, .modal-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.section-heading h2, .grid h2, .edit-modal h2 { margin: 0; font-size: 17px; }
.section-heading span { color: var(--console-muted); font-size: 13px; }
.new-bot-card > p, .edit-modal > p, .empty { color: var(--console-muted); font-size: 13px; line-height: 1.55; }
label { display: block; margin-top: 12px; color: #44515e; font-size: 13px; font-weight: 600; }
input, select {
  width: 100%; height: 44px; margin-top: 6px; padding: 0 12px; border: 1px solid #d5e0e3;
  border-radius: var(--console-radius-sm); background: #fff; color: var(--console-ink); font: inherit;
}
input:focus, select:focus {
  outline: 0; border-color: var(--console-brand); box-shadow: 0 0 0 3px rgba(79, 184, 168, 0.14);
}
form > button { margin-top: 16px; }
button {
  height: 42px; padding: 0 14px; border: 0; border-radius: var(--console-radius-sm);
  background: var(--console-brand); color: #fff; cursor: pointer; font: inherit; font-weight: 600;
}
article { display: flex; align-items: center; gap: 10px; padding: 14px 0; border-top: 1px solid #edf0f1; }
.bots-card article:first-of-type, .accounts-card article:first-of-type { margin-top: 8px; }
article div { flex: 1; min-width: 0; }
article b, article small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
article small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.status {
  padding: 4px 8px; border-radius: 999px; background: #f1f3f5; color: #788595; font-size: 12px; white-space: nowrap;
}
.status.connected { background: var(--console-brand-soft); color: #197565; }
.status.connecting { background: var(--console-warn-soft); color: #9b6b10; }
.text-button {
  min-width: 52px; height: 36px; flex: 0 0 auto; white-space: nowrap;
  background: var(--console-brand-soft); color: var(--console-brand-dark); font-weight: 600;
}
.text-button.delete { background: var(--console-danger-soft); color: var(--console-danger); }
.create-account {
  display: grid; grid-template-columns: 1fr 1fr 140px auto; align-items: end; gap: 10px; margin-top: 12px;
}
.create-account label { margin-top: 0; }
.create-account button { margin-top: 0; min-width: 104px; height: 44px; }
.create-account button:disabled { opacity: .65; cursor: wait; }
.inline-error { margin: 10px 0 0; color: var(--console-danger); font-size: 13px; }
.inline-success { margin: 10px 0 0; color: #197565; font-size: 13px; }
.error { margin-top: 16px; color: var(--console-danger); }
.modal-mask {
  position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 20px;
  background: rgba(25, 37, 48, 0.4);
}
.edit-modal {
  width: 100%; max-width: 480px; padding: 24px; border-radius: var(--console-radius-lg);
  background: #fff; box-shadow: var(--console-shadow-md);
}
.close {
  width: 36px; height: 36px; padding: 0; border-radius: 50%; background: #edf1f2; color: #53616e;
  font-size: 22px; line-height: 1; font-weight: 400;
}
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }
.modal-actions button { margin: 0; min-width: 88px; }
.secondary { background: #edf1f2; color: #4c5d69; }
.confirm-hint { margin: 0 0 12px; color: var(--console-muted); font-size: 13px; line-height: 1.55; }
.danger-solid { background: var(--console-danger); color: #fff; }

@media (max-width: 1000px) {
  .grid { grid-template-columns: 1fr; grid-template-rows: none; }
  .brand-card, .new-bot-card, .bots-card, .accounts-card { grid-column: auto; grid-row: auto; }
  .create-account { grid-template-columns: 1fr; }
  .create-account button { width: 100%; }
  .admin { padding: 24px 16px 40px; }
}
@media (max-width: 600px) {
  .admin h1 { font-size: 24px; }
  .update-card, .service-card, .grid section { padding: 16px; }
  .update-card { align-items: stretch; flex-direction: column; }
  .update-card button, .service-actions button { width: 100%; height: 44px; }
  .log-view { max-height: 160px; }
  .bots-card article, .accounts-card article { align-items: flex-start; flex-wrap: wrap; }
  .status { margin-left: auto; }
  .section-heading { flex-wrap: wrap; }
  .section-heading .text-button { width: 100%; }
}
</style>
