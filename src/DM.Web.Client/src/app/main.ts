import dayjs from "dayjs";
import relativeTime from "dayjs/plugin/relativeTime";
import "dayjs/locale/ru";
import { createApp } from "vue";
import { createPinia } from "pinia";
import { createVfm } from "vue-final-modal";

import App from "./App.vue";
import "vue-final-modal/style.css";
import { registerGlobalComponents } from "./providers/components";
import router from "./providers/router";
import { installRoutePrefetch } from "./providers/routePrefetch";
import { installSessionExpiredHandler } from "./providers/sessionExpiry";
import { installPreloadErrorReload } from "./providers/staleBundle";

import "@/assets/styles/ThemeVariables.css";
import "@/assets/styles/Reset.sass";
import "@/assets/styles/Fonts.sass";
import "@/assets/styles/InputsGlobal.sass";
import "@/assets/styles/AuthSurface.sass";
import "@/assets/styles/BbcodeGlobal.sass";

dayjs.extend(relativeTime).locale("ru");

// The HTTP client in shared reports an expired session; the app decides what it costs.
installSessionExpiredHandler();

const application = createApp(App);

registerGlobalComponents(application);

application.use(createPinia()).use(router).use(createVfm());

// Starts loading a page's chunk when the reader aims at its link.
installRoutePrefetch();

// A tab open across a deploy asks for chunks that are gone; it reloads once.
installPreloadErrorReload();

application.mount("#application");
