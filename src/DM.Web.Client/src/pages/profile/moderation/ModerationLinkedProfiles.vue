<script setup lang="ts">
/**
 * "Связанные профили", a section of the profile's "Модерация" tab: the
 * accounts that signed in from an address this one used, as a list the way the
 * site draws its lists ("- " and the name) with what ties them in brackets.
 */
import type { LinkedProfile } from "@/shared/api/models/moderation";
import { BlockTitle, SecondaryText } from "@/shared/ui/Layout";
import { formatDate } from "@/shared/lib/utils/datetime";
import { pluralize } from "@/shared/lib/utils/pluralize";

defineProps<{
  profiles: LinkedProfile[];
}>();

/** "1 общий IP" / "3 общих IP". */
function sharedIpsLabel(count: number): string {
  return `${count} ${pluralize(count, "общий", "общих", "общих")} IP`;
}
</script>

<template>
  <section class="mod-section">
    <BlockTitle>Связанные профили</BlockTitle>

    <ul v-if="profiles.length" class="mod-linked-list">
      <li v-for="p in profiles" :key="p.userId">
        <span class="mod-linked-dash" aria-hidden="true">- </span
        ><router-link
          :to="{ name: 'profile', params: { username: p.username } }"
          >{{ p.username }}</router-link
        >{{ " "
        }}<span class="mod-linked-why"
          >({{ sharedIpsLabel(p.sharedIpsCount) }}, последний
          {{ formatDate(p.lastSharedLoginUtc) }})</span
        >
      </li>
    </ul>

    <SecondaryText v-else>Нет связанных профилей</SecondaryText>
  </section>
</template>

<style scoped lang="sass">
.mod-linked-list
  list-style: none
  margin: 0
  padding: 0
  line-height: 1.5

.mod-linked-dash,
.mod-linked-why
  color: $text-muted

.mod-linked-why
  font-size: $secondary-font-size
</style>
