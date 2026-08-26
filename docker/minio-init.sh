#!/bin/sh
set -eu

# Bootstrap of the object store. Root creates the bucket and the two scoped
# accounts here and is then out of the picture: the application and imgproxy
# hold keys that can only touch objects of one bucket, so a leaked workload
# configuration cannot drop the bucket, rewrite its policy or mint further
# accounts.

BUCKET="${BUCKET_NAME:-dm-uploads}"
ENDPOINT="${MINIO_ENDPOINT:-http://dm-minio:9000}"
ROOT_USER="${MINIO_ROOT_USER:-minio}"
APP_USER="${MINIO_APP_USER:-dm-app}"
IMGPROXY_USER="${MINIO_IMGPROXY_USER:-dm-imgproxy}"

if [ -z "${MINIO_APP_PASSWORD:-}" ] || [ -z "${MINIO_IMGPROXY_PASSWORD:-}" ]; then
    echo "ERROR: MINIO_APP_PASSWORD and MINIO_IMGPROXY_PASSWORD must be set: refusing to leave the workloads on the root account" >&2
    exit 1
fi

# The health check gates this container, but only the alias proves the
# credentials are accepted. Worth a few retries rather than failing the whole
# stack because the server was a second late.
attempt=0
until mc alias set dm "$ENDPOINT" "$ROOT_USER" "$MINIO_ROOT_PASSWORD" > /dev/null 2>&1; do
    attempt=$((attempt + 1))
    if [ "$attempt" -ge 30 ]; then
        echo "ERROR: $ENDPOINT did not accept the root alias after $attempt attempts" >&2
        exit 1
    fi
    sleep 2
done

mc mb --ignore-existing "dm/$BUCKET"

# Anonymous GET is how public content is served (no presigned link per avatar),
# and it is granted per prefix: a type nobody declared public must not become
# readable just by being added. Mirrors UploadFolder.AnonymouslyReadable, and
# DeploymentConfigurationShould fails the build when the two drift apart.
#
# Set here rather than by the application: a bucket policy is an administrative
# call, and the scoped account below is deliberately not allowed to make one.
#
# set-json and not `mc anonymous set download`, for two reasons. A prefixed
# `set` adds a statement without removing one already in place, so on a stand
# that has run before, the bucket-wide grant would survive the correction. And
# the grant this replaces was `download` on the whole bucket, which is mc's
# readonly policy — GetObject on every key AND anonymous ListBucket, reachable
# from outside through the proxy, so the keys were enumerable and the random
# suffix in them protected nothing.
cat > /tmp/dm-anonymous-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {"AWS": ["*"]},
      "Action": ["s3:GetObject"],
      "Resource": [
        "arn:aws:s3:::$BUCKET/avatars/*",
        "arn:aws:s3:::$BUCKET/characters/*"
      ]
    }
  ]
}
EOF
mc anonymous set-json /tmp/dm-anonymous-policy.json "dm/$BUCKET" > /dev/null
rm -f /tmp/dm-anonymous-policy.json

# Exactly what the upload path performs: PutObject on upload, DeleteObject on
# rollback and by the orphan sweeper, ListBucket for that sweep, GetObject to
# read back. No multipart - uploads are size capped well below the threshold.
cat > /tmp/dm-app-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": ["arn:aws:s3:::$BUCKET/*"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:ListBucket", "s3:GetBucketLocation"],
      "Resource": ["arn:aws:s3:::$BUCKET"]
    }
  ]
}
EOF

# imgproxy renders untrusted input and never writes: read is all it gets.
cat > /tmp/dm-imgproxy-policy.json <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject"],
      "Resource": ["arn:aws:s3:::$BUCKET/*"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:GetBucketLocation"],
      "Resource": ["arn:aws:s3:::$BUCKET"]
    }
  ]
}
EOF

# Both of these replace what is already there, so a restart re-applies the
# policy from the repository and the password from the environment instead of
# failing on an account that exists.
mc admin policy create dm dm-app /tmp/dm-app-policy.json
mc admin policy create dm dm-imgproxy /tmp/dm-imgproxy-policy.json
mc admin user add dm "$APP_USER" "$MINIO_APP_PASSWORD"
mc admin user add dm "$IMGPROXY_USER" "$MINIO_IMGPROXY_PASSWORD"

# Attach is the one call that is not idempotent: it exits non-zero when the
# policy is already in effect, which would fail every restart of the stack.
mc admin policy attach dm dm-app --user "$APP_USER" ||
    echo "policy dm-app is already attached to $APP_USER"
mc admin policy attach dm dm-imgproxy --user "$IMGPROXY_USER" ||
    echo "policy dm-imgproxy is already attached to $IMGPROXY_USER"

echo "MinIO bootstrap complete: bucket $BUCKET, accounts $APP_USER and $IMGPROXY_USER"
