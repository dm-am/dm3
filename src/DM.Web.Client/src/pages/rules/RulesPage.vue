<script setup lang="ts">
/**
 * RulesPage - страница правил сайта
 *
 * Все секции статические (без загрузки из БД).
 * Редактирование — через код.
 */

import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import RulesIntro from "./RulesIntro.vue";
import HelpLinksSection from "./HelpLinksSection.vue";
import PenaltyTable from "./PenaltyTable.vue";
import RulesExternalLinks from "./RulesExternalLinks.vue";
import RulesBans from "./RulesBans.vue";
import RulesMasters from "./RulesMasters.vue";
import AdminList from "./AdminList.vue";
</script>

<template>
  <page-title>Правила</page-title>

  <!-- Приветствие и TL;DR -->
  <RulesIntro />

  <!-- Куда обращаться -->
  <HelpLinksSection />

  <!-- Таблица штрафов с раскрывающимися деталями -->
  <section class="rules-section">
    <BlockTitle>Нарушения и баллы</BlockTitle>
    <PenaltyTable />
  </section>

  <!-- Ссылки на сторонние ресурсы -->
  <RulesExternalLinks />

  <!-- Как работают баны -->
  <RulesBans />

  <!-- Для мастеров -->
  <RulesMasters />

  <!-- Администрация -->
  <AdminList />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.rules-section
  margin: $big 0
</style>
