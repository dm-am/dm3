<script setup lang="ts">
/**
 * ProfileSubpageHeader — the heading of a profile subpage (received
 * reviews / given reviews / received endorsements / given
 * endorsements).
 *
 * Unifies the visual unit: a PageTitle with the "{label}: {username}" format
 * (like the profile H1 "Личный кабинет: {username}") + a short description with
 * a link back to the profile. The description is provided via a slot and rendered through
 * LeadText so the profile router-link can be embedded inside the phrase itself
 * as a bold link (a unified look with games/blogs and other intros).
 */
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
