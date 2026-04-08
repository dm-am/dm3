import { type RouteParams, type LocationQuery, type LocationQueryValue, useRoute } from "vue-router";
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
