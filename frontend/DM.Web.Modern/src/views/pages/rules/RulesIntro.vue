<script setup lang="ts">
/**
 * RulesIntro - статическое приветствие и TL;DR правил
 */
</script>

<template>
  <section class="rules-intro">
    <p>
      Привет! Мы рады видеть тебя на DM — одном из крупнейших русскоязычных
      ролевых сайтов. Это большое и дружелюбное сообщество любителей текстовых
      ролевых игр, существующее с 2007-го года.
    </p>

    <p>
      Здесь люди находят игры, чтобы творить и веселиться вместе с
      единомышленниками, заводят новые знакомства и даже друзей. Если ты еще не
      знаешь, что такое "текстовые ролевые игры" — загляни на страницу
      <router-link to="/about" class="highlight-link">"О проекте"</router-link>.
    </p>

    <p>
      Наши правила созданы для того, чтобы всем было комфортно. Мы постарались
      написать их максимально коротко и понятно.
    </p>

    <h4>TL;DR</h4>
    <ol>
      <li>Не оскорбляй других людей</li>
      <li>
        Мат и контент 18+ оборачивай в тег <span class="tag">[nsfw]</span>
      </li>
      <li>Не обсуждай политику нигде и никак</li>
      <li>Один человек — один аккаунт</li>
      <li>Не спамь и не флуди</li>
    </ol>

    <p class="outro">
      Если тебе что-то непонятно в правилах — обратись к кому-нибудь из
      Администрации, мы всегда поможем разобраться.
    </p>
  </section>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.rules-intro
  margin: $medium 0

  p
    margin: $medium 0
    line-height: 1.6

  h4
    margin: $medium 0 $small
    font-weight: 600

  ol
    margin: $small 0
    padding-left: $big

    li
      margin: $minor 0

  .tag
    font-family: monospace
    background: $bg-element-accent
    padding: 3px 2px
    border-radius: 2px
    font-size: 0.9em

  .highlight-link
    color: $link
    text-decoration: none
    font-weight: bold
    &:hover
      color: $link-hover
      text-decoration: underline

  .outro
    color: $text
</style>
