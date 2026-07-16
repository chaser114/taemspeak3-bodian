<template>
  <section :class="['player-bar', { expanded }]">
    <button v-if="expanded" class="back-button" title="返回播放器" @click="expanded = false">‹ 返回</button>

    <button class="track-summary" title="打开完整播放器" @click="expanded = !expanded">
      <img v-if="state.current && state.current.coverUrl" :src="state.current.coverUrl" :alt="trackTitle">
      <span v-else class="cover-placeholder">♫</span>
      <span class="track-copy">
        <b>{{ trackTitle }}</b>
        <small>{{ state.current ? '正在频道中播放' : '搜索后即可加入待播' }}</small>
      </span>
    </button>

    <div class="controls" aria-label="播放控制">
      <button title="上一首" aria-label="上一首" @click="$emit('previous')">⏮</button>
      <button class="play-button" :title="state.paused ? '继续播放' : '暂停播放'" :aria-label="state.paused ? '继续播放' : '暂停播放'" @click="$emit('pause')">{{ state.paused ? '▶' : 'Ⅱ' }}</button>
      <button title="下一首" aria-label="下一首" @click="$emit('next')">⏭</button>
    </div>

    <div class="timeline">
      <div class="progress-track"><i :style="{ width: progress + '%' }"></i></div>
      <small>{{ time(state.position || 0) }} / {{ time(state.length || 0) }}</small>
    </div>

    <button class="queue-button" title="待播队列" aria-label="待播队列" @click="$emit('queue')">
      ☷<em v-if="state.queue.length">{{ state.queue.length }}</em>
    </button>
  </section>
</template>

<script lang="ts">
import Vue from "vue";
import { MusicState } from "../ConsoleApi";

export default Vue.extend({
  props: { state: { type: Object as () => MusicState, required: true } },
  data() { return { expanded: false }; },
  computed: {
    progress(): number {
      const state = this.state as MusicState;
      return state.length ? Math.min(100, state.position! / state.length * 100) : 0;
    },
    trackTitle(): string {
      return this.state.current ? this.state.current.title : "等待点歌";
    },
  },
  methods: {
    time(seconds: number) {
      return Math.floor(seconds / 60) + ":" + String(Math.floor(seconds % 60)).padStart(2, "0");
    },
  },
});
</script>

<style scoped lang="less">
.player-bar { position: fixed; z-index: 6; left: 216px; right: 0; bottom: 0; height: 96px; display: flex; align-items: center; gap: 24px; padding: 12px 34px; background: rgba(255,255,255,.97); border-top: 1px solid #dfe6e8; box-shadow: 0 -10px 28px rgba(30,50,55,.07); }
button { border: 0; background: transparent; cursor: pointer; }
.track-summary { min-width: 250px; max-width: 34%; display: flex; align-items: center; gap: 12px; color: var(--console-ink); text-align: left; }
.track-summary img, .cover-placeholder { width: 58px; height: 58px; flex: 0 0 58px; border-radius: 9px; object-fit: cover; background: #e1f1ed; display: grid; place-items: center; color: #287f74; font-size: 24px; }
.track-copy { min-width: 0; }
.track-copy b, .track-copy small { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track-copy small { margin-top: 5px; color: #768493; }
.controls { display: flex; align-items: center; gap: 10px; }
.controls button { width: 38px; height: 38px; border-radius: 50%; color: #476170; font-size: 18px; transition: background .15s ease, color .15s ease; }
.controls button:hover { background: #edf7f5; color: #257e73; }
.controls .play-button { width: 52px; height: 52px; background: #4fb8a8; color: #fff; font-size: 20px; box-shadow: 0 5px 12px rgba(79,184,168,.28); }
.controls .play-button:hover { background: #257e73; color: #fff; }
.timeline { display: flex; align-items: center; gap: 12px; flex: 1; min-width: 160px; }
.progress-track { height: 4px; flex: 1; overflow: hidden; border-radius: 4px; background: #dfe6e8; }
.progress-track i { display: block; height: 100%; background: #4fb8a8; }
.timeline small { color: #83909d; font-size: 12px; white-space: nowrap; }
.queue-button { position: relative; width: 42px; height: 42px; border-radius: 50%; color: #526b75; font-size: 22px; }
.queue-button:hover { background: #edf7f5; color: #257e73; }
.queue-button em { position: absolute; top: -2px; right: -2px; min-width: 17px; border-radius: 10px; background: #287f74; color: #fff; font-size: 10px; font-style: normal; }

.player-bar.expanded { top: 0; left: 216px; bottom: 0; height: auto; z-index: 10; flex-direction: column; justify-content: center; gap: 18px; padding: 52px 34px; background: #f7fbfa; }
.expanded .track-summary { max-width: none; display: grid; text-align: center; }
.expanded .track-summary img, .expanded .cover-placeholder { width: 46vw; height: 46vw; max-width: 340px; max-height: 340px; margin: auto; border-radius: 18px; }
.expanded .track-copy small { margin-top: 8px; }
.expanded .controls { margin-top: 16px; }
.expanded .timeline { width: 70vw; max-width: 620px; flex: none; }
.expanded .queue-button { margin-top: 2px; }
.back-button { position: absolute; top: 24px; left: 30px; color: #287f74; font-size: 16px; }

@media (max-width: 760px) {
  .player-bar { left: 0; bottom: 58px; height: 76px; gap: 8px; padding: 9px 14px; }
  .track-summary { min-width: 0; flex: 1; }
  .track-summary img, .cover-placeholder { width: 50px; height: 50px; flex-basis: 50px; }
  .controls { gap: 2px; }
  .controls button { width: 31px; height: 31px; font-size: 15px; }
  .controls .play-button { width: 42px; height: 42px; }
  .timeline { display: none; }
  .player-bar.expanded { left: 0; bottom: 58px; padding: 52px 20px 28px; }
  .expanded .track-summary img, .expanded .cover-placeholder { width: 72vw; height: 72vw; max-width: 290px; max-height: 290px; }
  .expanded .timeline { display: flex; width: 100%; max-width: 430px; }
  .back-button { top: 18px; left: 18px; }
}
</style>
