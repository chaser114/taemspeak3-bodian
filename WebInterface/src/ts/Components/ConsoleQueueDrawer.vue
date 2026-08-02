<template>
  <transition name="drawer">
    <div v-if="open" class="drawer-wrap">
      <button class="shade" title="关闭队列" @click="$emit('close')"></button>
      <aside class="drawer">
        <header>
          <div><span>待播队列</span><small>{{ queue.length }} 首歌曲</small></div>
          <button type="button" title="关闭" @click="$emit('close')"><b-icon icon="close" /></button>
        </header>
        <div class="items">
          <article
            v-for="(track, index) in queue"
            :key="track.resource.resid + index"
            :class="{ active: track.active, selectable: !track.active }"
            role="button"
            tabindex="0"
            :aria-current="track.active ? 'true' : undefined"
            @click="play(track)"
            @keydown.enter.prevent="play(track)"
            @keydown.space.prevent="play(track)"
          >
            <i>{{ String(index + 1).padStart(2, '0') }}</i>
            <div><strong>{{ track.title }}</strong><small>{{ track.type }}</small></div>
            <b-icon v-if="track.active" icon="volume-high" />
          </article>
          <p v-if="!queue.length">待播队列为空</p>
        </div>
        <footer v-if="isAdmin && queue.length">
          <button type="button" @click="$emit('clear')"><b-icon icon="playlist-remove" />清空待播队列</button>
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
    play(track: Track) {
      this.$emit("play", track.resource);
    },
  },
});
</script>

<style scoped lang="less">
.drawer-wrap { position: fixed; z-index: 12; inset: 0; }
.shade { position: absolute; inset: 0; width: 100%; border: 0; background: rgba(0, 0, 0, .42); cursor: pointer; }
.drawer { position: absolute; inset: 0 0 0 auto; width: 420px; max-width: 92vw; display: flex; flex-direction: column; background: var(--console-surface); color: var(--console-ink); border-left: 1px solid var(--console-line); box-shadow: -18px 0 50px rgba(0, 0, 0, .22); backdrop-filter: blur(24px) saturate(140%); }
header { display: flex; justify-content: space-between; align-items: center; padding: 28px 24px 20px; border-bottom: 1px solid var(--console-line); }
header span, header small { display: block; }
header span { font-size: 19px; font-weight: 700; letter-spacing: -.01em; }
header small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
header button { width: 38px; height: 38px; min-width: 38px; min-height: 38px; display: grid; place-items: center; padding: 0; border: 0; border-radius: 50%; background: var(--console-surface-2); color: var(--console-muted); cursor: pointer; line-height: 1; }
header button:hover { background: var(--console-hover); color: var(--console-ink); }
.items { flex: 1; overflow: auto; padding: 10px; }
.items article { display: flex; align-items: center; gap: 12px; min-height: 68px; padding: 10px 12px; border-radius: var(--console-radius-sm); outline: 0; transition: background-color 160ms ease, transform 160ms ease; }
.items article.selectable { cursor: pointer; }
.items article.selectable:hover, .items article.selectable:focus-visible { background: var(--console-hover); transform: translateX(-2px); }
.items article.active { background: var(--console-brand-soft); border-left: 3px solid var(--console-brand); padding-left: 9px; color: var(--console-brand); font-weight: 600; }
.items i { width: 26px; color: var(--console-muted-2); font-style: normal; font-size: 12px; }
.items div { min-width: 0; flex: 1; }
.items strong, .items small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.items strong { font-size: 14px; }
.items small { margin-top: 4px; color: var(--console-muted); font-size: 12px; }
.items p { text-align: center; color: var(--console-muted); font-size: 14px; }
footer { padding: 16px 20px; border-top: 1px solid var(--console-line); }
footer button { width: 100%; height: 42px; border: 1px solid var(--console-danger-soft); border-radius: 10px; background: var(--console-danger-soft); color: var(--console-danger); font: inherit; cursor: pointer; font-weight: 600; }
footer button:hover { background: var(--console-brand); color: #fff; }
.drawer-enter-active, .drawer-leave-active { transition: opacity 180ms ease; }
.drawer-enter-active .drawer, .drawer-leave-active .drawer { transition: transform 240ms var(--console-ease-drawer); }
.drawer-enter, .drawer-leave-to { opacity: 0; }
.drawer-enter .drawer, .drawer-leave-to .drawer { transform: translateX(100%); }
@media (max-width: 760px) {
  .drawer { inset: auto 0 calc(var(--console-nav-h) + env(safe-area-inset-bottom)) 0; width: 100%; height: 76vh; height: 76dvh; max-height: 620px; border: 0; border-radius: 22px 22px 0 0; }
  .drawer-enter .drawer, .drawer-leave-to .drawer { transform: translateY(100%); }
}
</style>
