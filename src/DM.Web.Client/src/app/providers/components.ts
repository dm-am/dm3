import type { App } from "vue";

import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

/**
 * Only `shared` primitives may be registered globally. A global component is
 * usable in any template without an import, so `boundaries/dependencies` has no
 * edge to refuse and `vue/no-undef-components` skips the name outright — its
 * whitelist is read out of this very file. A component of a sliced layer
 * registered here would therefore let any slice reach another one past its `@x`
 * door with both gates green; `shared` sits below every layer, so a hidden edge
 * to it breaks no rule. Asserted by fsdBoundaries.spec.ts.
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
