<script setup lang="ts">
/**
 * The name in the heading of a profile, the earlier names behind "[...]", and
 * in edit mode the way to another name: it is asked for in the account, which
 * keeps the form and the fate of the request, so the profile only leads there.
 */
import { computed } from "vue";
import type { User } from "@/shared/api/models/community";
import { formatDate } from "@/shared/lib/utils/datetime";
import { PageTitle } from "@/shared/ui/Layout";
import { Tooltip } from "@/shared/ui/Tooltip";

const props = defineProps<{
  /** The profile, whose name and earlier names the heading reads. */
  user: User;
  /** The owner edits the profile: the way to another name stands under the heading. */
  editing: boolean;
  /** A request of the owner's for another name waits for a moderator. */
  nameChangePending: boolean;
}>();

const history = computed(() => props.user.usernameHistory ?? []);

/** The section of the account that sends the request and tells its fate. */
const USERNAME_CHANGE = { name: "account", hash: "#username-change" } as const;
</script>

<template>
  <PageTitle>
    Профиль: {{ user.username }}{{ " "
    }}<Tooltip v-if="history.length" placement="bottom" focusable>
      <template #content>
        <div class="history">
          <div class="history-title">Прошлые имена:</div>
          <div v-for="entry in history" :key="entry.id" class="history-entry">
            <span class="history-old">{{ entry.oldUsername }}</span>
            <span class="history-when">{{ formatDate(entry.changedUtc) }}</span>
          </div>
        </div>
      </template>
      <span class="history-hint" aria-label="Прошлые имена">[...]</span>
    </Tooltip>
  </PageTitle>

  <template v-if="editing">
    <p v-if="nameChangePending" class="change-name-pending">
      Заявка на смену имени уже на рассмотрении
    </p>
    <router-link v-else :to="USERNAME_CHANGE" class="change-name-link"
      >Сменить имя пользователя</router-link
    >
  </template>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.history-hint
  color: $text-muted
  cursor: help
  font-weight: normal

.history
  display: flex
  flex-direction: column
  gap: $tiny
  text-align: left

.history-title
  margin-bottom: $tiny

.history-entry
  display: flex
  justify-content: space-between
  gap: $medium

.change-name-link
  align-self: flex-start
  font-size: $font-size
  +link-tone($link)

.change-name-pending
  margin: 0
  font-size: $font-size
  color: $text-muted
</style>
