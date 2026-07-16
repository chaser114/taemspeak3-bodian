<template>
  <main class="admin">
    <header><p>控制台</p><h1>管理机器人与账号</h1></header>
    <p v-if="error" class="error">{{ error }}</p>

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
        <h2>网页账号</h2>
        <form class="create-account" @submit.prevent="createAccount">
          <label>账号<input v-model.trim="username"></label>
          <label>密码<input v-model="userPassword" type="password"></label>
          <label>角色<select v-model="role"><option value="user">普通用户</option><option value="admin">管理员</option></select></label>
          <button>创建账号</button>
        </form>
        <article v-for="account in accounts" :key="account.username">
          <div><b>{{ account.username }}</b><small>{{ account.role === 'admin' ? '管理员' : '普通用户' }}</small></div>
          <button class="text-button" @click="toggle(account)">{{ account.enabled ? '已启用' : '已停用' }}</button>
        </article>
      </section>
    </div>

    <div v-if="editing" class="modal-mask" @click.self="closeEdit">
      <section class="edit-modal" role="dialog" aria-modal="true" aria-labelledby="edit-title">
        <div class="modal-heading"><h2 id="edit-title">编辑机器人</h2><button class="close" title="关闭" @click="closeEdit">×</button></div>
        <p>{{ editing.name }}</p>
        <form @submit.prevent="saveEdit">
          <label>服务器地址<input v-model.trim="editAddress"></label>
          <label>机器人名称<input v-model.trim="editNickname"></label>
          <label>服务器密码（留空则不设密码）<input v-model="editPassword" type="password"></label>
          <div class="modal-actions"><button type="button" class="secondary" @click="closeEdit">取消</button><button>保存并重新连接</button></div>
        </form>
      </section>
    </div>
  </main>
</template>

<script lang="ts">
import Vue from "vue";
import { consoleApi, ConsoleUser } from "../ConsoleApi";

interface Bot { id: string; name: string; address: string; status: string; }
interface Account { username: string; role: string; enabled: boolean; }

export default Vue.extend({
  data() {
    return {
      brandName: "波点音乐", bots: [] as Bot[], accounts: [] as Account[], error: "",
      newAddress: "", newNickname: "波点音乐", newPassword: "",
      username: "", userPassword: "", role: "user",
      editing: null as Bot | null, editAddress: "", editNickname: "", editPassword: "",
    };
  },
  async created() {
    const user = await consoleApi<ConsoleUser>("me");
    if (user.role !== "admin") { this.$router.replace("/music"); return; }
    this.brandName = user.brandName;
    await this.reload();
  },
  methods: {
    async reload() {
      this.bots = (await consoleApi<{ bots: Bot[] }>("bots")).bots;
      this.accounts = (await consoleApi<{ accounts: Account[] }>("accounts")).accounts;
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
    createAccount() {
      return this.run(async () => {
        await consoleApi("accounts", { username: this.username, password: this.userPassword, role: this.role });
        this.username = ""; this.userPassword = ""; await this.reload();
      });
    },
    toggle(account: Account) { return this.run(async () => { await consoleApi("accounts/enabled", { username: account.username, enabled: !account.enabled }); await this.reload(); }); },
  },
});
</script>

<style scoped lang="less">
.admin { max-width: 1120px; margin: auto; padding: 42px; }
.admin > header p { margin: 0 0 5px; color: #287f74; font-weight: bold; }
.admin h1 { margin: 0; font-size: 32px; }
.grid { display: grid; grid-template-columns: minmax(320px, .9fr) minmax(0, 1.8fr); grid-template-rows: auto auto auto; gap: 20px; margin-top: 28px; align-items: start; }
.grid section { min-width: 0; padding: 22px; border: 1px solid #dfe6e8; border-radius: 10px; background: #fff; }
.brand-card { grid-column-start: 1; grid-column-end: 2; grid-row: 1; }
.new-bot-card { grid-column-start: 1; grid-column-end: 2; grid-row: 2; }
.bots-card { grid-column-start: 2; grid-column-end: 3; grid-row: 1 / span 2; }
.accounts-card { grid-column-start: 1; grid-column-end: 3; grid-row: 3; }
.section-heading, .modal-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.section-heading h2, .grid h2, .edit-modal h2 { margin: 0; font-size: 18px; }
.section-heading span { color: #778595; font-size: 13px; }
.new-bot-card > p, .edit-modal > p, .empty { color: #778595; font-size: 13px; }
label { display: block; margin-top: 13px; color: #44515e; font-size: 13px; }
input, select { width: 100%; height: 40px; margin-top: 5px; padding: 0 10px; border: 1px solid #d5e0e3; border-radius: 6px; background: #fff; color: var(--console-ink); font: inherit; }
form > button { margin-top: 16px; }
button { height: 38px; padding: 0 12px; border: 0; border-radius: 6px; background: #4fb8a8; color: #fff; cursor: pointer; font: inherit; }
article { display: flex; align-items: center; gap: 10px; padding: 13px 0; border-top: 1px solid #edf0f1; }
.bots-card article:first-of-type, .accounts-card article:first-of-type { margin-top: 12px; }
article div { flex: 1; min-width: 0; }
article b, article small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
article small { margin-top: 4px; color: #778595; font-size: 12px; }
.status { padding: 4px 8px; border-radius: 12px; background: #f1f3f5; color: #788595; font-size: 12px; white-space: nowrap; }
.status.connected { background: #e4f7f1; color: #197565; }
.status.connecting { background: #fff3d9; color: #9b6b10; }
.text-button { min-width: 52px; flex: 0 0 auto; white-space: nowrap; background: #edf7f5; color: #287f74; }
.text-button.delete { background: #fff0f1; color: #bd4d55; }
.create-account { display: grid; grid-template-columns: 1fr 1fr 140px auto; align-items: end; gap: 10px; }
.create-account label { margin-top: 0; }
.create-account button { margin-top: 0; }
.error { margin-top: 18px; color: #b34d57; }
.modal-mask { position: fixed; z-index: 20; inset: 0; display: grid; place-items: center; padding: 20px; background: rgba(25,37,48,.38); }
.edit-modal { width: 100%; max-width: 480px; padding: 24px; border-radius: 10px; background: #fff; box-shadow: 0 18px 54px rgba(20,35,45,.22); }
.close { width: 34px; padding: 0; border-radius: 50%; background: #edf1f2; color: #53616e; font-size: 24px; line-height: 1; }
.modal-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 20px; }
.modal-actions button { margin: 0; }
.secondary { background: #edf1f2; color: #4c5d69; }
@media (max-width: 1000px) { .grid { grid-template-columns: 1fr; grid-template-rows: none; } .brand-card, .new-bot-card, .bots-card, .accounts-card { grid-column-start: auto; grid-column-end: auto; grid-row: auto; } .create-account { grid-template-columns: 1fr; } .admin { padding: 28px 16px; } }
@media (max-width: 600px) { .admin h1 { font-size: 27px; } .bots-card article { align-items: flex-start; flex-wrap: wrap; } .status { margin-left: auto; } }
</style>
