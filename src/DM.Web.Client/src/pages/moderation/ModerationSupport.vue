<script setup lang="ts">
/**
 * ModerationSupport — "Поддержка" (doc 4.2.3.8.7). Support-desk tickets:
 * "Ошибка", "Восстановление доступа", "Проблемы с регистрацией".
 * Admin scope — these subtypes are visible to administrators only
 * (server-side visibility scoping in TicketService).
 */
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import TicketList from "./tickets/TicketList.vue";
import { SUPPORT_SUBTYPES } from "./lib/labels";
import { useRoleGate } from "./lib/useRoleGate";

const { hasAccess, deniedText } = useRoleGate("Admin");
</script>

<template>
  <div class="moderation-support">
    <page-title>Поддержка</page-title>

    <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

    <TicketList
      v-else
      :subtypes="SUPPORT_SUBTYPES"
      empty-text="Обращений пока нет"
    />
  </div>
</template>
