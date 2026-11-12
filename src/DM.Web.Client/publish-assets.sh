#!/bin/sh
# Publishes the hashed assets of this image into the directory nginx serves /assets/ from.
#
# Both frontends of a rollout serve from one volume, and a vendor chunk keeps its name
# across releases, so a file already there may be the one the other frontend is serving
# right now: it is touched, which tells the rollout's cleanup it is still in use, and never
# rewritten. A new file is copied under a temporary name in the same directory and renamed,
# so a browser never reads half of it.
#
# The two variables exist for the test in scripts/deployment/check-nginx-configuration.sh.
set -eu

from=${DM_ASSETS_FROM:-/usr/share/nginx/html/assets}
to=${DM_ASSETS_TO:-/var/lib/dm-front/assets}

# Vite writes a flat directory. A subdirectory or a missing source makes cp fail, and the
# stock entrypoint stops the container on it: a frontend that cannot publish its bundle
# would serve a document whose scripts are 404.
for file in "$from"/*; do
  name=${file##*/}
  if [ -e "$to/$name" ]; then
    touch -c "$to/$name"
  else
    cp "$file" "$to/.$name.$$"
    # -n: the other frontend renamed the same file in first, and its copy stays.
    mv -n "$to/.$name.$$" "$to/$name"
    rm -f "$to/.$name.$$"
  fi
done
