import { inject, provide, ref, toValue, watchEffect } from "vue";
import type { InjectionKey, MaybeRefOrGetter, Ref } from "vue";

const ZONE_SECTION = Symbol("zone-section") as InjectionKey<
  Ref<string | undefined>
>;

/**
 * Where a zone shell keeps the name of the section a sub-page is showing.
 *
 * The heading of a zone reads "{entity} | {section}" and the browser tab says
 * the same sentence, which is only true while one place composes both. The
 * shell knows the entity and reads a static section off `route.meta.section`;
 * what it cannot know is a section that is data — the name of a character, or
 * whether a new sheet is an NPC. Those pages announce it here.
 *
 * Before this the sub-page drew a second `h1` of its own and set the tab title
 * on its own, so the page carried two headings, the second one repeating a word
 * the first did not say, and the tab said a third thing.
 */
export function provideZoneSection(): Ref<string | undefined> {
  const section = ref<string | undefined>(undefined);
  provide(ZONE_SECTION, section);
  return section;
}

/**
 * Announces the section of the page inside its zone shell. Reactive: the value
 * is read again whenever its source changes, and cleared when the page leaves,
 * so the next sub-page cannot inherit it.
 */
export function useZoneSection(
  section: MaybeRefOrGetter<string | undefined>,
): void {
  const shared = inject(ZONE_SECTION, null);
  if (!shared) return;

  watchEffect((onCleanup) => {
    shared.value = toValue(section);
    onCleanup(() => {
      shared.value = undefined;
    });
  });
}
