import { createVfm } from "vue-final-modal";
import type { App } from "vue";

import "vue-final-modal/style.css";

// vue-i18n is deliberately absent. It used to be installed here with an empty
// message catalogue and never called once — no $t, no useI18n, nothing — while
// every string in the app is written in Russian inline. The product is
// Russian-only and no second language is planned anywhere in docs/, so the
// plugin was cost without a purpose. If a second language ever appears, it
// starts with a decision about the catalogue, not with reinstating a plugin.

// vue-toastification is deliberately absent for the same reason. It was
// installed here with its stylesheet, in the entry chunk, while every toast on
// the site goes through shared/lib/composables/useToast and shared/ui/Toast,
// and not one call ever reached the plugin.

export const vfm = createVfm();

export function installPlugins(app: App) {
  app.use(vfm);
}
