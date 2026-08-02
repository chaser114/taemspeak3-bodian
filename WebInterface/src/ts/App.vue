<template>
  <div id="app" class="console-app">
    <ConsoleShell v-if="$route.path !== '/'"><router-view/></ConsoleShell>
    <router-view v-else/>
  </div>
</template>

<script lang="ts">
import Vue from "vue";
import ConsoleShell from "./Components/ConsoleShell.vue";

export default Vue.extend({
  components: { ConsoleShell },
  created() {
    this.applyInitialTheme();
  },
  methods: {
    applyInitialTheme() {
      const query = new URLSearchParams(window.location.search).get("theme");
      const saved = window.localStorage.getItem("bd-theme");
      const system = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
      const theme = query === "dark" || query === "light" ? query : (saved === "dark" || saved === "light" ? saved : system);
      document.documentElement.setAttribute("data-theme", theme);
    },
  },
});
</script>

<style lang="less">
:root {
  --console-bg: #ffffff;
  --console-surface: #ffffff;
  --console-surface-2: #f5f5f7;
  --console-surface-3: #efeff1;
  --console-ink: #1d1d1f;
  --console-muted: #6e6e73;
  --console-muted-2: #86868b;
  --console-line: rgba(0, 0, 0, .08);
  --console-line-strong: rgba(0, 0, 0, .18);
  --console-hover: rgba(0, 0, 0, .05);
  --console-pill: rgba(0, 0, 0, .07);
  --console-brand: #fa233b;
  --console-brand-dark: #d91e3a;
  --console-brand-soft: rgba(250, 35, 59, .09);
  --console-danger: #d91e3a;
  --console-danger-soft: rgba(250, 35, 59, .09);
  --console-warn: #9a6500;
  --console-warn-soft: rgba(235, 173, 0, .14);
  --console-canvas: var(--console-bg);
  --console-glass: rgba(255, 255, 255, .88);
  --console-font: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI", "PingFang SC", "Microsoft YaHei", Roboto, sans-serif;
  --console-radius-xs: 8px;
  --console-radius-sm: 12px;
  --console-radius: 16px;
  --console-radius-lg: 22px;
  --console-radius-full: 999px;
  --console-shadow-sm: 0 2px 12px rgba(0, 0, 0, .04);
  --console-shadow: 0 8px 28px rgba(0, 0, 0, .08);
  --console-shadow-md: 0 18px 54px rgba(0, 0, 0, .14);
  --console-ease-out: cubic-bezier(.23, 1, .32, 1);
  --console-ease-drawer: cubic-bezier(.32, .72, 0, 1);
  --console-player-h: 96px;
  --console-nav-h: 64px;
}

html[data-theme="dark"] {
  --console-bg: #000000;
  --console-surface: #000000;
  --console-surface-2: #1c1c1e;
  --console-surface-3: #262628;
  --console-ink: #f5f5f7;
  --console-muted: #98989d;
  --console-muted-2: #6e6e73;
  --console-line: rgba(255, 255, 255, .12);
  --console-line-strong: rgba(255, 255, 255, .24);
  --console-hover: rgba(255, 255, 255, .09);
  --console-pill: rgba(255, 255, 255, .12);
  --console-brand-soft: rgba(250, 35, 59, .2);
  --console-danger-soft: rgba(250, 35, 59, .2);
  --console-warn-soft: rgba(235, 173, 0, .2);
  --console-glass: rgba(0, 0, 0, .84);
  --console-shadow-sm: 0 2px 12px rgba(0, 0, 0, .25);
  --console-shadow: 0 8px 28px rgba(0, 0, 0, .36);
  --console-shadow-md: 0 18px 54px rgba(0, 0, 0, .52);
}

html, body, #app {
  min-height: 100%;
  margin: 0;
  background: var(--console-bg);
}

html { color-scheme: light; }
html[data-theme="dark"] { color-scheme: dark; }

.console-app {
  min-height: 100vh;
  font-family: var(--console-font);
  color: var(--console-ink);
  font-size: 15px;
  line-height: 1.5;
  -webkit-font-smoothing: antialiased;
  transition: background-color 180ms ease, color 180ms ease;
}

*, *::before, *::after { box-sizing: border-box; }
button, input, select, textarea { font: inherit; }
button { -webkit-tap-highlight-color: transparent; }
button:not(:disabled) { transition: transform 140ms ease-out, background-color 160ms ease, color 160ms ease, border-color 160ms ease, opacity 160ms ease; }
button:not(:disabled):active { transform: scale(.97); }
.button { letter-spacing: 0 !important; }

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: .01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: .01ms !important;
    scroll-behavior: auto !important;
  }
}
</style>
