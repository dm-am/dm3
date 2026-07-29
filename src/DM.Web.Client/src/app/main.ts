import { createApp } from "vue";

import App from "./App.vue";
import {
  pinia,
  router,
  installPlugins,
  registerGlobalComponents,
  setupDayjs,
} from "./providers";

// Setup dayjs
setupDayjs();

// Import global styles
import "@/assets/styles/ThemeVariables.css";
import "@/assets/styles/Reset.sass";
import "@/assets/styles/Fonts.sass";
import "@/assets/styles/InputsGlobal.sass";
import "@/assets/styles/BbcodeGlobal.sass";

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
