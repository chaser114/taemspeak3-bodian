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
  --console-surface: #ffffff;
  --console-canvas: #f4f6f8;
  --console-danger: #d65c63;
  --console-danger-soft: #fff1f2;
  --console-warn: #9a6b00;
  --console-warn-soft: #fff8e6;
  --console-radius-sm: 8px;
  --console-radius: 12px;
  --console-radius-lg: 16px;
  --console-shadow: 0 8px 24px rgba(30, 50, 55, 0.06);
  --console-shadow-md: 0 16px 40px rgba(30, 50, 55, 0.1);
  --console-ease-out: cubic-bezier(0.23, 1, 0.32, 1);
  --console-space-1: 8px;
  --console-space-2: 12px;
  --console-space-3: 16px;
  --console-space-4: 24px;
  --console-space-5: 32px;
  --console-font: -apple-system, BlinkMacSystemFont, "Segoe UI", "PingFang SC", "Hiragino Sans GB", "Microsoft YaHei", Roboto, sans-serif;
  --console-player-h: 96px;
  --console-nav-h: 60px;
}
html, body, #app { min-height: 100%; margin: 0; background: var(--console-canvas); }
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
