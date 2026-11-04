import { inject, provide, ref, toValue, watchEffect } from "vue";
import type { InjectionKey, MaybeRefOrGetter, Ref } from "vue";

const ZONE_SECTION = Symbol("zone-section") as InjectionKey<
  Ref<string | undefined>
>;

/**
 * The section a sub-page announces to its zone shell when the section is data
 * (a character's name, an NPC sheet): the shell composes the heading and the
 * tab "{entity} | {section}" alone.
 */
export function provideZoneSection(): Ref<string | undefined> {
  const section = ref<string | undefined>(undefined);
  provide(ZONE_SECTION, section);
  return section;
}

/** Reactive, and cleared when the page leaves. */
export function useZoneSection(
  section: MaybeRefOrGetter<string | undefined>,
): void {
  announce(inject(ZONE_SECTION, null), section);
}

const ZONE_MARKER = Symbol("zone-marker") as InjectionKey<
  Ref<string | undefined>
>;

/** A muted word after the heading ("архив"), left out of the tab title. */
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
