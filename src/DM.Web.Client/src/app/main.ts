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
import "./styles/ThemeVariables.css";
import "./styles/Reset.sass";
import "./styles/Fonts.sass";
import "./styles/Inputs.sass";
import "./styles/BbcodeGlobal.sass";

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
