/**
 * Minimal typed shape of ExpandableList's `defineExpose({ expandItem })`.
 *
 * `ExpandableList` is generic (`<script setup generic="T">`), so a template
 * ref typed via `InstanceType<typeof ExpandableList>` fails vue-tsc (a
 * generic SFC component type doesn't satisfy the `new (...) => any`
 * constraint `InstanceType` needs). Since callers here only need
 * `expandItem`, a narrow structural type sidesteps that entirely.
 */
export interface ExpandableListExpose {
  expandItem: (id: string) => void;
}
