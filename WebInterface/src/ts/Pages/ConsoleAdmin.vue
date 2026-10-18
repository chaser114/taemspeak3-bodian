<template>
  <div class="bd-admin">
    <header class="bd-admin-head">
      <h1>管理</h1>
      <p>机器人、网页账号、访客权限与程序更新。</p>
    </header>

    <p v-if="error" class="bd-inline-error">{{ error }}</p>

    <!-- ============ 站点 ============ -->
    <section class="bd-card">
      <h2>站点</h2>
      <div class="bd-field">
        <div class="bd-label"><b>站点名称</b><span>显示在登录页与侧栏</span></div>
        <div class="bd-control">
          <input v-model.trim="brandName" class="bd-input" maxlength="32" placeholder="波点音乐">
          <button type="button" class="bd-pill solid" @click="saveBrand">保存</button>
        </div>
      </div>
      <div class="bd-field">
        <div class="bd-label"><b>当前版本</b><span>{{ displayVersion }}</span></div>
        <div class="bd-control">
          <span v-if="hasUpdate" class="bd-tag warn">有新版本 {{ latestVersion }}</span>
          <span v-else class="bd-tag">已是最新</span>
          <button type="button" class="bd-pill" @click="updateOpen = true">{{ hasUpdate ? '立即更新' : '检查更新' }}</button>
        </div>
      </div>
    </section>
    <UpdatePanel :open="updateOpen" @close="updateOpen = false" @applied="onUpdateApplied"/>

    <!-- ============ 机器人 ============ -->
    <section class="bd-card">
      <div class="bd-card-head"><h2>TeamSpeak 机器人</h2><span>{{ bots.length }} 个</span></div>
      <p v-if="!bots.length" class="bd-muted">还没有创建机器人。</p>
      <div v-for="bot in bots" :key="bot.id" class="bd-row-item">
        <span class="bd-row-icon"><b-icon icon="robot" /></span>
        <span class="bd-row-copy">
          <b>{{ bot.name }}</b>
          <span>{{ bot.address || '未填写地址' }}</span>
        </span>
        <span class="bd-tag" :class="bot.status">{{ statusText(bot.status) }}</span>
        <button type="button" class="bd-pill" @click="openEdit(bot)">编辑</button>
        <button type="button" class="bd-pill danger" @click="remove(bot)">删除</button>
      </div>
      <div class="bd-field">
        <div class="bd-label"><b>新建机器人</b><span>填写地址后自动保存并连接</span></div>
        <div class="bd-control wrap">
          <input v-model.trim="newAddress" class="bd-input" placeholder="ts.example.com:9987">
          <input v-model.trim="newNickname" class="bd-input narrow" placeholder="昵称">
          <button type="button" class="bd-pill solid" :disabled="busy" @click="createBot"><b-icon icon="plus" size="is-small" />添加</button>
        </div>
      </div>
    </section>

    <!-- ============ 网页账号 ============ -->
    <section class="bd-card">
      <div class="bd-card-head"><h2>网页账号</h2><span>{{ accounts.length }} 个</span></div>
      <p class="bd-muted">账号与 TeamSpeak UID 无关。可为每个账号指定能控制的机器人，留空表示可控制全部。</p>

      <div v-for="account in accounts" :key="account.username" class="bd-account-item">
        <span class="bd-avatar">{{ account.username.slice(0, 1).toUpperCase() }}</span>
        <span class="bd-row-copy">
          <b>{{ account.username }}</b>
          <span>{{ account.role === 'admin' ? '管理员' : '普通用户' }}{{ account.enabled ? '' : ' · 已停用' }}</span>
        </span>
        <span class="bd-tag" :class="{ ok: account.enabled }">{{ account.enabled ? '已启用' : '已停用' }}</span>
        <button type="button" class="bd-pill" @click="openBots(account)">
          {{ botScopeText(account) }}
        </button>
        <button type="button" class="bd-pill" @click="account.username === me ? openOwnPassword() : openResetPassword(account)">改密</button>
        <button type="button" class="bd-pill" :class="{ danger: account.enabled }" @click="toggle(account)">{{ account.enabled ? '停用' : '启用' }}</button>
      </div>

      <div class="bd-field">
        <div class="bd-label"><b>新建账号</b><span>密码至少 8 位</span></div>
        <div class="bd-control wrap">
          <input v-model.trim="username" class="bd-input narrow" maxlength="32" placeholder="账号">
          <input v-model="userPassword" class="bd-input narrow" type="password" autocomplete="new-password" placeholder="密码">
          <select v-model="role" class="bd-input narrow">
            <option value="user">普通用户</option>
            <option value="admin">管理员</option>
          </select>
          <button type="button" class="bd-pill solid" :disabled="accountBusy" @click="createAccount">创建</button>
        </div>
      </div>
      <p v-if="accountError" class="bd-inline-error">{{ accountError }}</p>
      <p v-if="accountSuccess" class="bd-inline-success">{{ accountSuccess }}</p>
    </section>

    <!-- ============ 访客模式 ============ -->
    <section class="bd-card">
      <div class="bd-card-head">
        <h2>访客模式</h2>
        <span>无需登录即可控制指定机器人</span>
      </div>
      <div class="bd-field">
        <div class="bd-label"><b>允许访客访问</b><span>关闭后访客入口立即失效</span></div>
        <div class="bd-control">
          <button type="button" class="bd-switch" :class="{ on: guest.enabled }" role="switch" :aria-checked="guest.enabled" @click="guest.enabled = !guest.enabled" />
        </div>
      </div>
      <div class="bd-field align-top">
        <div class="bd-label"><b>访客可控制的机器人</b><span>至少选择一个才能开启</span></div>
        <div class="bd-control wrap left">
          <label v-for="bot in bots" :key="bot.id" class="bd-check">
            <input type="checkbox" :value="bot.id" v-model="guest.bots">
            <span>{{ bot.name }}</span>
          </label>
          <span v-if="!bots.length" class="bd-muted">请先创建机器人。</span>
        </div>
      </div>
      <div class="bd-field">
        <div class="bd-label"></div>
        <div class="bd-control">
          <button type="button" class="bd-pill" :disabled="busy" @click="openGuestPassword">保存访客设置</button>
        </div>
      </div>
      <p class="bd-muted note">访客只能播放、加入队列和切歌，不能清空队列、查看日志或修改任何设置。</p>
    </section>

    <!-- ============ 服务 ============ -->
    <section class="bd-card">
      <div class="bd-card-head"><h2>服务与日志</h2><span v-if="servicePid">PID {{ servicePid }}</span></div>
      <div class="bd-field">
        <div class="bd-label"><b>后台服务</b><span>重启或停止需要管理员密码</span></div>
        <div class="bd-control">
          <button type="button" class="bd-pill" :disabled="serviceBusy" @click="refreshLogs">刷新日志</button>
          <button type="button" class="bd-pill" :disabled="serviceBusy" @click="openServiceConfirm('restart')">重启</button>
          <button type="button" class="bd-pill danger" :disabled="serviceBusy" @click="openServiceConfirm('stop')">停止</button>
        </div>
      </div>
      <p v-if="serviceMessage" class="bd-inline-success">{{ serviceMessage }}</p>
      <pre class="bd-log" ref="logView">{{ logText || '暂无日志。' }}</pre>
    </section>

    <!-- ============ modals ============ -->
    <div v-if="editing" class="bd-modal-mask" @click.self="closeEdit">
      <section class="bd-modal" role="dialog" aria-modal="true">
        <div class="bd-modal-head"><h2>编辑机器人</h2><button type="button" class="bd-icon-button" @click="closeEdit"><b-icon icon="close" /></button></div>
        <p class="bd-muted">{{ editing.name }}</p>
        <label class="bd-modal-label">服务器地址<input v-model.trim="editAddress" class="bd-input wide"></label>
        <label class="bd-modal-label">机器人名称<input v-model.trim="editNickname" class="bd-input wide"></label>
        <label class="bd-modal-label">服务器密码（留空表示不修改）<input v-model="editPassword" class="bd-input wide" type="password"></label>
        <div class="bd-modal-actions">
          <button type="button" class="bd-pill" @click="closeEdit">取消</button>
          <button type="button" class="bd-pill solid" @click="saveEdit">保存并重连</button>
        </div>
      </section>
    </div>

    <div v-if="botsModal" class="bd-modal-mask" @click.self="botsModal = null">
      <section class="bd-modal" role="dialog" aria-modal="true">
        <div class="bd-modal-head"><h2>机器人权限 · {{ botsModal.username }}</h2><button type="button" class="bd-icon-button" @click="botsModal = null"><b-icon icon="close" /></button></div>
        <p class="bd-muted">勾选该账号可以控制的机器人。全部不勾选表示可控制全部机器人。</p>
        <div class="bd-check-list">
          <label v-for="bot in bots" :key="bot.id" class="bd-check">
            <input type="checkbox" :value="bot.id" v-model="botsModal.bots">
            <span>{{ bot.name }}</span>
          </label>
          <span v-if="!bots.length" class="bd-muted">还没有机器人。</span>
        </div>
        <div class="bd-modal-actions">
          <button type="button" class="bd-pill" @click="botsModal = null">取消</button>
          <button type="button" class="bd-pill solid" :disabled="busy" @click="saveAccountBots">保存</button>
        </div>
      </section>
    </div>

    <div v-if="passwordModal" class="bd-modal-mask" @click.self="closePasswordModal">
      <section class="bd-modal" role="dialog" aria-modal="true">
        <div class="bd-modal-head">
          <h2>{{ passwordModal.mode === 'own' ? '修改我的密码' : ('修改密码 · ' + passwordModal.username) }}</h2>
          <button type="button" class="bd-icon-button" @click="closePasswordModal"><b-icon icon="close" /></button>
        </div>
        <label v-if="passwordModal.mode === 'own'" class="bd-modal-label">当前密码<input v-model="passwordModal.currentPassword" class="bd-input wide" type="password" autocomplete="current-password"></label>
        <label class="bd-modal-label">新密码<input v-model="passwordModal.newPassword" class="bd-input wide" type="password" autocomplete="new-password" placeholder="至少 8 位"></label>
        <label class="bd-modal-label">确认新密码<input v-model="passwordModal.confirmPassword" class="bd-input wide" type="password" autocomplete="new-password"></label>
        <p v-if="passwordModal.error" class="bd-inline-error">{{ passwordModal.error }}</p>
        <div class="bd-modal-actions">
          <button type="button" class="bd-pill" @click="closePasswordModal">取消</button>
          <button type="button" class="bd-pill solid" :disabled="accountBusy" @click="submitPassword">保存密码</button>
        </div>
      </section>
    </div>

    <div v-if="confirming" class="bd-modal-mask" @click.self="closeConfirm">
      <section class="bd-modal" role="dialog" aria-modal="true">
        <div class="bd-modal-head">
          <h2>{{ confirming.kind === 'service' ? (confirming.action === 'restart' ? '重启服务' : '停止服务') : '保存访客设置' }}</h2>
          <button type="button" class="bd-icon-button" @click="closeConfirm"><b-icon icon="close" /></button>
        </div>
        <p class="bd-muted">
          <template v-if="confirming.kind === 'service' && confirming.action === 'restart'">确认后服务会自动重启，约 10 秒后刷新网页即可。</template>
          <template v-else-if="confirming.kind === 'service'">停止后网页将无法访问，需要在服务器上重新运行启动脚本。</template>
          <template v-else>请输入管理员密码以确认修改访客权限。</template>
        </p>
        <label class="bd-modal-label">管理员密码
          <input ref="confirmInput" v-model="confirming.password" class="bd-input wide" type="password" autocomplete="current-password" :disabled="serviceBusy" placeholder="输入当前管理员密码" @keydown.enter.prevent="submitConfirm">
        </label>
        <p v-if="confirming.error" class="bd-inline-error">{{ confirming.error }}</p>
        <div class="bd-modal-actions">
          <button type="button" class="bd-pill" :disabled="serviceBusy" @click="closeConfirm">取消</button>
          <button type="button" class="bd-pill solid" :class="{ danger: confirming.kind === 'service' && confirming.action === 'stop' }" :disabled="serviceBusy || !confirming.password" @click="submitConfirm">
            {{ serviceBusy ? '处理中…' : '确认' }}
          </button>
        </div>
      </section>
    </div>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser, ConsoleAccount, ConsoleBot, GuestSettings } from "../ConsoleApi";
import UpdatePanel from "../Components/UpdatePanel.vue";

interface ConfirmState {
  kind: "service" | "guest";
  action: "restart" | "stop" | "save";
  password: string;
  error: string;
}

export default Vue.extend({
  components: { UpdatePanel },
  data() {
    return {
      brandName: "波点音乐",
      bots: [] as ConsoleBot[],
      accounts: [] as ConsoleAccount[],
      guest: { enabled: false, bots: [] } as GuestSettings,
      error: "",
      busy: false,
      newAddress: "",
      newNickname: "",
      username: "",
      userPassword: "",
      role: "user",
      accountBusy: false,
      accountError: "",
      accountSuccess: "",
      me: "",
      updateOpen: false,
      currentVersion: "",
      latestVersion: "",
      hasUpdate: false,
      servicePid: 0 as number | string,
      serviceBusy: false,
      serviceMessage: "",
      logText: "",
      logTimer: 0 as any,
      editing: null as ConsoleBot | null,
      editAddress: "",
      editNickname: "",
      editPassword: "",
      botsModal: null as null | { username: string; bots: string[] },
      passwordModal: null as null | { mode: "own" | "admin"; username: string; currentPassword: string; newPassword: string; confirmPassword: string; error: string },
      confirming: null as ConfirmState | null,
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
    this.me = user.username;
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
    statusText(status: string) { return status === "connected" ? "已连接" : status === "connecting" ? "连接中" : "离线"; },
    botScopeText(account: ConsoleAccount): string {
      if (account.role === "admin") return "全部机器人";
      if (!account.allowedBots || !account.allowedBots.length) return "全部机器人";
      return account.allowedBots.length + " 个机器人";
    },
    async reload() {
      try {
        this.bots = (await consoleApi<{ bots: ConsoleBot[] }>("bots")).bots;
        this.accounts = (await consoleApi<{ accounts: ConsoleAccount[] }>("accounts")).accounts;
        const guest = await consoleApi<GuestSettings>("guest/settings");
        this.guest = { enabled: !!guest.enabled, bots: guest.bots || [] };
      } catch (error) {
        this.error = error instanceof Error ? error.message : "读取管理数据失败。";
      }
    },
    async run(action: () => Promise<any>) {
      this.error = "";
      try { await action(); }
      catch (error) { this.error = error instanceof Error ? error.message : "操作失败。"; }
    },
    saveBrand() {
      return this.run(async () => {
        await consoleApi("settings/brand", { brandName: this.brandName });
        await this.reload();
      });
    },
    createBot() {
      if (this.busy) return;
      this.busy = true;
      return this.run(async () => {
        await consoleApi("setup/bot", { address: this.newAddress, nickname: this.newNickname || "波点音乐", serverPassword: "" });
        this.newAddress = "";
        this.newNickname = "";
        await this.reload();
      }).finally(() => { this.busy = false; });
    },
    openEdit(bot: ConsoleBot) { this.editing = bot; this.editAddress = bot.address || ""; this.editNickname = bot.name; this.editPassword = ""; },
    closeEdit() { this.editing = null; this.editPassword = ""; },
    saveEdit() {
      if (!this.editing) return;
      return this.run(async () => {
        await consoleApi("setup/bot", { id: this.editing!.id, address: this.editAddress, nickname: this.editNickname, serverPassword: this.editPassword });
        this.closeEdit();
        await this.reload();
      });
    },
    remove(bot: ConsoleBot) {
      return this.run(async () => { await consoleApi("bots/delete", { id: bot.id }); await this.reload(); });
    },
    async createAccount() {
      this.accountError = "";
      this.accountSuccess = "";
      const name = (this.username || "").trim();
      if (!name) { this.accountError = "请填写账号。"; return; }
      if ((this.userPassword || "").length < 8) { this.accountError = "密码至少需要 8 个字符。"; return; }
      this.accountBusy = true;
      try {
        await consoleApi("accounts", { username: name, password: this.userPassword, role: this.role });
        this.accountSuccess = "账号「" + name + "」已创建。";
        this.username = "";
        this.userPassword = "";
        this.role = "user";
        await this.reload();
      } catch (error) {
        this.accountError = error instanceof Error ? error.message : "创建账号失败。";
      } finally {
        this.accountBusy = false;
      }
    },
    toggle(account: ConsoleAccount) {
      return this.run(async () => { await consoleApi("accounts/enabled", { username: account.username, enabled: !account.enabled }); await this.reload(); });
    },
    openBots(account: ConsoleAccount) {
      this.botsModal = { username: account.username, bots: (account.allowedBots || []).slice() };
    },
    async saveAccountBots() {
      if (!this.botsModal || this.busy) return;
      this.busy = true;
      try {
        await consoleApi("accounts/bots", { username: this.botsModal.username, bots: this.botsModal.bots });
        this.botsModal = null;
        await this.reload();
        this.accountSuccess = "已更新机器人权限。";
      } catch (error) {
        this.error = error instanceof Error ? error.message : "保存机器人权限失败。";
      } finally {
        this.busy = false;
      }
    },
    openGuestPassword() {
      if (this.guest.enabled && !this.guest.bots.length) {
        this.error = "开启访客模式前，请至少选择一个机器人。";
        return;
      }
      this.error = "";
      this.confirming = { kind: "guest", action: "save", password: "", error: "" };
      this.$nextTick(() => {
        const input = this.$refs.confirmInput as HTMLInputElement | undefined;
        if (input) input.focus();
      });
    },
    openOwnPassword() {
      this.passwordModal = { mode: "own", username: this.me, currentPassword: "", newPassword: "", confirmPassword: "", error: "" };
    },
    openResetPassword(account: ConsoleAccount) {
      this.passwordModal = { mode: "admin", username: account.username, currentPassword: "", newPassword: "", confirmPassword: "", error: "" };
    },
    closePasswordModal() { this.passwordModal = null; },
    async submitPassword() {
      if (!this.passwordModal) return;
      const modal = this.passwordModal;
      modal.error = "";
      if (!modal.newPassword || modal.newPassword.length < 8) { modal.error = "新密码至少需要 8 个字符。"; return; }
      if (modal.newPassword !== modal.confirmPassword) { modal.error = "两次输入的新密码不一致。"; return; }
      this.accountBusy = true;
      try {
        if (modal.mode === "own") {
          await consoleApi("accounts/password", { currentPassword: modal.currentPassword, newPassword: modal.newPassword });
          this.accountSuccess = "密码已修改，请重新登录。";
          this.closePasswordModal();
          setTimeout(async () => {
            try { await consoleApi("logout", {}); } catch (_) { /* ignore */ }
            this.$router.replace("/");
          }, 800);
        } else {
          await consoleApi("accounts/password", { username: modal.username, newPassword: modal.newPassword });
          this.accountSuccess = "已修改账号「" + modal.username + "」的密码。";
          this.closePasswordModal();
        }
      } catch (error) {
        modal.error = error instanceof Error ? error.message : "修改密码失败。";
      } finally {
        this.accountBusy = false;
      }
    },
    async refreshUpdate() {
      try {
        const status = await consoleApi<{ currentVersion?: string }>("update/status");
        this.currentVersion = status.currentVersion || "";
        const check = await consoleApi<{ hasUpdate?: boolean; currentVersion?: string; latestVersion?: string }>("update/check", { source: "bodian" });
        if (check.currentVersion) this.currentVersion = check.currentVersion;
        this.latestVersion = check.latestVersion || "";
        this.hasUpdate = !!check.hasUpdate;
      } catch (_) { /* ignore */ }
    },
    onUpdateApplied() { this.hasUpdate = false; this.refreshUpdate(); },
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
      this.confirming = { kind: "service", action, password: "", error: "" };
      this.$nextTick(() => {
        const input = this.$refs.confirmInput as HTMLInputElement | undefined;
        if (input) input.focus();
      });
    },
    closeConfirm() {
      if (this.serviceBusy) return;
      this.confirming = null;
    },
    async submitConfirm() {
      if (!this.confirming || !this.confirming.password || this.serviceBusy) return;
      const state = this.confirming;
      this.serviceBusy = true;
      state.error = "";
      try {
        if (state.kind === "guest") {
          await consoleApi("guest/settings", { enabled: this.guest.enabled, bots: this.guest.bots, password: state.password });
          this.confirming = null;
          await this.reload();
          this.accountSuccess = this.guest.enabled ? "访客模式已开启。" : "访客模式已关闭。";
        } else {
          const path = state.action === "restart" ? "service/restart" : "service/stop";
          const result = await consoleApi<{ message?: string }>(path, { password: state.password });
          this.serviceMessage = result.message || (state.action === "restart" ? "服务即将重启，请稍候刷新。" : "服务即将停止。");
          this.confirming = null;
          if (state.action === "restart") setTimeout(() => { window.location.reload(); }, 12000);
        }
      } catch (error) {
        state.error = error instanceof Error ? error.message : "操作失败。";
      } finally {
        this.serviceBusy = false;
      }
    },
  },
});
</script>

<style scoped lang="less">
.bd-admin { max-width: 880px; padding: 6px 26px 28px; }
.bd-admin-head h1 { margin: 0; color: var(--console-ink); font-size: 30px; font-weight: 800; letter-spacing: -.025em; }
.bd-admin-head p { margin: 4px 0 0; color: var(--console-muted); font-size: 13px; }

.bd-card {
  margin-top: 16px; padding: 4px 0;
  border: 1px solid var(--console-line); border-radius: var(--console-radius);
  background: var(--console-surface);
}
.bd-card > h2, .bd-card > .bd-card-head {
  margin: 0; padding: 15px 18px 9px;
  display: flex; align-items: center; justify-content: space-between; gap: 12px;
  font-size: 16px !important; font-weight: 750; letter-spacing: -.015em; line-height: 1.3;
}
.bd-card-head span { color: var(--console-muted); font-size: 12px; font-weight: 500; }

.bd-field {
  display: flex; align-items: center; gap: 18px;
  padding: 11px 18px; border-top: 1px solid var(--console-line);
}
.bd-field.align-top { align-items: flex-start; }
.bd-label { flex: 0 0 190px; min-width: 0; }
.bd-label b { display: block; font-size: 13px; font-weight: 600; }
.bd-label span { display: block; margin-top: 2px; color: var(--console-muted); font-size: 11.5px; line-height: 1.45; }
.bd-control { flex: 1; min-width: 0; display: flex; align-items: center; justify-content: flex-end; gap: 8px; }
.bd-control.wrap { flex-wrap: wrap; }
.bd-control.left { justify-content: flex-start; }

.bd-input {
  height: 30px; padding: 0 10px; border-radius: 7px;
  border: 1px solid var(--console-line-strong);
  background: var(--console-bg); color: var(--console-ink);
  font-size: 12.5px; outline: none;
}
.bd-input:focus { border-color: var(--console-brand); box-shadow: 0 0 0 3px var(--console-brand-soft); }
.bd-input.narrow { width: 130px; }
.bd-input.wide { width: 100%; height: 36px; margin-top: 6px; }
select.text-input { padding-right: 24px; cursor: pointer; }

.bd-row-item { display: flex; align-items: center; gap: 10px; padding: 10px 18px; border-top: 1px solid var(--console-line); }
.bd-row-icon {
  width: 30px; height: 30px; flex: 0 0 auto; display: grid; place-items: center;
  border-radius: 8px; background: var(--console-brand-soft); color: var(--console-brand);
}
.bd-row-copy { flex: 1; min-width: 0; }
.bd-row-copy b, .bd-row-copy span { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.bd-row-copy b { color: var(--console-ink); font-size: 13px; font-weight: 600; }
.bd-row-copy span { margin-top: 2px; color: var(--console-muted); font-size: 11.5px; }

.bd-account-item { display: flex; align-items: center; gap: 10px; padding: 10px 18px; border-top: 1px solid var(--console-line); }
.bd-avatar {
  width: 30px; height: 30px; flex: 0 0 auto; display: grid; place-items: center;
  border-radius: 50%; background: linear-gradient(140deg, #7d7aff, #4f46e5);
  color: #fff; font-size: 12px; font-weight: 700;
}

.bd-tag {
  height: 22px; padding: 0 9px; display: inline-flex; align-items: center;
  border-radius: 999px; background: var(--console-fill); color: var(--console-muted);
  font-size: 11px; font-weight: 600; white-space: nowrap;
}
.bd-tag.connected, .bd-tag.ok { background: var(--console-ok-soft); color: var(--console-ok); }
.bd-tag.connecting { background: var(--console-warn-soft); color: var(--console-warn); }
.bd-tag.warn { background: var(--console-warn-soft); color: var(--console-warn); }

.bd-switch {
  position: relative; width: 44px; height: 26px; flex: 0 0 auto;
  border: 0; border-radius: 999px; background: var(--console-fill-2);
  cursor: pointer; transition: background .18s ease;
}
.bd-switch::after {
  content: ""; position: absolute; top: 2px; left: 2px;
  width: 22px; height: 22px; border-radius: 50%;
  background: #fff; box-shadow: 0 1px 3px rgba(0, 0, 0, .3);
  transition: transform .18s ease;
}
.bd-switch.on { background: var(--console-ok); }
.bd-switch.on::after { transform: translateX(18px); }
.bd-switch:disabled { opacity: .5; cursor: not-allowed; }

.bd-check { display: inline-flex; align-items: center; gap: 7px; margin: 0 14px 6px 0; font-size: 12.5px; cursor: pointer; }
.bd-check input { width: 15px; height: 15px; margin: 0; accent-color: var(--console-brand); cursor: pointer; }
.bd-check-list { display: flex; flex-wrap: wrap; gap: 4px 0; margin-top: 10px; padding: 10px 12px; border-radius: 9px; background: var(--console-surface-2); }

.bd-muted { margin: 0; padding: 4px 18px 10px; color: var(--console-muted); font-size: 12.5px; line-height: 1.6; }
.bd-muted.note { padding-top: 0; padding-bottom: 14px; font-size: 11.5px; }

.bd-log {
  margin: 0; max-height: 220px; overflow: auto;
  padding: 12px 16px; border-radius: 0 0 var(--console-radius) var(--console-radius);
  background: #0f0f11; color: #c8c8cc;
  font-family: "Cascadia Mono", Consolas, monospace;
  font-size: 11.5px; line-height: 1.6; white-space: pre-wrap; word-break: break-word;
}

.bd-modal-mask { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 20px; background: rgba(0, 0, 0, .52); }
.bd-modal {
  width: 100%; max-width: 460px; padding: 20px 22px 18px;
  border: 1px solid var(--console-line); border-radius: 18px;
  background: var(--console-surface); color: var(--console-ink);
  box-shadow: var(--console-shadow-md);
}
.bd-modal-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.bd-modal-head h2 { margin: 0; color: var(--console-ink); font-size: 16px; font-weight: 750; }
.bd-modal-head .icon-button { margin-left: auto; }
.bd-modal-label { display: block; margin-top: 12px; color: var(--console-ink); font-size: 12.5px; font-weight: 600; }
.bd-modal-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 18px; }
.bd-modal-actions .pill { height: 32px; padding: 0 14px; }
.pill.danger { color: var(--console-brand); }
.pill.danger:hover:not(:disabled) { background: var(--console-danger-soft); }

@media (max-width: 1023px) {
  .bd-admin { padding: 4px 16px 24px; }
  .bd-admin-head h1 { font-size: 25px; }
  .bd-field { flex-direction: column; align-items: flex-start; gap: 8px; }
  .bd-label { flex: none; }
  .bd-control { width: 100%; justify-content: flex-start; }
  .bd-row-item, .bd-account-item { flex-wrap: wrap; gap: 8px; }
  .bd-row-copy { flex: 1 1 140px; }
  .bd-input.narrow { width: 100%; }
  .bd-control.wrap .bd-input.narrow { flex: 1 1 120px; }
}
</style>
