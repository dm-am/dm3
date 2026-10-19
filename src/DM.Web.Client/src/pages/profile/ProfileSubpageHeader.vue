<script setup lang="ts">
/** The heading of a profile subpage, "{label}: {username}", and a lead phrase with a link to the profile. */
import { PageTitle, LeadText } from "@/shared/ui/Layout";

defineProps<{
  /** The heading part before the colon, e.g. "Полученные оценки". */
  label: string;
  /** Username — rendered after the colon. */
  username: string;
}>();
</script>

<template>
  <div>
    <PageTitle> {{ label }}: {{ username }} </PageTitle>
    <LeadText v-if="$slots.default">
      <slot />
    </LeadText>
  </div>
</template>
