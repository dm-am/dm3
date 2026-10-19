import {
  SubscriptionSettings,
  SubscriptionTargetType,
  type Subscription,
} from "@/shared/api/models/subscriptions";

// The server writes the enums by name and the client computes with numbers, so the names
// are read into numbers at this one door; what the client sends goes out as numbers.
export type WireSubscription = Omit<Subscription, "targetType" | "settings"> & {
  targetType: string | number;
  settings: string | number;
};

/** "Game" as SubscriptionTargetType.Game, null for a type the client does not know. */
export function targetTypeFromWire(
  value: string | number,
): SubscriptionTargetType | null {
  if (typeof value === "number") return value;
  const named =
    SubscriptionTargetType[value as keyof typeof SubscriptionTargetType];
  return typeof named === "number" ? named : null;
}

/** "InApp, AuthorGameEvents" as InApp | AuthorGameEvents. */
export function settingsFromWire(value: string | number): SubscriptionSettings {
  if (typeof value === "number") return value;
  let bits = 0;
  for (const part of value.split(",")) {
    const name = part.trim();
    const named =
      SubscriptionSettings[name as keyof typeof SubscriptionSettings];
    if (typeof named === "number") bits |= named;
    // A bit without a name is written as its number.
    else if (/^\d+$/.test(name)) bits |= Number(name);
  }
  return bits;
}

/** Null for a target of a type the client does not know: no page has a place for it. */
export function subscriptionFromWire(
  wire: WireSubscription,
): Subscription | null {
  const targetType = targetTypeFromWire(wire.targetType);
  return targetType === null
    ? null
    : { ...wire, targetType, settings: settingsFromWire(wire.settings) };
}
