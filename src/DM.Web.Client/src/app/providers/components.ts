import type { App } from "vue";

import { IconType } from "@/shared/ui/Icon/iconType";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SidebarTitle from "@/shared/ui/Layout/SidebarTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import HumanTimespan from "@/shared/ui/Date/HumanTimespan.vue";
import TheIcon from "@/shared/ui/Icon/TheIcon.vue";
import TheLightbox from "@/shared/ui/Layout/TheLightbox.vue";
import TheButton from "@/shared/ui/Button/TheButton.vue";
import TheForm from "@/shared/ui/Form/TheForm.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { UserLink } from "@/entities/user";

export function registerGlobalComponents(app: App) {
  // Make IconType available globally
  app.config.globalProperties.IconType = IconType;

  // Register global components
  app
    .component("TheIcon", TheIcon)
    .component("PageTitle", PageTitle)
    .component("BlockTitle", BlockTitle)
    .component("SidebarTitle", SidebarTitle)
    .component("SecondaryText", SecondaryText)
    .component("TheForm", TheForm)
    .component("FormField", FormField)
    .component("TheButton", TheButton)
    .component("TheLightbox", TheLightbox)
    .component("HumanDate", HumanDate)
    .component("HumanTimespan", HumanTimespan)
    .component("UserLink", UserLink);
}
