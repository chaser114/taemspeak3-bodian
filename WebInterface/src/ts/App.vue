<template>
  <div id="app" class="console-app">
    <ConsoleShell v-if="$route.path !== '/'"><router-view/></ConsoleShell>
    <router-view v-else/>
  </div>
</template>
<script lang="ts">
import ConsoleShell from "./Components/ConsoleShell.vue";
export default { components: { ConsoleShell } };
</script>
<style lang="less">
:root {
  --console-brand: #4fb8a8;
  --console-brand-dark: #257e73;
  --console-brand-soft: #e8f7f4;
  --console-ink: #1e2733;
  --console-muted: #788595;
  --console-line: #e5e9ee;
  --console-surface: rgba(255, 255, 255, 0.9);
  --console-canvas: linear-gradient(180deg, #f8fafb 0%, #f0f4f6 100%);
  --console-danger: #d65c63;
  --console-danger-soft: #fff1f2;
  --console-warn: #9a6b00;
  --console-warn-soft: #fff8e6;

  /* 圆角系统 */
  --console-radius-xs: 6px;
  --console-radius-sm: 10px;
  --console-radius: 14px;
  --console-radius-lg: 18px;
  --console-radius-xl: 24px;
  --console-radius-full: 999px;

  /* 阴影系统 */
  --console-shadow-sm: 0 2px 8px rgba(30, 50, 55, 0.04);
  --console-shadow: 0 4px 12px rgba(30, 50, 55, 0.08);
  --console-shadow-md: 0 8px 24px rgba(30, 50, 55, 0.12);
  --console-shadow-lg: 0 12px 32px rgba(30, 50, 55, 0.16);
  --console-shadow-hover: 0 8px 20px rgba(79, 184, 168, 0.15);

  /* 间距系统 */
  --console-space-xs: 4px;
  --console-space-sm: 8px;
  --console-space-md: 12px;
  --console-space-lg: 16px;
  --console-space-xl: 24px;
  --console-space-2xl: 32px;
  --console-space-3xl: 48px;

  /* 动画曲线 */
  --console-ease-out: cubic-bezier(0.33, 1, 0.68, 1);
  --console-ease-in-out: cubic-bezier(0.65, 0, 0.35, 1);

  /* 播放器和导航高度 */
  --console-player-h: 110px;
  --console-nav-h: 60px;

  --console-font: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", Roboto, sans-serif;
}
html, body, #app {
  min-height: 100%;
  margin: 0;
  background: var(--console-canvas);
}
.console-app {
  min-height: 100vh;
  font-family: var(--console-font);
  color: var(--console-ink);
  font-size: 15px;
  line-height: 1.5;
  -webkit-font-smoothing: antialiased;
}
* { box-sizing: border-box; }
button, input, select, textarea { font: inherit; }
button {
  transition: transform 150ms var(--console-ease-out), background-color 150ms ease, color 150ms ease, opacity 150ms ease, box-shadow 150ms ease;
}
@media (hover: hover) and (pointer: fine) {
  button:not(:disabled):active { transform: scale(0.97); }
}
.button { letter-spacing: 0 !important; }
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
  }
}
</style>
