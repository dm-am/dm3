import type { App } from "vue";

import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

/**
 * Only shared primitives: a global component has no import for the layer rules
 * to check, and shared sits below every layer (fsdBoundaries.spec.ts). The
 * vue/no-undef-components whitelist is read out of this file.
 */
export function registerGlobalComponents(app: App) {
  app
    .component("PageTitle", PageTitle)
    .component("BlockTitle", BlockTitle)
    .component("SecondaryText", SecondaryText)
    .component("Form", Form)
    .component("FormField", FormField)
    .component("Button", Button)
    .component("Dialog", Dialog);
}

// What vue-tsc checks a template's use of each of them against, as if the
// template had imported it; fsdBoundaries.spec.ts holds this list to the one
// above.
declare module "vue" {
  export interface GlobalComponents {
    PageTitle: typeof PageTitle;
    BlockTitle: typeof BlockTitle;
    SecondaryText: typeof SecondaryText;
    Form: typeof Form;
    FormField: typeof FormField;
    Button: typeof Button;
    Dialog: typeof Dialog;
  }
}
