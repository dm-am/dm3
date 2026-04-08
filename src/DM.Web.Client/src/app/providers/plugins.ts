import { createVfm } from "vue-final-modal";
import Toast, { type PluginOptions, POSITION } from "vue-toastification";
import { createI18n } from "vue-i18n";
import type { App } from "vue";

import "vue-final-modal/style.css";
import "vue-toastification/dist/index.css";

export const vfm = createVfm();

export const i18n = createI18n({
  locale: "ru",
  legacy: false, // Use Composition API mode - prevents __disposer errors during rapid unmount
});

export const toastOptions: PluginOptions = {
  position: POSITION.TOP_RIGHT,
  timeout: 5000,
};

export function installPlugins(app: App) {
  app.use(vfm);
  app.use(i18n);
  app.use(Toast, toastOptions);
}
