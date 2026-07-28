import Vue from "vue";
import VueRouter from "vue-router";
import Buefy from "buefy";
import "@mdi/font/css/materialdesignicons.css";
import "buefy/dist/buefy.css";
import App from "./App.vue";
import Home from "./Pages/Home.vue";
import ConsoleOverview from "./Pages/ConsoleOverview.vue";
import ConsoleAdmin from "./Pages/ConsoleAdmin.vue";

Vue.use(VueRouter);
Vue.use(Buefy);

const router = new VueRouter({
	mode: "history",
	routes: [
		{ path: "/", component: Home },
		{ path: "/music", component: ConsoleOverview },
		{ path: "/recent", component: ConsoleOverview, props: { recentOnly: true } },
		{ path: "/admin", component: ConsoleAdmin },
		{ path: "*", redirect: "/" },
	]
});

export default new Vue({ el: "#app", template: "<App/>", components: { App }, router });
