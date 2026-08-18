<script setup lang="ts">
// The overview tab. Gated like its siblings: the tab strip above it filters
// itself by role, but the address does not, and an authenticated non-moderator
// opening /moderation directly must read a refusal, not an invitation to pick
// a section none of which would open.
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(sectionRole("moderation"));
</script>

<template>
  <secondary-text v-if="!hasAccess">{{ deniedText }}</secondary-text>
  <secondary-text v-else>Выберите раздел модерации</secondary-text>
</template>
