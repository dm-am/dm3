#!/bin/sh
set -eu

# Start of the object store, and none of it needs an administrator: SeaweedFS
# creates the bucket itself from -bucket, and the accounts and the anonymous
# reads come from an identity file written below from the passwords in the
# environment. The file is rewritten on every start, so a changed password takes
# effect on the next one and nothing is remembered in the volume.
#
# There is no AWS_ACCESS_KEY_ID in this container on purpose: SeaweedFS turns an
# environment key into a global administrator that no bucket can scope.

# A value goes into JSON between double quotes, and the only characters that
# would break out of such a string are the quote, the backslash and control
# characters. The single quote is refused with them because the same values sit
# in docker/.env, which _env.sh unquotes. Refusing names the variable and never
# prints the value.
check() {
    name="$1"
    eval "value=\${$name:-}"
    if [ -z "$value" ]; then
        echo "storage-start: $name is empty" >&2
        exit 1
    fi
    case "$value" in
        *\"*|*\\*|*\'*)
            echo "storage-start: $name contains a forbidden character" >&2
            exit 1 ;;
    esac
    # Compared after stripping rather than grepped: grep reads the value line by
    # line and never sees the newline it would have to report.
    if [ "$(printf '%s' "$value" | tr -d '[:cntrl:]')" != "$value" ]; then
        echo "storage-start: $name contains a control character" >&2
        exit 1
    fi
}

for v in STORAGE_BUCKET STORAGE_APP_USER STORAGE_APP_PASSWORD \
         STORAGE_IMGPROXY_USER STORAGE_IMGPROXY_PASSWORD \
         STORAGE_BACKUP_USER STORAGE_BACKUP_PASSWORD; do
    check "$v"
done

# The bucket name also goes into a flag and into the health check URL.
case "$STORAGE_BUCKET" in
    *[!a-z0-9.-]*)
        echo "storage-start: STORAGE_BUCKET is not a bucket name (a-z, 0-9, dot, hyphen)" >&2
        exit 1 ;;
esac

# Every action carries the bucket suffix, and none of them is Admin: an action
# without a suffix applies to every bucket, Admin creates and drops them.
#
# dm-app reads and writes objects and cannot list them: the application only
# ever addresses a key it stored in the database. Listing belongs to dm-backup,
# which has to walk the bucket and cannot write. imgproxy parses untrusted image
# data and only reads.
#
# Anonymous reads mirror UploadFolder.AnonymouslyReadable, prefix by prefix, and
# grant no List: a public listing would turn the random part of every key into
# a directory. StorageConfigurationShould fails the build when the two drift.
B="$STORAGE_BUCKET"
umask 077
cat > /tmp/s3.json <<JSON
{"identities":[
 {"name":"dm-app","credentials":[{"accessKey":"$STORAGE_APP_USER","secretKey":"$STORAGE_APP_PASSWORD"}],
  "actions":["Read:$B","Write:$B"]},
 {"name":"dm-imgproxy","credentials":[{"accessKey":"$STORAGE_IMGPROXY_USER","secretKey":"$STORAGE_IMGPROXY_PASSWORD"}],
  "actions":["Read:$B"]},
 {"name":"dm-backup","credentials":[{"accessKey":"$STORAGE_BACKUP_USER","secretKey":"$STORAGE_BACKUP_PASSWORD"}],
  "actions":["Read:$B","List:$B"]},
 {"name":"anonymous","actions":["Read:$B/avatars/*","Read:$B/characters/*"]}
]}
JSON
# The image entrypoint drops root before starting the server, so the file is
# handed to the user that will read it, and to nobody else.
chown seaweed:seaweed /tmp/s3.json
chmod 0600 /tmp/s3.json

# The server writes the flags it was given into the volume and reads them back
# on the next start, preferring the stored value whenever the command line holds
# the default. A flag removed from this file would keep working for one more
# start, and a default written out explicitly would lose to the stored value
# forever. Removed here, the command below is the whole configuration. The file
# holds no secret, only the path of the identity file.
rm -f /data/mini.options

# One process with every service bound to loopback inside the container, and
# the listeners on all interfaces of the one network it is attached to. Why that
# network is the boundary is written at storage-internal in docker-compose.yml.
#
# -s3.iam=false closes the API that would mint accounts beside the file above.
# The volume size is set explicitly because the default is otherwise derived
# from the size of the disk, and telemetry because it is on by default.
exec /entrypoint.sh mini -dir=/data -ip=127.0.0.1 -ip.bind=0.0.0.0 -s3.port=8333 \
    -bucket="$B" \
    -s3.config=/tmp/s3.json -s3.iam=false -s3.autoCreateBucket=false \
    -s3.allowDeleteBucketNotEmpty=false -webdav=false -admin.ui=false \
    -s3.port.iceberg=0 -s3.port.lance=0 -master.telemetry=false \
    -master.volumeSizeLimitMB=128 -metricsPort=9324
