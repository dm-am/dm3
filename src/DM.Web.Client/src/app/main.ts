import { createApp } from "vue";

import App from "./App.vue";
import {
  pinia,
  router,
  installPlugins,
  registerGlobalComponents,
  setupDayjs,
} from "./providers";
import { setSessionExpiredHandler } from "@/shared/api";

// Setup dayjs
setupDayjs();

// Import global styles
import "@/assets/styles/ThemeVariables.css";
import "@/assets/styles/Reset.sass";
import "@/assets/styles/Fonts.sass";
import "@/assets/styles/InputsGlobal.sass";
import "@/assets/styles/BbcodeGlobal.sass";

// The HTTP client detects an expired session; navigation is the app's business,
// so the app hands it the destination instead of the client reaching upwards for
// the router.
setSessionExpiredHandler(() => {
  void router.push({ name: "home" });
});

// Create app
const application = createApp(App);

// Register global components
registerGlobalComponents(application);

// Install plugins
application.use(pinia);
application.use(router);
installPlugins(application);

// Mount app
application.mount("#application");
