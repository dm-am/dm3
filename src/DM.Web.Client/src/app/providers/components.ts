import type { App } from "vue";

import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SidebarTitle from "@/shared/ui/Layout/SidebarTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import HumanTimespan from "@/shared/ui/Date/HumanTimespan.vue";
import Lightbox from "@/shared/ui/Layout/Lightbox.vue";
import Button from "@/shared/ui/Button/Button.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { UserLink } from "@/entities/user";

export function registerGlobalComponents(app: App) {
  // Register global components
  app
    .component("PageTitle", PageTitle)
    .component("BlockTitle", BlockTitle)
    .component("SidebarTitle", SidebarTitle)
    .component("SecondaryText", SecondaryText)
    .component("Form", Form)
    .component("FormField", FormField)
    .component("Button", Button)
    .component("Lightbox", Lightbox)
    .component("HumanDate", HumanDate)
    .component("HumanTimespan", HumanTimespan)
    .component("UserLink", UserLink);
}
