<template>
  <transition name="drawer">
    <div v-if="open" class="drawer-wrap">
      <button class="shade" title="关闭队列" aria-label="关闭队列" @click="$emit('close')"></button>
      <aside class="drawer" aria-label="待播队列">
        <header>
          <div><strong>待播队列</strong><small>{{ queue.length }} 首歌曲</small></div>
          <button type="button" class="icon-button" title="关闭" aria-label="关闭" @click="$emit('close')"><b-icon icon="close" /></button>
        </header>
        <div class="items">
          <article
            v-for="(track, index) in queue"
            :key="track.resource.resid + index"
            :class="['q-row', { active: track.active, selectable: !track.active }]"
            role="button"
            tabindex="0"
            :aria-current="track.active ? 'true' : undefined"
            @click="play(track)"
            @keydown.enter.prevent="play(track)"
            @keydown.space.prevent="play(track)"
          >
            <span class="idx">{{ String(index + 1).padStart(2, '0') }}</span>
            <span class="scover"><img v-if="track.coverUrl" :src="track.coverUrl" :alt="track.title"><b-icon v-else icon="music-note" /></span>
            <span class="info"><strong>{{ track.title }}</strong><small>{{ track.type || '歌曲' }}</small></span>
            <span v-if="track.active" class="eq" aria-label="正在播放"><i></i><i></i><i></i></span>
            <span class="dur">{{ duration(track) }}</span>
          </article>
          <p v-if="!queue.length" class="empty">待播队列为空</p>
        </div>
        <footer v-if="isAdmin && queue.length">
          <button type="button" class="clear-button" @click="$emit('clear')"><b-icon icon="playlist-remove" />清空待播队列</button>
        </footer>
      </aside>
    </div>
  </transition>
</template>

<script lang="ts">
import Vue from "vue";
import { Track } from "../ConsoleApi";

export default Vue.extend({
  props: {
    open: { type: Boolean, required: true },
    queue: { type: Array as () => Track[], required: true },
    isAdmin: { type: Boolean, required: true },
  },
  methods: {
    play(track: Track) { this.$emit("play", track.resource); },
    duration(track: Track) {
      const value = track.resource.add && (track.resource.add.duration || track.resource.add.length);
      return value ? String(value) : "--:--";
    },
  },
});
</script>

<style scoped lang="less">
.drawer-wrap { position: fixed; z-index: 12; inset: 0; }
.shade { position: absolute; inset: 0; width: 100%; border: 0; background: rgba(0, 0, 0, .35); backdrop-filter: blur(3px); cursor: pointer; }
.drawer { position: absolute; z-index: 1; inset: 0 0 0 auto; width: 400px; max-width: 92vw; display: flex; flex-direction: column; background: var(--console-glass); color: var(--console-ink); border-left: 1px solid var(--console-line-strong); box-shadow: -18px 0 50px rgba(0, 0, 0, .16); backdrop-filter: blur(24px) saturate(140%); }
header { display: flex; align-items: center; gap: 12px; padding: 22px 24px 16px; }
header strong, header small { display: block; }
header strong { font-size: 18px; font-weight: 800; letter-spacing: -.01em; }
header small { margin-top: 2px; color: var(--console-muted); font-size: 12.5px; }
.icon-button { width: 36px; height: 36px; display: grid; place-items: center; margin-left: auto; border: 0; border-radius: 50%; background: transparent; color: var(--console-muted); cursor: pointer; }
.icon-button:hover { background: var(--console-hover); color: var(--console-ink); }
.items { flex: 1; overflow-y: auto; padding: 0 12px; }
.q-row { display: flex; align-items: center; gap: 14px; height: 62px; padding: 0 12px; border-radius: 12px; transition: background .15s ease; }
.q-row.selectable { cursor: pointer; }
.q-row.selectable:hover, .q-row.selectable:focus-visible { background: var(--console-hover); outline: 0; }
.q-row .idx { width: 26px; flex: 0 0 auto; color: var(--console-muted-2); font-size: 13px; font-variant-numeric: tabular-nums; text-align: right; }
.scover { width: 42px; height: 42px; flex: 0 0 auto; display: grid; place-items: center; overflow: hidden; border-radius: 9px; background: var(--console-surface-2); color: var(--console-muted); }
.scover img { width: 100%; height: 100%; object-fit: cover; }
.info { min-width: 0; flex: 1; }
.info strong, .info small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.info strong { font-size: 14px; font-weight: 600; }
.info small { margin-top: 2px; color: var(--console-muted-2); font-size: 12px; }
.active .info strong { color: var(--console-brand); }
.dur { color: var(--console-muted-2); font-size: 12px; font-variant-numeric: tabular-nums; }
.eq { display: flex; align-items: flex-end; gap: 2px; height: 12px; }
.eq i { width: 2.5px; height: 5px; border-radius: 2px; background: var(--console-brand); animation: eq 1s ease-in-out infinite; }
.eq i:nth-child(2) { animation-delay: .22s; }
.eq i:nth-child(3) { animation-delay: .44s; }
@keyframes eq { 0%, 100% { height: 4px; } 50% { height: 11px; } }
.empty { padding: 36px 12px; color: var(--console-muted); text-align: center; font-size: 14px; }
footer { padding: 14px 24px calc(env(safe-area-inset-bottom) + 18px); border-top: 1px solid var(--console-line); }
.clear-button { width: 100%; height: 44px; display: inline-flex; align-items: center; justify-content: center; gap: 7px; border: 1px solid var(--console-line-strong); border-radius: 12px; background: transparent; color: var(--console-brand); font: inherit; font-size: 14px; font-weight: 700; cursor: pointer; }
.clear-button:hover { background: var(--console-brand-soft); }
.drawer-enter-active, .drawer-leave-active { transition: opacity 180ms ease; }
.drawer-enter-active .drawer, .drawer-leave-active .drawer { transition: transform 240ms var(--console-ease-drawer); }
.drawer-enter, .drawer-leave-to { opacity: 0; }
.drawer-enter .drawer, .drawer-leave-to .drawer { transform: translateX(100%); }
@media (max-width: 760px) {
  .drawer { inset: auto 0 0; width: 100%; height: 76vh; height: 76dvh; max-height: 620px; border: 0; border-top: 1px solid var(--console-line-strong); border-radius: 24px 24px 0 0; }
  .drawer-enter .drawer, .drawer-leave-to .drawer { transform: translateY(100%); }
}
</style>
