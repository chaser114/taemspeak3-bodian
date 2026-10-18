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
      const prefersDark = window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
      const theme = query === "dark" || query === "light" ? query : (saved === "dark" || saved === "light" ? saved : (prefersDark ? "dark" : "light"));
      document.documentElement.setAttribute("data-theme", theme);
    },
  },
});
</script>

<style lang="less">
:root {
  /* Apple Music inspired palette: black / white / red. */
  --console-bg: #ffffff;
  --console-surface: #ffffff;
  --console-surface-2: #f5f5f7;
  --console-surface-3: #ececee;
  --console-side: #f5f5f7;
  --console-ink: #1d1d1f;
  --console-muted: #6e6e73;
  --console-muted-2: #8e8e93;
  --console-line: rgba(0, 0, 0, .09);
  --console-line-strong: rgba(0, 0, 0, .18);
  --console-hover: rgba(0, 0, 0, .045);
  --console-fill: rgba(0, 0, 0, .06);
  --console-fill-2: rgba(0, 0, 0, .1);
  --console-brand: #fa233b;
  --console-brand-dark: #d91e33;
  --console-brand-soft: rgba(250, 35, 59, .1);
  --console-danger: #d91e33;
  --console-danger-soft: rgba(250, 35, 59, .1);
  --console-warn: #9a6500;
  --console-warn-soft: rgba(235, 173, 0, .14);
  --console-ok: #30a94c;
  --console-ok-soft: rgba(48, 209, 88, .14);
  --console-canvas: var(--console-bg);
  --console-glass: rgba(255, 255, 255, .82);
  --console-font: -apple-system, BlinkMacSystemFont, "SF Pro Text", "Segoe UI Variable Display", "Segoe UI", "PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", Roboto, sans-serif;
  --console-radius-xs: 7px;
  --console-radius-sm: 10px;
  --console-radius: 14px;
  --console-radius-lg: 20px;
  --console-radius-full: 999px;
  --console-shadow-sm: 0 1px 3px rgba(0, 0, 0, .08);
  --console-shadow: 0 4px 16px rgba(0, 0, 0, .08);
  --console-shadow-md: 0 18px 48px rgba(0, 0, 0, .16);
  --console-ease-out: cubic-bezier(.23, 1, .32, 1);
  --console-ease-drawer: cubic-bezier(.32, .72, 0, 1);
  --console-player-h: 90px;
  --console-nav-h: 62px;
  --console-side-w: 232px;
  --console-panel-w: 300px;
  --console-header-h: 64px;
}

html[data-theme="dark"] {
  --console-bg: #000000;
  --console-surface: #1c1c1e;
  --console-surface-2: #1c1c1e;
  --console-surface-3: #2c2c2e;
  --console-side: #1c1c1e;
  --console-ink: #f5f5f7;
  --console-muted: #98989d;
  --console-muted-2: #6e6e73;
  --console-line: rgba(255, 255, 255, .11);
  --console-line-strong: rgba(255, 255, 255, .22);
  --console-hover: rgba(255, 255, 255, .07);
  --console-fill: rgba(255, 255, 255, .1);
  --console-fill-2: rgba(255, 255, 255, .17);
  --console-brand-soft: rgba(250, 35, 59, .18);
  --console-danger-soft: rgba(250, 35, 59, .18);
  --console-warn-soft: rgba(235, 173, 0, .18);
  --console-ok-soft: rgba(48, 209, 88, .18);
  --console-glass: rgba(0, 0, 0, .76);
  --console-shadow-sm: 0 1px 3px rgba(0, 0, 0, .5);
  --console-shadow: 0 4px 20px rgba(0, 0, 0, .55);
  --console-shadow-md: 0 18px 48px rgba(0, 0, 0, .7);
}

* { box-sizing: border-box; }

html, body {
  margin: 0;
  padding: 0;
  background: var(--console-bg);
  color: var(--console-ink);
  font-family: var(--console-font);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

body { min-height: 100vh; }

button { font-family: inherit; color: inherit; }
input, select, textarea { font-family: inherit; }

a { color: var(--console-brand); }

::-webkit-scrollbar { width: 10px; height: 10px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { border: 3px solid transparent; border-radius: 999px; background-clip: content-box; background-color: var(--console-line-strong); }
::-webkit-scrollbar-thumb:hover { background-color: var(--console-muted-2); }

.console-app { min-height: 100vh; background: var(--console-bg); }

/* Shared primitives used across console pages. */
.bd-pill {
  height: 26px; padding: 0 12px; border: 0; border-radius: 999px;
  display: inline-flex; align-items: center; gap: 5px;
  background: var(--console-fill); color: var(--console-ink);
  font-size: 12px; font-weight: 600; cursor: pointer; white-space: nowrap;
}
.bd-pill:hover:not(:disabled) { background: var(--console-fill-2); }
.bd-pill:disabled { opacity: .45; cursor: not-allowed; }
.bd-pill.solid { background: var(--console-brand); color: #fff; }
.bd-pill.solid:hover:not(:disabled) { background: var(--console-brand-dark); }
.bd-pill.danger { color: var(--console-brand); }

.bd-icon-button {
  width: 30px; height: 30px; display: grid; place-items: center;
  border: 0; border-radius: 7px; background: transparent;
  color: var(--console-muted); cursor: pointer;
}
.bd-icon-button:hover:not(:disabled) { background: var(--console-hover); color: var(--console-ink); }
.bd-icon-button:disabled { opacity: .4; cursor: not-allowed; }

.bd-empty-state {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  gap: 10px; padding: 80px 20px; text-align: center; color: var(--console-muted);
}
.bd-empty-state h1 { margin: 0; font-size: 20px; color: var(--console-ink); }
.bd-empty-state p { margin: 0; font-size: 13px; }

.bd-inline-error { margin: 10px 0 0; color: var(--console-danger); font-size: 13px; }
.bd-inline-success { margin: 10px 0 0; color: var(--console-ok); font-size: 13px; }

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation-duration: .001ms !important; animation-iteration-count: 1 !important; transition-duration: .001ms !important; }
}
</style>
