import { createVfm } from "vue-final-modal";
import Toast, { type PluginOptions, POSITION } from "vue-toastification";
import type { App } from "vue";

import "vue-final-modal/style.css";
import "vue-toastification/dist/index.css";

// vue-i18n is deliberately absent. It used to be installed here with an empty
// message catalogue and never called once — no $t, no useI18n, nothing — while
// every string in the app is written in Russian inline. The product is
// Russian-only and no second language is planned anywhere in docs/, so the
// plugin was cost without a purpose. If a second language ever appears, it
// starts with a decision about the catalogue, not with reinstating a plugin.

export const vfm = createVfm();

export const toastOptions: PluginOptions = {
  position: POSITION.TOP_RIGHT,
  timeout: 5000,
};

export function installPlugins(app: App) {
  app.use(vfm);
  app.use(Toast, toastOptions);
}
