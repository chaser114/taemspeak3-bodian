<template><div id="app" class="console-app"><ConsoleShell v-if="$route.path !== '/'"><router-view/></ConsoleShell><router-view v-else/></div></template>
<script lang="ts">import ConsoleShell from "./Components/ConsoleShell.vue"; export default { components: { ConsoleShell } };</script>
<style lang="less">
:root {
	--console-brand: #4fb8a8;
	--console-brand-dark: #257e73;
	--console-ink: #1e2733;
	--console-muted: #788595;
	--console-line: #e5e9ee;
	--console-surface: #fff;
	--console-canvas: #f6f8fa;
	--console-danger: #d65c63;
}
html, body, #app { min-height: 100%; margin: 0; background: var(--console-canvas); }
.console-app { min-height: 100vh; font-family: "Microsoft YaHei", Arial, sans-serif; color: var(--console-ink); }
* { box-sizing: border-box; }
.button { letter-spacing: 0 !important; }
@media (max-width: 760px) { .mobile-nav > button { display: none !important; } }
</style>
