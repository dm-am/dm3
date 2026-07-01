<script setup lang="ts">
/**
 * ProfileSubpageHeader — заголовок профильной подстраницы (полученные
 * оценки / поставленные оценки / полученные рекомендации / написанные
 * рекомендации).
 *
 * Унифицирует визуальную единицу: PageTitle с форматом «{label}: {username}»
 * (как H1 профиля «Личный кабинет: {username}») + краткое описание со
 * ссылкой обратно на профиль. Описание задается слотом и рендерится через
 * LeadText, чтобы вписать router-link на профиль внутрь самой фразы
 * жирной ссылкой (единый look с games/blogs и прочими интро).
 */
import { PageTitle, LeadText } from "@/shared/ui/Layout";

defineProps<{
  /** Часть заголовка до двоеточия, напр. «Полученные оценки». */
  label: string;
  /** Имя пользователя — рендерится после двоеточия, регистр сохраняется. */
  username: string;
}>();
</script>

<template>
  <div>
    <PageTitle>
      {{ label }}:
      <span class="username-preserve-case">{{ username }}</span>
    </PageTitle>
    <LeadText v-if="$slots.default">
      <slot />
    </LeadText>
  </div>
</template>
