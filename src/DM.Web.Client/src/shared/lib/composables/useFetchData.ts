import {
  type RouteParams,
  type LocationQuery,
  type LocationQueryValue,
  useRoute,
} from "vue-router";
import { watch, onMounted } from "vue";

type ParamValue = string | string[] | undefined;
type QueryValue = LocationQueryValue | LocationQueryValue[];

type ParamStrategy = {
  param: (params: RouteParams) => ParamValue;
  callback: (param: ParamValue) => void;
};

type QueryStrategy = {
  query: (query: LocationQuery) => QueryValue;
  callback: (value: QueryValue) => void;
};

/**
 * Runs `mountHook` on mount and re-runs a strategy's callback when the piece of
 * the route it watches changes.
 *
 * **Contract: one route change fires at most one callback per watcher.** A
 * navigation that changes several watched values (a link to another rubric from
 * page 3 changes both `rubric` and `number`) must produce one reload, not one
 * per changed value — hence the `break`. The consequence is a rule on callers:
 * every callback within the same watcher has to be interchangeable and must
 * re-read what it needs from the route itself, never from the argument it was
 * handed. All current call sites already do exactly that.
 *
 * Param and query watchers are independent, so a change to both fires both.
 */
export function useFetchData(
  mountHook: () => any,
  paramStrategies: ParamStrategy[] = [],
  queryStrategies: QueryStrategy[] = [],
) {
  onMounted(mountHook);

  const route = useRoute();

  // Watch route params
  if (paramStrategies.length > 0) {
    watch(
      paramStrategies.map((s) => () => s.param(route.params)),
      (newParams, oldParams) => {
        for (let i = 0; i < newParams.length; i++) {
          if (newParams[i] !== oldParams?.[i]) {
            paramStrategies[i].callback(newParams[i]);
            break;
          }
        }
      },
      { flush: "post" },
    );
  }

  // Watch query params
  if (queryStrategies.length > 0) {
    watch(
      queryStrategies.map((s) => () => s.query(route.query)),
      (newQueries, oldQueries) => {
        for (let i = 0; i < newQueries.length; i++) {
          if (newQueries[i] !== oldQueries?.[i]) {
            queryStrategies[i].callback(newQueries[i]);
            break;
          }
        }
      },
      { flush: "post" },
    );
  }
}
