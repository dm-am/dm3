<script setup lang="ts">
/**
 * SupportPage - support form (bug reports, questions, lost access).
 *
 * Hosts the real submission form (SupportTicketForm); guests are allowed
 * to submit. When arriving with ?reason=access (e.g. from a "lost access"
 * link elsewhere on the site) it leads with the account-recovery action
 * before the form.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { SvgIcon } from "@/shared/ui/Icon";
import { DISCORD_INVITE_URL } from "@/shared/config/contacts";
import { SupportTicketForm } from "@/features/support-ticket";
import { useAuthStore } from "@/entities/user";

const route = useRoute();
const { user } = storeToRefs(useAuthStore());

// The ?action=recovery CTA below is consumed by the guest-only GuestActions
// widget — for an already authenticated user it would be a silent no-op, so
// the branch only leads with account recovery when there is no active user.
const isAccessRecovery = computed(
  () => route.query.reason === "access" && !user.value,
);
// Authenticated users can still land here with ?reason=access (e.g. a stale
// link) — access is not actually lost, so point them at account settings
// instead of the guest-only recovery flow.
const isAccessRecoveryAuthenticated = computed(
  () => route.query.reason === "access" && !!user.value,
);
</script>

<template>
  <page-title>Поддержка</page-title>

  <template v-if="isAccessRecovery">
    <LeadText
      >Доступ можно восстановить прямо сейчас, а если не получится, напишите нам
      через форму ниже</LeadText
    >
    <div class="recovery-card">
      <router-link
        :to="{ query: { ...route.query, action: 'recovery' } }"
        class="recovery-link"
      >
        Перейти к восстановлению доступа
      </router-link>
    </div>
    <DashSeparator spacing="small" />
  </template>

  <LeadText v-else-if="isAccessRecoveryAuthenticated">
    Вы уже авторизованы, поэтому доступ к аккаунту не потерян: изменить пароль
    или другие данные можно в
    <router-link to="/account">настройках аккаунта</router-link>
  </LeadText>

  <LeadText v-else
    >Если нашли ошибку или не можете разобраться, опишите проблему, и мы
    поможем</LeadText
  >

  <SupportTicketForm kind="support" />

  <p class="discord-fallback">
    <SvgIcon name="discord" class="discord-icon" />
    Если удобнее, напишите нам в
    <a :href="DISCORD_INVITE_URL" target="_blank" rel="noopener noreferrer"
      >Discord</a
    >
  </p>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Variables" as *
@use "@/assets/styles/Themes" as *

.recovery-card
  display: flex
  flex-direction: column
  gap: $small
  padding: $medium
  margin: $medium 0
  background-color: $bg-element
  border: 1px dashed $border

.recovery-link
  color: $link
  font-weight: 700
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

// Inline flow, not flex: a flex item is blockified, so the note copied as
// "Если удобнее, напишите нам в\nDiscord". The gap the flex drew is already a
// real space in the markup, right after the icon.
.discord-fallback
  margin-top: $medium
  color: $text-muted
  font-size: $secondary-font-size

  a
    color: $link

    &:hover
      color: $link-hover

.discord-icon
  width: 16px
  height: 16px
  vertical-align: text-bottom
</style>
