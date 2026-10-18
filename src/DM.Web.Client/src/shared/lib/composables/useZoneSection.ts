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
  announce(inject(ZONE_SECTION, null), section);
}

const ZONE_MARKER = Symbol("zone-marker") as InjectionKey<
  Ref<string | undefined>
>;

/**
 * Where a zone shell keeps a muted word it draws after the heading, such as
 * "архив" for an archived room. A marker and not part of the section: the tab
 * title leaves it out, and the heading draws it in its own quiet style.
 */
export function provideZoneMarker(): Ref<string | undefined> {
  const marker = ref<string | undefined>(undefined);
  provide(ZONE_MARKER, marker);
  return marker;
}

/** Announces the marker of the page; cleared when the page leaves. */
export function useZoneMarker(
  marker: MaybeRefOrGetter<string | undefined>,
): void {
  announce(inject(ZONE_MARKER, null), marker);
}

function announce(
  shared: Ref<string | undefined> | null,
  value: MaybeRefOrGetter<string | undefined>,
): void {
  if (!shared) return;

  watchEffect((onCleanup) => {
    shared.value = toValue(value);
    onCleanup(() => {
      shared.value = undefined;
    });
  });
}
