import { createApp } from "vue";

import App from "./App.vue";
import {
  pinia,
  router,
  installPlugins,
  installRoutePrefetch,
  installSessionExpiredHandler,
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

// The HTTP client detects an expired session; dropping the viewer and deciding
// where that takes them is the app's business, so the app hands the client a
// handler instead of the client reaching upwards for the store and the router.
installSessionExpiredHandler();

// Create app
const application = createApp(App);

// Register global components
registerGlobalComponents(application);

// Install plugins
application.use(pinia);
application.use(router);
installPlugins(application);

// Every page is a lazy chunk, so a click on a link costs a round trip before
// anything renders. This listens for the reader aiming at one — hover, focus or
// touch — and starts that request early. It draws nothing; the teardown it
// returns is dropped because the listeners live as long as the document does.
installRoutePrefetch();

// Mount app
application.mount("#application");
